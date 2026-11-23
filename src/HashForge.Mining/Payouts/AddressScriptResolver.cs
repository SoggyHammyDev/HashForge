using System.Collections.Concurrent;
using HashForge.Bitcoin.Rpc;

namespace HashForge.Mining.Payouts;

/// <summary>Asks the node to verify addresses and remembers the valid ones.</summary>
public sealed class AddressScriptResolver(IAddressValidator validator) : IAddressScriptResolver
{
    private readonly ConcurrentDictionary<string, AddressResolution> _valid = new(StringComparer.Ordinal);

    public async Task<AddressResolution> ResolveAsync(string address, CancellationToken cancellationToken)
    {
        if (_valid.TryGetValue(address, out var cached))
        {
            return cached;
        }

        var validation = await validator.ValidateAddressAsync(address, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || string.IsNullOrEmpty(validation.ScriptPubKey))
        {
            return new AddressResolution(false, null, validation.Error ?? AddressResolution.DefaultError);
        }

        var resolution = new AddressResolution(true, Convert.FromHexString(validation.ScriptPubKey), null);
        _valid[address] = resolution;
        return resolution;
    }
}
