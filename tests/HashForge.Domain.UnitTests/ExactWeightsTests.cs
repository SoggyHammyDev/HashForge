using System.Numerics;

namespace HashForge.Domain.UnitTests;

public sealed class ExactWeightsTests
{
    [Fact]
    public void GIVEN_ToIntegers_WHEN_WeightsAreWholeNumbers_THEN_ProportionsAreKept()
    {
        var integers = ExactWeights.ToIntegers([3, 2, 1]);

        Assert.Equal(integers[2] * 3, integers[0]);
        Assert.Equal(integers[2] * 2, integers[1]);
    }

    [Fact]
    public void GIVEN_ToIntegers_WHEN_WeightsArePowersOfTwo_THEN_SmallestBecomesOne()
    {
        Assert.Equal([new BigInteger(4), new BigInteger(2), BigInteger.One], ExactWeights.ToIntegers([1, 0.5, 0.25]).Select(Normalise(0.25)));
    }

    [Fact]
    public void GIVEN_ToIntegers_WHEN_WeightsDifferEnormously_THEN_NeitherIsRoundedAway()
    {
        var integers = ExactWeights.ToIntegers([1e15, 2e-11]);

        // Dividing back gives the original ratio; with rounded arithmetic the small weight would vanish.
        Assert.Equal(1e15 / 2e-11, (double)integers[0] / (double)integers[1], precision: 0);
        Assert.True(integers[1] > 0);
    }

    [Fact]
    public void GIVEN_ToIntegers_WHEN_WeightIsTheSmallestDoubleThereIs_THEN_ItIsOne()
    {
        Assert.Equal([BigInteger.One, new BigInteger(2)], ExactWeights.ToIntegers([double.Epsilon, double.Epsilon * 2]));
    }

    [Fact]
    public void GIVEN_ToIntegers_WHEN_WeightIsZero_THEN_ItStaysZero()
    {
        var integers = ExactWeights.ToIntegers([0, 1]);

        Assert.Equal(BigInteger.Zero, integers[0]);
        Assert.True(integers[1] > 0);
    }

    [Fact]
    public void GIVEN_ToIntegers_WHEN_ThereAreNoWeights_THEN_ReturnsNothing()
    {
        Assert.Empty(ExactWeights.ToIntegers([]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void GIVEN_ToIntegers_WHEN_WeightIsNotUsable_THEN_Throws(double weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactWeights.ToIntegers([1, weight]));
    }

    /// <summary>Divides by the integer that stands for <paramref name="unit"/>, so results read as multiples of it.</summary>
    private static Func<BigInteger, BigInteger> Normalise(double unit)
    {
        var one = ExactWeights.ToIntegers([unit])[0];
        return value => value / one;
    }
}
