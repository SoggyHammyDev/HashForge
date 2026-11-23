using HashForge.Mining.History;

namespace HashForge.Web.Components.Shared;

/// <summary>Maps hashrate samples onto a chart area.</summary>
public static class ChartGeometry
{
    private static readonly double[] NiceSteps = [1, 2, 5, 10];

    /// <summary>Rounds a maximum up to 1, 2 or 5 times a power of ten so the axis ends on a clean number.</summary>
    public static double NiceMaximum(double maximum)
    {
        if (!double.IsFinite(maximum) || maximum <= 0)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(maximum)));
        return NiceSteps.Select(step => step * magnitude).First(candidate => candidate >= maximum);
    }

    /// <summary>Places samples left to right by time and bottom to top by hashrate, with zero on the baseline.</summary>
    public static IReadOnlyList<ChartPoint> Plot(IReadOnlyList<PoolHistorySample> samples, ChartArea area, double axisMaximum)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(area);
        if (samples.Count == 0)
        {
            return [];
        }

        var start = samples[0].Timestamp;
        var span = (samples[^1].Timestamp - start).TotalSeconds;
        return
        [
            .. samples.Select(sample =>
            {
                var along = span <= 0 ? 0 : (sample.Timestamp - start).TotalSeconds / span;
                var up = axisMaximum <= 0 ? 0 : Math.Clamp(sample.Hashrate / axisMaximum, 0, 1);
                return new ChartPoint(area.Left + (along * area.Width), area.Bottom - (up * area.Height), sample);
            }),
        ];
    }
}
