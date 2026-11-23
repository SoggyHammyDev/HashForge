using System.Net;
using Microsoft.Extensions.Options;

namespace HashForge.Web;

public sealed class AdminOptionsValidator : IValidateOptions<AdminOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var invalid = options.EffectiveNetworks
            .Where(network => !IPNetwork.TryParse(network, out _))
            .Select(network => $"Admin network '{network}' is not a valid CIDR range (for example 192.168.0.0/24).")
            .ToList();
        return invalid.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(invalid);
    }
}
