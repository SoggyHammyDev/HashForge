using HashForge.Domain;
using HashForge.Mining.Pools;

namespace HashForge.Mining.UnitTests.Pools;

public sealed class PoolValidationTests
{
    private readonly MiningOptionsValidator _optionsValidator = new();

    [Fact]
    public void GIVEN_Validate_WHEN_PoolIsWellFormed_THEN_ReturnsNoErrors()
    {
        Assert.Empty(PoolDefinitionValidator.Validate(TestData.Pool()));
        Assert.Null(PoolDefinitionValidator.FirstError(TestData.Pool()));
    }

    public static TheoryData<PoolDefinition, string> InvalidPools() => new()
    {
        { TestData.Pool() with { Name = "Has Spaces" }, "name" },
        { TestData.Pool() with { Name = "" }, "name" },
        { TestData.Pool() with { Port = 0 }, "port" },
        { TestData.Pool() with { Port = 70000 }, "port" },
        { TestData.Pool() with { FeePercent = -1 }, "fee" },
        { TestData.Pool() with { FeePercent = 101 }, "fee" },
        { TestData.Pool() with { FeeAddress = " " }, "fee address" },
        { TestData.Pool() with { MaxCoinbaseOutputs = 0 }, "coinbase output" },
        { TestData.Pool() with { MinimumPayoutSatoshis = 545 }, "minimum payout" },
        { TestData.Pool() with { PplnsWindowMultiplier = 0 }, "window multiplier" },
    };

    [Theory]
    [MemberData(nameof(InvalidPools))]
    public void GIVEN_Validate_WHEN_PoolHasInvalidSetting_THEN_ReturnsErrorNamingIt(PoolDefinition pool, string expectedFragment)
    {
        var errors = PoolDefinitionValidator.Validate(pool);

        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(errors[0], PoolDefinitionValidator.FirstError(pool));
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_DefaultsWithValidPools_THEN_Succeeds()
    {
        var options = new MiningOptions { Pools = [TestData.Pool("a", port: 1), TestData.Pool("b", port: 2)] };

        Assert.True(_optionsValidator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_PoolNamesOrPortsRepeat_THEN_FailsForEach()
    {
        var options = new MiningOptions { Pools = [TestData.Pool("a", port: 1), TestData.Pool("A", port: 1)] };

        var result = _optionsValidator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("pool name"));
        Assert.Contains(result.Failures!, f => f.Contains("pool port"));
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_PoolIsInvalid_THEN_FailsWithPoolError()
    {
        var options = new MiningOptions { Pools = [TestData.Pool() with { Port = 0 }] };

        Assert.Contains(_optionsValidator.Validate(null, options).Failures!, f => f.Contains("port"));
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_CoinbaseTagTooLong_THEN_Fails()
    {
        var options = new MiningOptions { CoinbaseTag = new string('x', MiningOptions.MaxCoinbaseTagLength + 1) };

        Assert.Contains(_optionsValidator.Validate(null, options).Failures!, f => f.Contains("CoinbaseTag"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void GIVEN_OptionsValidate_WHEN_Extranonce2SizeOutOfRange_THEN_Fails(int size)
    {
        var options = new MiningOptions { Extranonce2Size = size };

        Assert.Contains(_optionsValidator.Validate(null, options).Failures!, f => f.Contains("Extranonce2Size"));
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_IntervalsAreNotPositive_THEN_FailsForEachInterval()
    {
        var options = new MiningOptions
        {
            ChainTipPollSeconds = 0,
            TemplateRefreshSeconds = 0,
            NetworkStatsRefreshSeconds = 0,
            HashrateWindowMinutes = 0,
            StateSnapshotSeconds = 0,
            HistorySampleMinutes = -1,
        };

        Assert.Equal(6, _optionsValidator.Validate(null, options).Failures!.Count());
    }

    [Fact]
    public void GIVEN_Validate_WHEN_FinderBonusSchemeHasDustBonus_THEN_ReturnsError()
    {
        var pool = TestData.Pool(scheme: PayoutScheme.FinderBonus) with { FinderBonusSatoshis = 100 };

        Assert.Contains(PoolDefinitionValidator.Validate(pool), e => e.Contains("finder bonus"));
        Assert.Empty(PoolDefinitionValidator.Validate(TestData.Pool(scheme: PayoutScheme.Prop) with { FinderBonusSatoshis = 0 }));
    }
}
