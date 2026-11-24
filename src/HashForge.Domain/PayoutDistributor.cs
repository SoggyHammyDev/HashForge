using System.Numerics;

namespace HashForge.Domain;

/// <summary>Splits a block reward between miners in proportion to their weights.</summary>
public static class PayoutDistributor
{
    private const decimal PercentScale = 100m;

    /// <summary>
    /// Returns payouts that always sum to <paramref name="rewardSatoshis"/>. Miners beyond the output cap or
    /// below the minimum payout are dropped and their portion is shared between the remaining miners.
    /// The fee address never receives more than the fee: anything that cannot be split goes to a miner.
    /// </summary>
    /// <exception cref="ArgumentException">There is reward to pay out but no miner to pay it to.</exception>
    public static IReadOnlyList<PayoutEntry> Distribute(
        IReadOnlyDictionary<string, double> weights,
        long rewardSatoshis,
        PayoutRules rules,
        FinderBonus? finderBonus = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegative(rewardSatoshis);

        var fee = FeeFor(rewardSatoshis, rules);
        var bonus = finderBonus is null ? 0 : Math.Clamp(finderBonus.Satoshis, 0, rewardSatoshis - fee);
        var distributable = rewardSatoshis - fee - bonus;

        // The fee and the finder's bonus may each need an output of their own, so a slot is held back for each.
        var reservedSlots = (fee > 0 ? 1 : 0) + (bonus > 0 ? 1 : 0);
        var payouts = SplitBetweenMiners(weights, distributable, rules, reservedSlots);
        if (payouts.Count == 0 && distributable > 0)
        {
            payouts = PayLargestMiner(weights, distributable);
        }

        if (bonus > 0)
        {
            Credit(payouts, finderBonus!.Address, bonus, atFront: true);
        }

        var forMiners = rewardSatoshis - fee;
        if (payouts.Count == 0 && forMiners > 0)
        {
            throw new ArgumentException("There is reward to pay out but no miner with any weight to pay it to.", nameof(weights));
        }

        // Whatever rounding or the minimum payout left unassigned belongs to the miners, not the pool.
        var unassigned = forMiners - payouts.Sum(p => p.Satoshis);
        if (unassigned > 0)
        {
            payouts[0] = payouts[0] with { Satoshis = payouts[0].Satoshis + unassigned };
        }

        if (fee > 0)
        {
            Credit(payouts, rules.FeeAddress, fee, atFront: false);
        }

        return payouts;
    }

    /// <summary>
    /// The pool's fee, or zero when there is no fee address or the fee is too small to be worth an output of
    /// its own, in which case the miners keep it.
    /// </summary>
    private static long FeeFor(long rewardSatoshis, PayoutRules rules)
    {
        if (string.IsNullOrWhiteSpace(rules.FeeAddress))
        {
            return 0;
        }

        var fee = (long)Math.Floor(rewardSatoshis * rules.FeePercent / PercentScale);
        return fee >= rules.MinimumPayoutSatoshis ? fee : 0;
    }

    private static List<PayoutEntry> SplitBetweenMiners(
        IReadOnlyDictionary<string, double> weights,
        long distributable,
        PayoutRules rules,
        int reservedSlots)
    {
        var minerSlots = Math.Max(rules.MaxOutputs - reservedSlots, 0);
        var eligible = ByWeight(weights).Take(minerSlots).ToList();

        while (eligible.Count > 0)
        {
            // Whole-number weights make each portion exact; only the fraction of a satoshi is rounded down.
            var exact = ExactWeights.ToIntegers([.. eligible.Select(w => w.Value)]);
            var total = exact.Aggregate(BigInteger.Zero, BigInteger.Add);
            var payouts = eligible
                .Select((w, index) => new PayoutEntry(w.Key, (long)(distributable * exact[index] / total)))
                .ToList();
            var kept = payouts.Count(p => p.Satoshis >= rules.MinimumPayoutSatoshis && p.Satoshis > 0);
            if (kept == payouts.Count)
            {
                return payouts;
            }

            // Payouts are ordered largest first, so the ones that fall short are always at the tail.
            eligible = eligible.Take(kept).ToList();
        }

        return [];
    }

    /// <summary>
    /// Used when no miner's cut clears the minimum payout: rather than the pool keeping the reward, the miner
    /// who did the most work is paid all of it.
    /// </summary>
    private static List<PayoutEntry> PayLargestMiner(IReadOnlyDictionary<string, double> weights, long satoshis) =>
        [.. ByWeight(weights).Take(1).Select(w => new PayoutEntry(w.Key, satoshis))];

    /// <summary>Miners with any real weight, largest first, with ties broken by address so the result is repeatable.</summary>
    private static IEnumerable<KeyValuePair<string, double>> ByWeight(IReadOnlyDictionary<string, double> weights) =>
        weights
            .Where(w => w.Value > 0 && double.IsFinite(w.Value))
            .OrderByDescending(w => w.Value)
            .ThenBy(w => w.Key, StringComparer.Ordinal);

    /// <summary>Adds an amount to an address's payout, creating the payout when there is none.</summary>
    private static void Credit(List<PayoutEntry> payouts, string address, long satoshis, bool atFront)
    {
        var existing = payouts.FindIndex(p => p.Address == address);
        if (existing >= 0)
        {
            payouts[existing] = payouts[existing] with { Satoshis = payouts[existing].Satoshis + satoshis };
            return;
        }

        payouts.Insert(atFront ? 0 : payouts.Count, new PayoutEntry(address, satoshis));
    }
}
