using HashForge.Domain;
using HashForge.Mining.Payouts;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Pools;

/// <summary>Starts and stops pools at runtime on behalf of an administrator.</summary>
public sealed partial class PoolProvisioner(
    IPoolRegistry registry,
    IPoolDefinitionStore store,
    IAddressScriptResolver addressResolver,
    ILogger<PoolProvisioner> logger) : IPoolProvisioner
{
    public async Task<ProvisionResult> AddAsync(PoolDefinition pool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var definitionError = PoolDefinitionValidator.FirstError(pool);
        if (definitionError is not null)
        {
            return ProvisionResult.Failure(definitionError);
        }

        var feeProblem = await addressResolver.FindProblemAsync(pool, cancellationToken).ConfigureAwait(false);
        if (feeProblem is not null)
        {
            return ProvisionResult.Failure($"Fee address rejected by the node: {feeProblem}");
        }

        if (!registry.TryAdd(pool, out var error))
        {
            return ProvisionResult.Failure(error);
        }

        await store.SaveAsync(pool, cancellationToken).ConfigureAwait(false);
        LogPoolAdded(pool.Name, pool.Scheme, pool.Port);
        return ProvisionResult.Success;
    }

    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        if (!registry.Remove(name))
        {
            return false;
        }

        await store.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
        LogPoolRemoved(name);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} ({Scheme}) started on port {Port} by an administrator")]
    private partial void LogPoolAdded(string pool, PayoutScheme scheme, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} stopped by an administrator")]
    private partial void LogPoolRemoved(string pool);
}
