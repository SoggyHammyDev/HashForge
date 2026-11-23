using HashForge.Mining.History;
using HashForge.Web.Components.Shared;

namespace HashForge.Web.UnitTests;

public sealed class ChartGeometryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, 1)]
    [InlineData(1.1, 2)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(2.4e12, 5e12)]
    public void GIVEN_NiceMaximum_WHEN_ValueGiven_THEN_RoundsUpToOneTwoOrFiveTimesPowerOfTen(double maximum, double expected)
    {
        Assert.Equal(expected, ChartGeometry.NiceMaximum(maximum), precision: 3);
    }

    [Fact]
    public void GIVEN_Plot_WHEN_SamplesSpanTimeAndValue_THEN_PointsFillAreaWithZeroOnBaseline()
    {
        var area = new ChartArea(Left: 10, Top: 20, Width: 100, Height: 50);
        PoolHistorySample[] samples =
        [
            new("p", Now, 0, 1, 1),
            new("p", Now.AddMinutes(5), 50, 1, 1),
            new("p", Now.AddMinutes(10), 100, 1, 1),
        ];

        var points = ChartGeometry.Plot(samples, area, axisMaximum: 100);

        Assert.Equal([(10d, 70d), (60d, 45d), (110d, 20d)], points.Select(p => (p.X, p.Y)));
        Assert.Same(samples[1], points[1].Sample);
        Assert.Equal((110d, 70d), (area.Right, area.Bottom));
    }

    [Fact]
    public void GIVEN_Plot_WHEN_NoSamplesOrDegenerateRanges_THEN_DoesNotDivideByZero()
    {
        var area = new ChartArea(0, 0, 100, 50);

        Assert.Empty(ChartGeometry.Plot([], area, 100));
        var single = ChartGeometry.Plot([new PoolHistorySample("p", Now, 500, 1, 1)], area, axisMaximum: 0);
        Assert.Equal((0d, 50d), (single[0].X, single[0].Y));
    }
}
