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
    public void GIVEN_Distribute_WHEN_NoWeights_THEN_FeeAddressReceivesWholeReward()
    {
        var payouts = PayoutDistributor.Distribute(Weights(), Reward, Rules(feePercent: 1));

        Assert.Equal([new PayoutEntry(FeeAddress, Reward)], payouts);
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
}
