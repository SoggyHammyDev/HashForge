using System.Net;
using Microsoft.Extensions.Options;

namespace HashForge.Web;

public sealed class WebOptionsValidator : IValidateOptions<WebOptions>
{
    public ValidateOptionsResult Validate(string? name, WebOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var invalid = options.ConfiguredProxies
            .Where(proxy => !IPAddress.TryParse(proxy, out _))
            .Select(proxy => $"Trusted proxy '{proxy}' is not a valid IP address.")
            .ToList();
        return invalid.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(invalid);
    }
}
