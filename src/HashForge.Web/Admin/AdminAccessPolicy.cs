using System.Net;
using Microsoft.Extensions.Options;

namespace HashForge.Web.Admin;

/// <summary>Allows the admin area only from the configured network ranges.</summary>
public sealed class AdminAccessPolicy(IOptions<AdminOptions> options) : IAdminAccessPolicy
{
    private readonly IPNetwork[] _networks = [.. options.Value.EffectiveNetworks.Select(n => IPNetwork.Parse(n))];

    public bool IsAllowed(IPAddress? remoteAddress)
    {
        if (remoteAddress is null)
        {
            return false;
        }

        var address = remoteAddress.IsIPv4MappedToIPv6 ? remoteAddress.MapToIPv4() : remoteAddress;
        return _networks.Any(network => network.Contains(address));
    }
}
