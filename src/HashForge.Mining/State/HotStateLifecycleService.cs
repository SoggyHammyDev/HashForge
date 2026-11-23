using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.State;

/// <summary>Restores state before mining starts and takes a final snapshot on shutdown.</summary>
public sealed partial class HotStateLifecycleService(
    IHotStateCoordinator coordinator,
    ILogger<HotStateLifecycleService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        RunSafelyAsync(coordinator.RestoreAllAsync, "restore", cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        RunSafelyAsync(coordinator.SaveAllAsync, "save", cancellationToken);

    private async Task RunSafelyAsync(Func<CancellationToken, Task> operation, string description, CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The pool must still start and stop cleanly when the state store is unavailable.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogStateFailure(exception, description);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not {Operation} pool state; continuing without it")]
    private partial void LogStateFailure(Exception exception, string operation);
}
