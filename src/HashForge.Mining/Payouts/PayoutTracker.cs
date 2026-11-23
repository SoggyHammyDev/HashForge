using System.Collections.Concurrent;
using System.Text.Json;
using HashForge.Domain;
using HashForge.Mining.Network;
using HashForge.Mining.Pools;
using HashForge.Mining.Shares;
using HashForge.Mining.State;

namespace HashForge.Mining.Payouts;

/// <summary>Feeds accepted shares into each pool's payout scheme and answers "who gets paid".</summary>
public sealed class PayoutTracker : IPayoutPlanSource, IShareObserver, IBlockObserver, IStatePersistable, IDisposable
{
    // Lazy guarantees each pool's scheme is created exactly once, even when many miners arrive together.
    private readonly ConcurrentDictionary<string, Lazy<IPayoutScheme>> _schemes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<ShareBucket>> _pendingState = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPoolRegistry _registry;
    private readonly IPayoutSchemeFactory _factory;
    private readonly INetworkStatsStore _networkStats;

    public PayoutTracker(IPoolRegistry registry, IPayoutSchemeFactory factory, INetworkStatsStore networkStats)
    {
        _registry = registry;
        _factory = factory;
        _networkStats = networkStats;
        _registry.PoolRemoved += OnPoolRemoved;
    }

    public string StateKey => "payouts";

    public void Dispose() => _registry.PoolRemoved -= OnPoolRemoved;

    public PayoutPlan GetPlan(PoolDefinition pool, string minerAddress)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return SchemeFor(pool).GetPlan(minerAddress);
    }

    public bool IsMinerSpecific(PoolDefinition pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return SchemeFor(pool).IsMinerSpecific;
    }

    public void OnShare(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);
        if (share.IsAccepted && _registry.Find(share.PoolName) is { } pool)
        {
            SchemeFor(pool).RecordShare(share.Worker.Address, share.CreditedDifficulty, _networkStats.Current?.Difficulty ?? 0);
        }
    }

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (_registry.Find(block.PoolName) is { } pool)
        {
            SchemeFor(pool).OnBlockFound();
        }

        return Task.CompletedTask;
    }

    public string ExportState()
    {
        // State still waiting for its pool to start is carried forward so a snapshot never discards it.
        var state = new Dictionary<string, IReadOnlyList<ShareBucket>>(_pendingState, StringComparer.OrdinalIgnoreCase);
        foreach (var (poolName, scheme) in _schemes)
        {
            if (scheme.Value is IPersistentPayoutScheme persistent)
            {
                state[poolName] = persistent.ExportState();
            }
        }

        return JsonSerializer.Serialize(state);
    }

    /// <summary>Restored state is held until its pool starts, because pools are registered after state is loaded.</summary>
    public void ImportState(string state)
    {
        var restored = JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<ShareBucket>>>(state) ?? [];
        foreach (var (poolName, buckets) in restored)
        {
            if (_schemes.TryGetValue(poolName, out var scheme))
            {
                Restore(scheme.Value, buckets);
            }
            else
            {
                _pendingState[poolName] = buckets;
            }
        }
    }

    private static void Restore(IPayoutScheme scheme, IReadOnlyList<ShareBucket> buckets)
    {
        if (scheme is IPersistentPayoutScheme persistent)
        {
            persistent.ImportState(buckets);
        }
    }

    private void OnPoolRemoved(object? sender, PoolChangedEventArgs args) => _schemes.TryRemove(args.Pool.Name, out _);

    private IPayoutScheme SchemeFor(PoolDefinition pool) =>
        _schemes.GetOrAdd(pool.Name, _ => new Lazy<IPayoutScheme>(() => Create(pool))).Value;

    private IPayoutScheme Create(PoolDefinition pool)
    {
        var scheme = _factory.Create(pool);
        if (_pendingState.TryRemove(pool.Name, out var buckets))
        {
            Restore(scheme, buckets);
        }

        return scheme;
    }
}
