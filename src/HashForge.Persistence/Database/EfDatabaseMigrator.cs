using Microsoft.EntityFrameworkCore;

namespace HashForge.Persistence.Database;

/// <summary>Applies any pending EF Core migrations.</summary>
public sealed class EfDatabaseMigrator(IDbContextFactory<HashForgeDbContext> contextFactory) : IDatabaseMigrator
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
