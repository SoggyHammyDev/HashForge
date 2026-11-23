using System.Globalization;
using System.Text.RegularExpressions;

namespace HashForge.Web.Branding;

/// <summary>Turns the configured accent colour into the CSS variables the stylesheet uses.</summary>
public static partial class BrandTheme
{
    private const string DarkText = "#1b1204";
    private const string LightText = "#ffffff";
    private const double SoftOpacity = 0.14;

    /// <summary>Above this perceived brightness, dark text reads better on the accent colour than white.</summary>
    private const double LightBackgroundThreshold = 150;

    public static bool IsHexColor(string? value) => value is not null && HexColor().IsMatch(value);

    /// <summary>Text colour that stays readable on top of the accent colour.</summary>
    public static string TextColorOn(string accentColor)
    {
        var (red, green, blue) = Parse(accentColor);
        var brightness = (0.299 * red) + (0.587 * green) + (0.114 * blue);
        return brightness > LightBackgroundThreshold ? DarkText : LightText;
    }

    /// <summary>A CSS rule that overrides the accent variables. Only call with a validated colour.</summary>
    public static string CssOverrides(string accentColor)
    {
        var (red, green, blue) = Parse(accentColor);
        var soft = SoftOpacity.ToString(CultureInfo.InvariantCulture);
        return $":root{{--accent:{accentColor};--accent-soft:rgba({red},{green},{blue},{soft});--on-accent:{TextColorOn(accentColor)}}}";
    }

    private static (int Red, int Green, int Blue) Parse(string accentColor)
    {
        if (!IsHexColor(accentColor))
        {
            throw new ArgumentException("Colour must be written as #RRGGBB.", nameof(accentColor));
        }

        int Channel(int start) => int.Parse(accentColor.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (Channel(1), Channel(3), Channel(5));
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
