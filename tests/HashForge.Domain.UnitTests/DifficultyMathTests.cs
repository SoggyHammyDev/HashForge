using System.Numerics;

namespace HashForge.Domain.UnitTests;

public sealed class DifficultyMathTests
{
    private const uint GenesisBits = 0x1d00ffff;
    private const string GenesisHashDisplayHex = "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f";

    [Fact]
    public void GIVEN_TargetFromCompact_WHEN_BitsAreGenesisBits_THEN_ReturnsDifficultyOneTarget()
    {
        Assert.Equal(DifficultyMath.Diff1Target, DifficultyMath.TargetFromCompact(GenesisBits));
    }

    [Fact]
    public void GIVEN_TargetFromCompact_WHEN_ExponentIsBelowMantissaSize_THEN_MantissaIsShiftedRight()
    {
        Assert.Equal(new BigInteger(0x12), DifficultyMath.TargetFromCompact(0x01123456));
    }

    [Fact]
    public void GIVEN_DifficultyFromTarget_WHEN_TargetIsDifficultyOneTarget_THEN_ReturnsOne()
    {
        Assert.Equal(1d, DifficultyMath.DifficultyFromTarget(DifficultyMath.Diff1Target));
    }

    [Fact]
    public void GIVEN_DifficultyFromTarget_WHEN_TargetIsZero_THEN_ReturnsInfinity()
    {
        Assert.Equal(double.PositiveInfinity, DifficultyMath.DifficultyFromTarget(BigInteger.Zero));
    }

    [Fact]
    public void GIVEN_DifficultyFromHash_WHEN_HashIsGenesisBlockHash_THEN_ReturnsKnownDifficulty()
    {
        var hash = Convert.FromHexString(GenesisHashDisplayHex);
        Array.Reverse(hash);

        Assert.Equal(2536.43, DifficultyMath.DifficultyFromHash(hash), precision: 2);
    }

    [Fact]
    public void GIVEN_HashToInteger_WHEN_HighByteIsSet_THEN_ResultIsPositive()
    {
        var hash = new byte[32];
        hash[31] = 0xff;

        Assert.True(DifficultyMath.HashToInteger(hash).Sign > 0);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(0.001)]
    [InlineData(65536d)]
    public void GIVEN_TargetFromDifficulty_WHEN_ConvertedBack_THEN_DifficultyRoundTrips(double difficulty)
    {
        var target = DifficultyMath.TargetFromDifficulty(difficulty);

        Assert.Equal(difficulty, DifficultyMath.DifficultyFromTarget(target), precision: 6);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void GIVEN_TargetFromDifficulty_WHEN_DifficultyIsNotPositive_THEN_Throws(double difficulty)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DifficultyMath.TargetFromDifficulty(difficulty));
    }
}
