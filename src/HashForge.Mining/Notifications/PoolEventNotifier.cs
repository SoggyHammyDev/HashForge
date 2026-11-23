using System.Collections.Concurrent;
using HashForge.Domain;
using HashForge.Mining.Network;
using HashForge.Mining.Shares;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Notifications;

/// <summary>Decides which pool events deserve a notification and queues them.</summary>
public sealed class PoolEventNotifier(
    INotificationOutbox outbox,
    INetworkStatsStore networkStats,
    IOptions<NotificationOptions> options) : IBestShareListener, IBlockObserver
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastBestShareSent = new(StringComparer.OrdinalIgnoreCase);

    public void OnPoolBestShare(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);

        if (!options.Value.NotifyBestShare || IsCoolingDown(share.PoolName, share.Timestamp))
        {
            return;
        }

        _lastBestShareSent[share.PoolName] = share.Timestamp;
        outbox.Enqueue(new BestShareNotification(
            share.PoolName,
            new BestShare(share.ActualDifficulty, share.Worker.FullName, share.Timestamp),
            networkStats.Current?.Difficulty ?? 0));
    }

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        if (options.Value.NotifyBlockFound)
        {
            outbox.Enqueue(new BlockFoundNotification(block));
        }

        return Task.CompletedTask;
    }

    private bool IsCoolingDown(string poolName, DateTimeOffset now) =>
        _lastBestShareSent.TryGetValue(poolName, out var lastSent)
        && now - lastSent < TimeSpan.FromSeconds(options.Value.BestShareCooldownSeconds);
}
