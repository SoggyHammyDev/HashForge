using Microsoft.Extensions.Options;

namespace HashForge.Web.Branding;

public sealed partial class BrandingOptionsValidator : IValidateOptions<BrandingOptions>
{
    public ValidateOptionsResult Validate(string? name, BrandingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Name) || options.Name.Length > BrandingOptions.MaxNameLength)
        {
            errors.Add($"Branding name is required and must be at most {BrandingOptions.MaxNameLength} characters.");
        }

        if (options.Tagline is { Length: > BrandingOptions.MaxTaglineLength })
        {
            errors.Add($"Branding tagline must be at most {BrandingOptions.MaxTaglineLength} characters.");
        }

        if (!BrandTheme.IsHexColor(options.AccentColor))
        {
            errors.Add("Branding accent colour must be written as #RRGGBB, for example #f7931a.");
        }

        if (!IsAcceptableLogo(options.LogoUrl))
        {
            errors.Add("Branding logo must be empty, an https:// URL, or a path starting with '/'.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsAcceptableLogo(string? logoUrl) =>
        string.IsNullOrWhiteSpace(logoUrl)
        || (logoUrl.StartsWith('/') && !logoUrl.StartsWith("//", StringComparison.Ordinal))
        || (Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
