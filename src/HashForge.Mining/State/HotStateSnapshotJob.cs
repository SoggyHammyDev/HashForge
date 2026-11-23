using HashForge.Mining.Hosting;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.State;

/// <summary>Background job that snapshots in-memory state in batches; shares are never written one by one.</summary>
public sealed class HotStateSnapshotJob(IHotStateCoordinator coordinator, IOptions<MiningOptions> options) : IPeriodicJob
{
    public string Name => "state-snapshot";

    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(options.Value.StateSnapshotSeconds);

    public Task ExecuteAsync(CancellationToken cancellationToken) => coordinator.SaveAllAsync(cancellationToken);
}
