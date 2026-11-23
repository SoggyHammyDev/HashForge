using HashForge.Mining.State;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HashForge.Persistence.Redis;

/// <summary>Keeps batched snapshots of the pool's in-memory state in Redis.</summary>
public sealed class RedisHotStateStore(IConnectionMultiplexer redis, IOptions<PersistenceOptions> options) : IHotStateStore
{
    private readonly string _prefix = options.Value.RedisKeyPrefix;

    public Task SaveAsync(string key, string state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return redis.GetDatabase().StringSetAsync(_prefix + key, state);
    }

    public async Task<string?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase().StringGetAsync(_prefix + key).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }
}
