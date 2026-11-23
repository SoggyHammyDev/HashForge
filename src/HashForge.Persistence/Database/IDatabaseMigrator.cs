namespace HashForge.Persistence.Database;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
