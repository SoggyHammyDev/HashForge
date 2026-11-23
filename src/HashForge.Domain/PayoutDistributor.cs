namespace HashForge.Domain;

/// <summary>Splits a block reward between miners in proportion to their weights.</summary>
public static class PayoutDistributor
{
    private const decimal PercentScale = 100m;

    /// <summary>
    /// Returns payouts that always sum to <paramref name="rewardSatoshis"/>. Miners beyond the output cap or
    /// below the minimum payout are dropped and their portion is shared between the remaining miners.
    /// </summary>
    public static IReadOnlyList<PayoutEntry> Distribute(
        IReadOnlyDictionary<string, double> weights,
        long rewardSatoshis,
        PayoutRules rules,
        FinderBonus? finderBonus = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegative(rewardSatoshis);

        var fee = (long)Math.Floor(rewardSatoshis * rules.FeePercent / PercentScale);
        var bonus = finderBonus is null ? 0 : Math.Clamp(finderBonus.Satoshis, 0, rewardSatoshis - fee);
        var distributable = rewardSatoshis - fee - bonus;

        // The finder may need an output of their own, so one slot is held back for them when a bonus applies.
        var minerPayouts = SplitBetweenMiners(weights, distributable, rules, reservedSlots: bonus > 0 ? 2 : 1);
        if (bonus > 0)
        {
            AddTo(minerPayouts, finderBonus!.Address, bonus);
        }

        var poolAmount = rewardSatoshis - minerPayouts.Sum(p => p.Satoshis);

        return MergePoolAmount(minerPayouts, poolAmount, rules);
    }

    private static List<PayoutEntry> SplitBetweenMiners(
        IReadOnlyDictionary<string, double> weights,
        long distributable,
        PayoutRules rules,
        int reservedSlots)
    {
        var minerSlots = Math.Max(rules.MaxOutputs - reservedSlots, 0);
        var eligible = weights
            .Where(w => w.Value > 0)
            .OrderByDescending(w => w.Value)
            .ThenBy(w => w.Key, StringComparer.Ordinal)
            .Take(minerSlots)
            .ToList();

        while (eligible.Count > 0)
        {
            var total = eligible.Sum(w => w.Value);
            var payouts = eligible
                .Select(w => new PayoutEntry(w.Key, (long)Math.Floor(distributable * (decimal)(w.Value / total))))
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

    /// <summary>Adds an amount to an address's payout, creating the payout at the front when there is none.</summary>
    private static void AddTo(List<PayoutEntry> payouts, string address, long satoshis)
    {
        var existing = payouts.FindIndex(p => p.Address == address);
        if (existing < 0)
        {
            payouts.Insert(0, new PayoutEntry(address, satoshis));
            return;
        }

        payouts[existing] = payouts[existing] with { Satoshis = payouts[existing].Satoshis + satoshis };
    }

    private static List<PayoutEntry> MergePoolAmount(List<PayoutEntry> minerPayouts, long poolAmount, PayoutRules rules)
    {
        if (poolAmount == 0)
        {
            return minerPayouts;
        }

        var existing = minerPayouts.FindIndex(p => p.Address == rules.FeeAddress);
        var isDust = poolAmount < rules.MinimumPayoutSatoshis && minerPayouts.Count > 0;
        var target = existing >= 0 ? existing : isDust ? 0 : -1;
        if (target < 0)
        {
            minerPayouts.Add(new PayoutEntry(rules.FeeAddress, poolAmount));
            return minerPayouts;
        }

        minerPayouts[target] = minerPayouts[target] with { Satoshis = minerPayouts[target].Satoshis + poolAmount };
        return minerPayouts;
    }
}
