using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class MigrationRunner
{
    public const int CurrentSchemaVersion = 6;
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
        await EnsureMigrationTableAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (currentVersion < 1)
        {
            await InsertVersionAsync(connection, transaction, 1, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 2)
        {
            await CreateCatalogAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 2, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 3)
        {
            await CreateFullTextIndexAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 3, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 4)
        {
            await CreateMetadataTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 4, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 5)
        {
            await CreateDuplicateSuppressionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 5, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 6)
        {
            await CreateThumbnailCacheAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 6, cancellationToken).ConfigureAwait(false);
        }

        await SetUserVersionAsync(connection, transaction, CurrentSchemaVersion, cancellationToken)
            .ConfigureAwait(false);
        transaction.Commit();
    }

    private static async Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {MigrationTableName} (
                Version INTEGER NOT NULL PRIMARY KEY,
                AppliedAtUtc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateCatalogAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Models (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                ScanRoot TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ModelFiles (
                ModelId TEXT NOT NULL PRIMARY KEY,
                NormalizedPath TEXT NOT NULL UNIQUE,
                ContentHash TEXT NOT NULL,
                FileSizeBytes INTEGER NOT NULL,
                ModifiedAtUtc TEXT NOT NULL,
                IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
                FOREIGN KEY (ModelId) REFERENCES Models(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_ModelFiles_ContentHash ON ModelFiles(ContentHash);

            CREATE TABLE IF NOT EXISTS ImportErrors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanRoot TEXT NOT NULL,
                Code TEXT NOT NULL,
                Path TEXT NULL,
                Message TEXT NOT NULL,
                RecordedAtUtc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateFullTextIndexAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE VIRTUAL TABLE IF NOT EXISTS Models_Fts USING fts5(
                model_id UNINDEXED,
                name,
                file_path,
                tags,
                tokenize = 'trigram'
            );

            CREATE TRIGGER IF NOT EXISTS model_files_ai AFTER INSERT ON ModelFiles BEGIN
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT new.rowid, new.ModelId, COALESCE(m.Name, ''), new.NormalizedPath, ''
                FROM Models m
                WHERE m.Id = new.ModelId;
            END;

            CREATE TRIGGER IF NOT EXISTS model_files_ad AFTER DELETE ON ModelFiles BEGIN
                DELETE FROM Models_Fts WHERE rowid = old.rowid;
            END;

            CREATE TRIGGER IF NOT EXISTS model_files_au AFTER UPDATE ON ModelFiles BEGIN
                DELETE FROM Models_Fts WHERE rowid = old.rowid;
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT new.rowid, new.ModelId, COALESCE(m.Name, ''), new.NormalizedPath, ''
                FROM Models m
                WHERE m.Id = new.ModelId;
            END;

            CREATE TRIGGER IF NOT EXISTS models_name_au AFTER UPDATE OF Name ON Models BEGIN
                DELETE FROM Models_Fts
                WHERE rowid IN (SELECT rowid FROM ModelFiles WHERE ModelId = new.Id);
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT f.rowid, f.ModelId, COALESCE(new.Name, ''), f.NormalizedPath, ''
                FROM ModelFiles f
                WHERE f.ModelId = new.Id;
            END;

            INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
            SELECT f.rowid, f.ModelId, COALESCE(m.Name, ''), f.NormalizedPath, ''
            FROM ModelFiles f
            INNER JOIN Models m ON m.Id = f.ModelId
            WHERE NOT EXISTS (
                SELECT 1 FROM Models_Fts existing WHERE existing.rowid = f.rowid
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateMetadataTablesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Artists (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS Collections (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS Tags (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS ModelTags (
                ModelId TEXT NOT NULL,
                TagId TEXT NOT NULL,
                PRIMARY KEY (ModelId, TagId),
                FOREIGN KEY (ModelId) REFERENCES Models(Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
            );

            ALTER TABLE Models ADD COLUMN ArtistId TEXT REFERENCES Artists(Id);
            ALTER TABLE Models ADD COLUMN CollectionId TEXT REFERENCES Collections(Id);
            ALTER TABLE Models ADD COLUMN CreatedAt TEXT;

            UPDATE Models
            SET CreatedAt = COALESCE(
                (SELECT MIN(ModifiedAtUtc) FROM ModelFiles WHERE ModelFiles.ModelId = Models.Id),
                strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
            WHERE CreatedAt IS NULL OR CreatedAt = '';
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateDuplicateSuppressionsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DuplicateSuppressions (
                ContentHash TEXT PRIMARY KEY,
                SuppressedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ModelFiles_ActiveContentHash
                ON ModelFiles(ContentHash)
                WHERE IsActive = 1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateThumbnailCacheAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ThumbnailCache (
                ContentHash TEXT PRIMARY KEY,
                CacheStatus TEXT NOT NULL,
                GeneratedAt TEXT
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {MigrationTableName} (Version, AppliedAtUtc)
            SELECT $version, $appliedAtUtc
            WHERE NOT EXISTS (
                SELECT 1 FROM {MigrationTableName} WHERE Version = $version
            );
            """;
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue(
            "$appliedAtUtc",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SetUserVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {version};";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
