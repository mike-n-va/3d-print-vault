using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class MigrationRunner
{
    public const int CurrentSchemaVersion = 10;
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

        if (currentVersion < 7)
        {
            await MigrateToModelFilesV7Async(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 7, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 8)
        {
            await MigrateToV8Async(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 8, cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 9)
        {
            await MigrateToV9Async(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 9, cancellationToken).ConfigureAwait(false);
        }

                if (currentVersion < 10)
        {
            await MigrateToV10Async(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertVersionAsync(connection, transaction, 10, cancellationToken).ConfigureAwait(false);
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

    private static async Task MigrateToModelFilesV7Async(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE Models ADD COLUMN HeroPreviewPath TEXT;
            ALTER TABLE Models ADD COLUMN ConfidenceScore REAL DEFAULT 1.0;

            CREATE TABLE IF NOT EXISTS ModelFiles_V7 (
                Id TEXT NOT NULL PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
                ModelId TEXT NOT NULL,
                NormalizedPath TEXT NOT NULL UNIQUE,
                ContentHash TEXT NOT NULL,
                FileSizeBytes INTEGER NOT NULL,
                ModifiedAtUtc TEXT NOT NULL,
                IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
                FileRole INTEGER NOT NULL DEFAULT 0,
                ConfidenceScore REAL NOT NULL DEFAULT 1.0,
                FOREIGN KEY (ModelId) REFERENCES Models(Id) ON DELETE CASCADE
            );

            INSERT OR IGNORE INTO ModelFiles_V7 (Id, ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive, FileRole, ConfidenceScore)
            SELECT ModelId, ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive, 0, 1.0
            FROM ModelFiles;

            DROP TRIGGER IF EXISTS models_name_au;
            DROP TRIGGER IF EXISTS model_files_ai;
            DROP TRIGGER IF EXISTS model_files_ad;
            DROP TRIGGER IF EXISTS model_files_au;

            DROP TABLE ModelFiles;
            ALTER TABLE ModelFiles_V7 RENAME TO ModelFiles;

            CREATE INDEX IF NOT EXISTS IX_ModelFiles_ModelId ON ModelFiles(ModelId);
            CREATE INDEX IF NOT EXISTS IX_ModelFiles_ContentHash ON ModelFiles(ContentHash);
            CREATE INDEX IF NOT EXISTS IX_ModelFiles_ActiveContentHash ON ModelFiles(ContentHash) WHERE IsActive = 1;

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
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MigrateToV8Async(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE Models ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Models ADD COLUMN Description TEXT;
            ALTER TABLE Models ADD COLUMN PrintNotes TEXT;

            CREATE INDEX IF NOT EXISTS IX_Models_IsFavorite ON Models(IsFavorite);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MigrateToV9Async(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Releases (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                ArtistId TEXT REFERENCES Artists(Id),
                CollectionId TEXT REFERENCES Collections(Id),
                Description TEXT,
                ReleaseDate TEXT,
                CoverImagePath TEXT,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UNIQUE(Name, ArtistId)
            );

            CREATE INDEX IF NOT EXISTS IX_Releases_ArtistId ON Releases(ArtistId);
            CREATE INDEX IF NOT EXISTS IX_Releases_IsFavorite ON Releases(IsFavorite);

            ALTER TABLE Models ADD COLUMN ReleaseId TEXT REFERENCES Releases(Id);
            CREATE INDEX IF NOT EXISTS IX_Models_ReleaseId ON Models(ReleaseId);

            ALTER TABLE Artists ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;
            CREATE INDEX IF NOT EXISTS IX_Artists_IsFavorite ON Artists(IsFavorite);

            ALTER TABLE Collections ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;
            CREATE INDEX IF NOT EXISTS IX_Collections_IsFavorite ON Collections(IsFavorite);

            INSERT OR IGNORE INTO Releases (Id, Name, ArtistId, CollectionId, Description, CreatedAt)
            SELECT c.Id, c.Name, (SELECT m.ArtistId FROM Models m WHERE m.CollectionId = c.Id AND m.ArtistId IS NOT NULL LIMIT 1), c.Id, c.Description, strftime('%Y-%m-%dT%H:%M:%SZ', 'now')
            FROM Collections c;

            UPDATE Models SET ReleaseId = CollectionId WHERE CollectionId IS NOT NULL AND ReleaseId IS NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    private static async Task MigrateToV10Async(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await AddColumnIfNotExistsAsync(connection, transaction, "Artists", "FolderPath", "TEXT", cancellationToken).ConfigureAwait(false);
        await AddColumnIfNotExistsAsync(connection, transaction, "Releases", "FolderPath", "TEXT", cancellationToken).ConfigureAwait(false);
        await AddColumnIfNotExistsAsync(connection, transaction, "Models", "Scale", "TEXT", cancellationToken).ConfigureAwait(false);
        await AddColumnIfNotExistsAsync(connection, transaction, "Models", "FolderPath", "TEXT", cancellationToken).ConfigureAwait(false);
        await AddColumnIfNotExistsAsync(connection, transaction, "Models", "ActiveImagePath", "TEXT", cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Files (
                Id TEXT NOT NULL PRIMARY KEY,
                ModelId TEXT NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                FileExtension TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                FOREIGN KEY (ModelId) REFERENCES Models(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Files_ModelId ON Files(ModelId);
            CREATE INDEX IF NOT EXISTS IX_Files_FilePath ON Files(FilePath);
            CREATE INDEX IF NOT EXISTS IX_Models_FolderPath ON Models(FolderPath);
            CREATE INDEX IF NOT EXISTS IX_Models_Scale ON Models(Scale);
            CREATE INDEX IF NOT EXISTS IX_Releases_FolderPath ON Releases(FolderPath);
            CREATE INDEX IF NOT EXISTS IX_Artists_FolderPath ON Artists(FolderPath);

            CREATE TABLE IF NOT EXISTS ScanState (
                FolderPath TEXT NOT NULL PRIMARY KEY,
                LastScannedUtc TEXT NOT NULL,
                DirectoryLastWriteUtc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddColumnIfNotExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var checkCmd = connection.CreateCommand();
        checkCmd.Transaction = transaction;
        checkCmd.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var exists = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        await reader.CloseAsync().ConfigureAwait(false);

        if (!exists)
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.Transaction = transaction;
            alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}