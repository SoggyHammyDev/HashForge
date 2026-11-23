using HashForge.Domain;
using HashForge.Mining.Pools;
using Microsoft.EntityFrameworkCore;

namespace HashForge.Persistence.Database;

public sealed class EfPoolDefinitionStore(IDbContextFactory<HashForgeDbContext> contextFactory) : IPoolDefinitionStore
{
    public async Task<IReadOnlyList<PoolDefinition>> GetAllAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            var entities = await context.Pools.AsNoTracking().OrderBy(p => p.Port).ToListAsync(cancellationToken).ConfigureAwait(false);
            return [.. entities.Select(ToModel)];
        }
    }

    public async Task SaveAsync(PoolDefinition pool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            var entity = await context.Pools.FindAsync([pool.Name], cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                entity = new PoolDefinitionEntity { Name = pool.Name, FeeAddress = pool.FeeAddress };
                context.Pools.Add(entity);
            }

            entity.Scheme = pool.Scheme;
            entity.Port = pool.Port;
            entity.FeePercent = pool.FeePercent;
            entity.FeeAddress = pool.FeeAddress;
            entity.PplnsWindowMultiplier = pool.PplnsWindowMultiplier;
            entity.FinderBonusSatoshis = pool.FinderBonusSatoshis;
            entity.MaxCoinbaseOutputs = pool.MaxCoinbaseOutputs;
            entity.MinimumPayoutSatoshis = pool.MinimumPayoutSatoshis;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            await context.Pools.Where(p => p.Name == name).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static PoolDefinition ToModel(PoolDefinitionEntity entity) => new()
    {
        Name = entity.Name,
        Scheme = entity.Scheme,
        Port = entity.Port,
        FeePercent = entity.FeePercent,
        FeeAddress = entity.FeeAddress,
        PplnsWindowMultiplier = entity.PplnsWindowMultiplier,
        FinderBonusSatoshis = entity.FinderBonusSatoshis,
        MaxCoinbaseOutputs = entity.MaxCoinbaseOutputs,
        MinimumPayoutSatoshis = entity.MinimumPayoutSatoshis,
    };
}
