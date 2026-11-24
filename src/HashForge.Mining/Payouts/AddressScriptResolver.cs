using System.Collections.Concurrent;
using HashForge.Bitcoin.Rpc;

namespace HashForge.Mining.Payouts;

/// <summary>
/// Asks the node to verify addresses and remembers the valid ones. When a pool restarts, every miner
/// reconnects within seconds and each needs its address checked; asked all at once the node stops answering,
/// so an address is only ever looked up once at a time and only a few lookups run together.
/// </summary>
public sealed class AddressScriptResolver(IAddressValidator validator) : IAddressScriptResolver, IDisposable
{
    /// <summary>How many addresses the node is asked about at the same time; the rest wait their turn.</summary>
    public const int MaxConcurrentLookups = 4;

    private readonly ConcurrentDictionary<string, AddressResolution> _valid = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<AddressResolution>>> _inProgress = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _nodeSlots = new(MaxConcurrentLookups, MaxConcurrentLookups);

    public Task<AddressResolution> ResolveAsync(string address, CancellationToken cancellationToken)
    {
        if (_valid.TryGetValue(address, out var cached))
        {
            return Task.FromResult(cached);
        }

        // Everyone asking about the same address shares one question to the node. A caller that gives up
        // stops waiting, but the lookup carries on for the others.
        var lookup = _inProgress.GetOrAdd(address, key => new Lazy<Task<AddressResolution>>(() => LookUpAsync(key)));
        return lookup.Value.WaitAsync(cancellationToken);
    }

    public void Dispose() => _nodeSlots.Dispose();

    private async Task<AddressResolution> LookUpAsync(string address)
    {
        try
        {
            var validation = await AskNodeAsync(address).ConfigureAwait(false);
            if (!validation.IsValid || string.IsNullOrEmpty(validation.ScriptPubKey))
            {
                return new AddressResolution(false, null, validation.Error ?? AddressResolution.DefaultError);
            }

            var resolution = new AddressResolution(true, Convert.FromHexString(validation.ScriptPubKey), null);
            _valid[address] = resolution;
            return resolution;
        }
        finally
        {
            // Only valid answers are remembered, so a failed or refused lookup is asked afresh next time.
            _inProgress.TryRemove(address, out _);
        }
    }

    private async Task<AddressValidation> AskNodeAsync(string address)
    {
        await _nodeSlots.WaitAsync().ConfigureAwait(false);
        try
        {
            return await validator.ValidateAddressAsync(address, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _nodeSlots.Release();
        }
    }
}
