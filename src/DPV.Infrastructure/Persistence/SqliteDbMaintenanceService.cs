using Dapper;
using DPV.Domain.Contracts.Catalog;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class SqliteDbMaintenanceService(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IDbMaintenanceService
{
    public async Task VacuumDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("VACUUM;", cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task BackupDatabaseAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        var destination = PrepareDestination(destinationFilePath);
        await using var source = connectionFactory.CreateConnection();
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(source, cancellationToken).ConfigureAwait(false);
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        await target.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(target);
    }

    public async Task RestoreDatabaseAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        var sourcePath = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Backup database file not found.", sourcePath);
        }

        EnsureDistinctFromCatalog(sourcePath);
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = connectionFactory.CreateConnection();
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
    }

    private string PrepareDestination(string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);
        var destination = Path.GetFullPath(destinationFilePath);
        EnsureDistinctFromCatalog(destination);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return destination;
    }

    private void EnsureDistinctFromCatalog(string path)
    {
        var catalog = Path.GetFullPath(connectionFactory.DatabasePath);
        if (string.Equals(path, catalog, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, catalog + "-wal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, catalog + "-shm", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The catalog database cannot be copied onto itself.");
        }
    }
}
