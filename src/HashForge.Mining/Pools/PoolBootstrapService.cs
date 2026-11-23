using HashForge.Domain;
using HashForge.Mining.Payouts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Pools;

/// <summary>Registers the configured pools, then any pools an administrator created earlier.</summary>
public sealed partial class PoolBootstrapService(
    IPoolRegistry registry,
    IPoolDefinitionStore store,
    IAddressScriptResolver addressResolver,
    IOptions<MiningOptions> options,
    ILogger<PoolBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RegisterAsync(options.Value.Pools, cancellationToken).ConfigureAwait(false);
        await RegisterAsync(await store.GetAllAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RegisterAsync(IEnumerable<PoolDefinition> pools, CancellationToken cancellationToken)
    {
        foreach (var pool in pools)
        {
            if (!pool.Enabled)
            {
                LogPoolDisabled(pool.Name);
                continue;
            }

            // A fee address the node will not pay to would break every job, so the pool is refused up front.
            var feeAddress = await addressResolver.ResolveAsync(pool.FeeAddress, cancellationToken).ConfigureAwait(false);
            if (!feeAddress.IsValid)
            {
                LogPoolRefused(pool.Name, pool.FeeAddress, feeAddress.Error);
            }
            else if (registry.TryAdd(pool, out var error))
            {
                LogPoolRegistered(pool.Name, pool.Scheme, pool.Port);
            }
            else
            {
                LogPoolSkipped(pool.Name, error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} ({Scheme}) registered on port {Port}")]
    private partial void LogPoolRegistered(string pool, PayoutScheme scheme, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} is disabled and was not started")]
    private partial void LogPoolDisabled(string pool);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pool {Pool} was NOT started: the node rejected its fee address '{FeeAddress}': {Reason}")]
    private partial void LogPoolRefused(string pool, string feeAddress, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pool {Pool} was not started: {Reason}")]
    private partial void LogPoolSkipped(string pool, string reason);
}
