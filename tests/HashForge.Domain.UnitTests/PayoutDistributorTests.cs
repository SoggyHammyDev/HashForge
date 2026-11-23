namespace HashForge.Domain.UnitTests;

public sealed class PayoutDistributorTests
{
    private const string FeeAddress = "pool";
    private const long Reward = 1_000_000;

    private static PayoutRules Rules(decimal feePercent = 0, int maxOutputs = 10, long minimum = 1) =>
        new(feePercent, FeeAddress, maxOutputs, minimum);

    private static Dictionary<string, double> Weights(params (string Address, double Weight)[] entries) =>
        entries.ToDictionary(e => e.Address, e => e.Weight);

    [Fact]
    public void GIVEN_Distribute_WHEN_TwoMinersWithEqualWeight_THEN_RewardIsSplitEvenly()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 1), ("b", 1)), Reward, Rules());

        Assert.Equal([new PayoutEntry("a", 500_000), new PayoutEntry("b", 500_000)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FeeIsConfigured_THEN_FeeAddressReceivesFee()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 3), ("b", 1)), Reward, Rules(feePercent: 2));

        Assert.Equal(
            [new PayoutEntry("a", 735_000), new PayoutEntry("b", 245_000), new PayoutEntry(FeeAddress, 20_000)],
            payouts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    [InlineData(100)]
    public void GIVEN_Distribute_WHEN_AnyFee_THEN_PayoutsSumToReward(double feePercent)
    {
        var weights = Weights(("a", 1), ("b", 1), ("c", 1));

        var payouts = PayoutDistributor.Distribute(weights, 999_999, Rules((decimal)feePercent));

        Assert.Equal(999_999, payouts.Sum(p => p.Satoshis));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_ThereIsRewardButNobodyToPay_THEN_ThrowsRatherThanPayingThePool()
    {
        Assert.Throws<ArgumentException>(() => PayoutDistributor.Distribute(Weights(), Reward, Rules(feePercent: 1)));
        Assert.Throws<ArgumentException>(() => PayoutDistributor.Distribute(Weights(("a", 0)), Reward, Rules()));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_WeightIsZeroOrNegative_THEN_MinerIsExcluded()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 1), ("b", 0), ("c", -5)), Reward, Rules());

        Assert.Equal([new PayoutEntry("a", Reward)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_MinerIsBelowMinimum_THEN_TheirPortionGoesToRemainingMiners()
    {
        var weights = Weights(("big", 999), ("tiny", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(minimum: 5_000));

        Assert.Equal([new PayoutEntry("big", Reward)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_MoreMinersThanOutputCap_THEN_SmallestMinersAreDroppedAndOneSlotIsReservedForFee()
    {
        var weights = Weights(("a", 4), ("b", 3), ("c", 2), ("d", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(feePercent: 10, maxOutputs: 3));

        Assert.Equal(["a", "b", FeeAddress], payouts.Select(p => p.Address));
        Assert.Equal(Reward, payouts.Sum(p => p.Satoshis));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_RoundingRemainderIsDust_THEN_LargestMinerReceivesIt()
    {
        var weights = Weights(("a", 1), ("b", 1), ("c", 1));

        var payouts = PayoutDistributor.Distribute(weights, 1_000_000, Rules(minimum: 1_000));

        Assert.Equal(3, payouts.Count);
        Assert.Equal(333_334, payouts[0].Satoshis);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FeeAddressIsAlsoMiner_THEN_AmountsAreMergedIntoOneOutput()
    {
        var weights = Weights((FeeAddress, 1), ("b", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(feePercent: 10));

        Assert.Equal(2, payouts.Count);
        Assert.Equal(550_000, payouts.Single(p => p.Address == FeeAddress).Satoshis);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_WeightsTie_THEN_OrderIsDeterministicByAddress()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("b", 1), ("a", 1)), Reward, Rules());

        Assert.Equal(["a", "b"], payouts.Select(p => p.Address));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_RewardIsNegative_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PayoutDistributor.Distribute(Weights(), -1, Rules()));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FinderBonusApplies_THEN_FinderGetsBonusAndRestIsSplitByWeight()
    {
        var weights = Weights(("a", 3), ("b", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(feePercent: 10), new FinderBonus("b", 100_000));

        // 1,000,000 - 100,000 fee - 100,000 bonus = 800,000 split 3:1; b also receives the bonus.
        Assert.Equal(
            [new PayoutEntry("a", 600_000), new PayoutEntry("b", 300_000), new PayoutEntry(FeeAddress, 100_000)],
            payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FinderHasNoShareOfSplit_THEN_FinderStillGetsBonusAsOwnOutput()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 1)), Reward, Rules(), new FinderBonus("finder", 250_000));

        Assert.Equal([new PayoutEntry("finder", 250_000), new PayoutEntry("a", 750_000)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_BonusExceedsReward_THEN_BonusIsCappedAtRewardAfterFee()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 1)), Reward, Rules(feePercent: 10), new FinderBonus("finder", 5_000_000));

        Assert.Equal([new PayoutEntry("finder", 900_000), new PayoutEntry(FeeAddress, 100_000)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_BonusAppliesAtOutputCap_THEN_OutputCountStaysWithinCap()
    {
        var weights = Weights(("a", 4), ("b", 3), ("c", 2), ("d", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(feePercent: 10, maxOutputs: 4), new FinderBonus("finder", 100_000));

        Assert.Equal(["finder", "a", "b", FeeAddress], payouts.Select(p => p.Address));
        Assert.Equal(Reward, payouts.Sum(p => p.Satoshis));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_BonusIsZero_THEN_BehavesLikeNoBonus()
    {
        var weights = Weights(("a", 1), ("b", 1));

        Assert.Equal(
            PayoutDistributor.Distribute(weights, Reward, Rules()),
            PayoutDistributor.Distribute(weights, Reward, Rules(), new FinderBonus("a", 0)));
    }

    [Theory]
    [InlineData(PayoutScheme.Solo, "SOLO")]
    [InlineData(PayoutScheme.Prop, "PROP")]
    [InlineData(PayoutScheme.Pplns, "PPLNS")]
    [InlineData(PayoutScheme.FinderBonus, "BONUS")]
    public void GIVEN_Label_WHEN_SchemeGiven_THEN_ReturnsShortDisplayName(PayoutScheme scheme, string expected)
    {
        Assert.Equal(expected, scheme.Label());
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FeeIsZero_THEN_NoFeeOutputExistsAndMinersGetEverything()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 3), ("b", 1)), Reward, Rules(feePercent: 0));

        Assert.Equal([new PayoutEntry("a", 750_000), new PayoutEntry("b", 250_000)], payouts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GIVEN_Distribute_WHEN_PoolHasNoFeeAddress_THEN_MinersGetEverythingAndNoBlankOutputIsCreated(string feeAddress)
    {
        var rules = new PayoutRules(FeePercent: 0, feeAddress, MaxOutputs: 10, MinimumPayoutSatoshis: 1);

        var payouts = PayoutDistributor.Distribute(Weights(("a", 1), ("b", 1), ("c", 1)), Reward, rules);

        Assert.Equal(["a", "b", "c"], payouts.Select(p => p.Address));
        Assert.Equal(Reward, payouts.Sum(p => p.Satoshis));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_NoMinerClearsTheMinimumPayout_THEN_MinerWithMostWorkIsPaidNotThePool()
    {
        var weights = Weights(("small", 1), ("largest", 3), ("medium", 2));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(feePercent: 10, minimum: 5_000_000));

        // The fee itself is also below the minimum, so the miners keep that too.
        Assert.Equal([new PayoutEntry("largest", Reward)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FeeIsTooSmallForAnOutput_THEN_MinersKeepIt()
    {
        var payouts = PayoutDistributor.Distribute(Weights(("a", 1)), Reward, Rules(feePercent: 0.01m, minimum: 1_000));

        Assert.Equal([new PayoutEntry("a", Reward)], payouts);
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_FeeIsOneHundredPercent_THEN_FeeAddressTakesAllEvenWithNoMiners()
    {
        Assert.Equal([new PayoutEntry(FeeAddress, Reward)], PayoutDistributor.Distribute(Weights(), Reward, Rules(feePercent: 100)));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_RewardIsZero_THEN_ThereIsNothingToPay()
    {
        Assert.Empty(PayoutDistributor.Distribute(Weights(("a", 1)), 0, Rules(feePercent: 1)));
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_GivenManyRandomBlocks_THEN_FeeAddressNeverGetsMoreThanTheFeeAndNothingIsLost()
    {
        var random = new Random(20261123);
        for (var run = 0; run < 2_000; run++)
        {
            var reward = random.NextInt64(1, 700_000_000);
            var feePercent = (decimal)random.Next(0, 1001) / 100;
            var rules = new PayoutRules(feePercent, FeeAddress, MaxOutputs: random.Next(1, 40), MinimumPayoutSatoshis: random.Next(1, 200_000));
            var weights = Enumerable.Range(0, random.Next(1, 60)).ToDictionary(i => $"miner{i}", _ => random.NextDouble() * 1000 + 0.001);
            var bonus = random.Next(3) == 0 ? new FinderBonus("miner0", random.NextInt64(0, 200_000_000)) : null;

            var payouts = PayoutDistributor.Distribute(weights, reward, rules, bonus);

            var maximumFee = (long)Math.Floor(reward * feePercent / 100);
            var toPool = payouts.Where(p => p.Address == FeeAddress).Sum(p => p.Satoshis);
            Assert.True(toPool <= maximumFee, $"run {run}: pool got {toPool}, fee is {maximumFee}");
            Assert.Equal(reward, payouts.Sum(p => p.Satoshis));
            Assert.All(payouts, p => Assert.True(p.Satoshis > 0));
            Assert.Equal(payouts.Count, payouts.Select(p => p.Address).Distinct().Count());
        }
    }

    [Fact]
    public void GIVEN_Distribute_WHEN_BonusAppliesAndNoMinerClearsTheMinimum_THEN_LargestMinerGetsTheRestNotAZeroOutput()
    {
        var weights = Weights(("largest", 3), ("small", 1));

        var payouts = PayoutDistributor.Distribute(weights, Reward, Rules(minimum: 5_000_000), new FinderBonus("finder", 100_000));

        Assert.Equal([new PayoutEntry("finder", 100_000), new PayoutEntry("largest", 900_000)], payouts);
    }
}