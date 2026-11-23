using HashForge.Mining.History;
using Microsoft.EntityFrameworkCore;

namespace HashForge.Persistence.Database;

public sealed class EfPoolHistoryRepository(IDbContextFactory<HashForgeDbContext> contextFactory) : IPoolHistoryRepository
{
    public async Task AddAsync(IReadOnlyList<PoolHistorySample> samples, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            context.PoolHistory.AddRange(samples.Select(s => new PoolHistoryEntity
            {
                PoolName = s.PoolName,
                Timestamp = s.Timestamp,
                Hashrate = s.Hashrate,
                Miners = s.Miners,
                Workers = s.Workers,
            }));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<PoolHistorySample>> GetSinceAsync(string poolName, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            return await context.PoolHistory
                .AsNoTracking()
                .Where(s => s.PoolName == poolName && s.Timestamp >= since)
                .OrderBy(s => s.Timestamp)
                .Select(s => new PoolHistorySample(s.PoolName, s.Timestamp, s.Hashrate, s.Miners, s.Workers))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
