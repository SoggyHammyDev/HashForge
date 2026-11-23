namespace HashForge.Domain.UnitTests;

public sealed class WorkMathTests
{
    [Fact]
    public void GIVEN_EffortPercent_WHEN_WorkEqualsNetworkDifficulty_THEN_ReturnsOneHundred()
    {
        Assert.Equal(100d, WorkMath.EffortPercent(5000, 5000));
    }

    [Fact]
    public void GIVEN_EffortPercent_WHEN_WorkIsHalfNetworkDifficulty_THEN_ReturnsFifty()
    {
        Assert.Equal(50d, WorkMath.EffortPercent(2500, 5000));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void GIVEN_EffortPercent_WHEN_NetworkDifficultyIsUnknown_THEN_ReturnsZero(double networkDifficulty)
    {
        Assert.Equal(0d, WorkMath.EffortPercent(100, networkDifficulty));
    }

    [Fact]
    public void GIVEN_Hashrate_WHEN_OneDifficultyPerSecond_THEN_ReturnsHashesPerDifficulty()
    {
        Assert.Equal(WorkMath.HashesPerDifficulty, WorkMath.Hashrate(60, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void GIVEN_Hashrate_WHEN_PeriodIsZero_THEN_ReturnsZero()
    {
        Assert.Equal(0d, WorkMath.Hashrate(60, TimeSpan.Zero));
    }
}
