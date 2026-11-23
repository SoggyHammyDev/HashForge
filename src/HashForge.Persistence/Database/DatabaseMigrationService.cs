using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Persistence.Database;

/// <summary>Brings the database schema up to date before anything else starts.</summary>
public sealed partial class DatabaseMigrationService(IDatabaseMigrator migrator, ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await migrator.MigrateAsync(cancellationToken).ConfigureAwait(false);
        LogMigrated();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date")]
    private partial void LogMigrated();
}
