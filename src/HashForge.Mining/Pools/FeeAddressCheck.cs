using HashForge.Domain;
using HashForge.Mining.Payouts;

namespace HashForge.Mining.Pools;

/// <summary>The one place that decides whether a pool's fee address is acceptable.</summary>
public static class FeeAddressCheck
{
    /// <summary>
    /// Returns why the node will not pay the pool's fee address, or null when there is no problem.
    /// A pool with no fee address has nothing to check; whether it may omit one is the validator's concern.
    /// </summary>
    public static async Task<string?> FindProblemAsync(
        this IAddressScriptResolver resolver, PoolDefinition pool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(pool);

        if (string.IsNullOrWhiteSpace(pool.FeeAddress))
        {
            return null;
        }

        var address = await resolver.ResolveAsync(pool.FeeAddress, cancellationToken).ConfigureAwait(false);
        return address.IsValid ? null : address.Error ?? AddressResolution.DefaultError;
    }
}