namespace HashForge.Web.Branding;

/// <summary>Lets whoever runs the pool present it under their own name, colour and logo.</summary>
public sealed class BrandingOptions
{
    public const string SectionName = "Branding";
    public const string DefaultName = "HashForge";
    public const string RepositoryUrl = "https://github.com/owenashurst/HashForge";
    public const int MaxNameLength = 40;
    public const int MaxTaglineLength = 120;

    /// <summary>The pool's name, shown in the header, page titles and footer.</summary>
    public string Name { get; set; } = DefaultName;

    /// <summary>A short line shown beside the name in the footer.</summary>
    public string Tagline { get; set; } = "Open-source Bitcoin mining pool";

    /// <summary>The highlight colour as #RRGGBB, used for the logo, the active tab and primary buttons.</summary>
    public string AccentColor { get; set; } = "#f7931a";

    /// <summary>An image to use as the logo (an https:// URL or a path starting with /). Empty shows the name's initial.</summary>
    public string LogoUrl { get; set; } = string.Empty;

    public bool IsDefaultName => string.Equals(Name, DefaultName, StringComparison.Ordinal);

    /// <summary>The first letter of the name, shown as the logo when no image is configured.</summary>
    public string Initial => Name.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : "?";

    /// <summary>The browser tab title for a page, e.g. "Blocks – My Pool".</summary>
    public string PageTitle(string? page) => string.IsNullOrWhiteSpace(page) ? Name : $"{page} – {Name}";
}
