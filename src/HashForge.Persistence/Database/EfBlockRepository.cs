using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Shares;
using Microsoft.EntityFrameworkCore;

namespace HashForge.Persistence.Database;

public sealed class EfBlockRepository(IDbContextFactory<HashForgeDbContext> contextFactory) : IBlockRepository
{
    public async Task AddAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);

        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            context.Blocks.Add(ToEntity(block));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<FoundBlock>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
        GetRecentAsync(null, count, cancellationToken);

    public Task<IReadOnlyList<FoundBlock>> GetRecentForPoolAsync(string poolName, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(poolName);
        return GetRecentAsync(poolName, count, cancellationToken);
    }

    private async Task<IReadOnlyList<FoundBlock>> GetRecentAsync(string? poolName, int count, CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            var entities = await context.Blocks
                .AsNoTracking()
                .Include(b => b.Payouts)
                .Where(b => poolName == null || b.PoolName == poolName)
                .OrderByDescending(b => b.FoundAt)
                .ThenByDescending(b => b.Id)
                .Take(count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. entities.Select(ToModel)];
        }
    }

    public async Task<FoundBlock?> FindAsync(string hash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            var entity = await context.Blocks
                .AsNoTracking()
                .Include(b => b.Payouts)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync(b => b.Hash == hash, cancellationToken)
                .ConfigureAwait(false);
            return entity is null ? null : ToModel(entity);
        }
    }

    private static BlockEntity ToEntity(FoundBlock block) => new()
    {
        PoolName = block.PoolName,
        Scheme = block.Scheme,
        Height = block.Height,
        Hash = block.Hash,
        WorkerAddress = block.Worker.Address,
        WorkerName = block.Worker.WorkerName,
        RewardSatoshis = block.RewardSatoshis,
        EffortPercent = block.EffortPercent,
        FoundAt = block.FoundAt,
        Accepted = block.Accepted,
        RejectReason = block.RejectReason,
        MinersPaid = block.MinersPaid,
        Payouts = [.. block.Payouts.Select(p => new BlockPayoutEntity { Address = p.Address, Satoshis = p.Satoshis })],
    };

    private static FoundBlock ToModel(BlockEntity entity) => new(
        entity.PoolName,
        entity.Scheme,
        entity.Height,
        entity.Hash,
        new WorkerIdentity(entity.WorkerAddress, entity.WorkerName),
        entity.RewardSatoshis,
        [.. entity.Payouts.OrderBy(p => p.Id).Select(p => new PayoutEntry(p.Address, p.Satoshis))],
        entity.EffortPercent,
        entity.FoundAt,
        entity.Accepted,
        entity.RejectReason)
    {
        MinersPaid = entity.MinersPaid,
    };
}
