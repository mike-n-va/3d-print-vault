using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class MigrationRunner
{
    public const int CurrentSchemaVersion = 1;
    private const string MigrationTableName = "DPVSchemaMigrations";

    public async Task MigrateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var currentVersion = await ReadSchemaVersionAsync(connection, cancellationToken)
            .ConfigureAwait(false);
        if (currentVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {currentVersion} is newer than the supported version "
                + $"{CurrentSchemaVersion}.");
        }

        if (currentVersion == CurrentSchemaVersion)
        {
            await VerifyMigrationRecordAsync(connection, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var transaction = connection.BeginTransaction();

        await using (var createTable = connection.CreateCommand())
        {
            createTable.Transaction = transaction;
            createTable.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {MigrationTableName} (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    AppliedAtUtc TEXT NOT NULL
                );
                """;
            await createTable.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var addBaseline = connection.CreateCommand())
        {
            addBaseline.Transaction = transaction;
            addBaseline.CommandText = $"""
                INSERT INTO {MigrationTableName} (Version, AppliedAtUtc)
                VALUES ($version, $appliedAtUtc);
                PRAGMA user_version = {CurrentSchemaVersion};
                """;
            addBaseline.Parameters.AddWithValue("$version", CurrentSchemaVersion);
            addBaseline.Parameters.AddWithValue(
                "$appliedAtUtc",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await addBaseline.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
    }

    private static async Task<int> ReadSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task VerifyMigrationRecordAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)
            FROM {MigrationTableName}
            WHERE Version = $version;
            """;
        command.Parameters.AddWithValue("$version", CurrentSchemaVersion);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (Convert.ToInt32(result, CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException(
                $"Database schema version {CurrentSchemaVersion} has no matching migration record.");
        }
    }
}
