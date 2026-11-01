using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DPV.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var databaseDirectory = Path.GetDirectoryName(connectionFactory.DatabasePath);
        if (!string.IsNullOrEmpty(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var journalMode = connection.CreateCommand())
        {
            journalMode.CommandText = "PRAGMA journal_mode = WAL;";
            var mode = Convert.ToString(
                await journalMode.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite did not enable the required WAL journal mode. Actual mode: {mode ?? "(null)"}.");
            }
        }

        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("SQLite database initialized at {DatabasePath}.", connectionFactory.DatabasePath);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
