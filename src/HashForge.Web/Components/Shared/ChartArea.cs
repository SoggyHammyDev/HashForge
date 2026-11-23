namespace HashForge.Web.Components.Shared;

/// <summary>The plotting area of a chart, in SVG user units.</summary>
public sealed record ChartArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}
