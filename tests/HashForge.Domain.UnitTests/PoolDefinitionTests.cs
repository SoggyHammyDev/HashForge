namespace HashForge.Domain.UnitTests;

public sealed class PoolDefinitionTests
{
    [Fact]
    public void GIVEN_ToPayoutRules_WHEN_Called_THEN_CopiesPayoutSettings()
    {
        var pool = new PoolDefinition
        {
            Name = "pplns",
            Scheme = PayoutScheme.Pplns,
            Port = 3334,
            FeeAddress = "pool",
            FeePercent = 1.5m,
            MaxCoinbaseOutputs = 100,
            MinimumPayoutSatoshis = 2_000,
        };

        Assert.Equal(new PayoutRules(1.5m, "pool", 100, 2_000), pool.ToPayoutRules());
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(99, 99)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    public void GIVEN_CappedAt_WHEN_CeilingGiven_THEN_LimitIsLoweredButNeverRaised(int ceiling, int expected)
    {
        var rules = new PayoutRules(1.5m, "pool", 100, 2_000);

        Assert.Equal(rules with { MaxOutputs = expected }, rules.CappedAt(ceiling));
    }

    [Fact]
    public void GIVEN_PoolDefinition_WHEN_OnlyRequiredValuesSet_THEN_DefaultsApply()
    {
        var pool = new PoolDefinition { Name = "solo", Scheme = PayoutScheme.Solo, Port = 3333, FeeAddress = "pool" };

        Assert.True(pool.Enabled);
        Assert.Equal(PoolDefinition.DefaultMaxCoinbaseOutputs, pool.MaxCoinbaseOutputs);
        Assert.Equal(PoolDefinition.DefaultMinimumPayoutSatoshis, pool.MinimumPayoutSatoshis);
        Assert.Equal(PoolDefinition.DefaultPplnsWindowMultiplier, pool.PplnsWindowMultiplier);
    }

    [Fact]
    public void GIVEN_ShareResult_WHEN_OutcomeIsAccepted_THEN_IsAcceptedIsTrue()
    {
        var worker = new WorkerIdentity("addr", "rig");

        var accepted = new ShareResult("solo", worker, ShareOutcome.Accepted, 1, 2, DateTimeOffset.UnixEpoch);
        var rejected = accepted with { Outcome = ShareOutcome.Duplicate };

        Assert.True(accepted.IsAccepted);
        Assert.False(rejected.IsAccepted);
    }
}
