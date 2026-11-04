using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class ExportAndMaintenanceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"dpv-export-{Guid.NewGuid():N}.db");
    private readonly string _backupPath = Path.Combine(Path.GetTempPath(), $"dpv-backup-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExportToCsvAndJson_ReturnsFormattedActiveRows()
    {
        var token = TestContext.Current.CancellationToken;
        await InsertActiveAndInactiveAsync(token);
        var export = CreateExport();

        var csv = await export.ExportToCsvAsync(token);
        var json = await export.ExportToJsonAsync(token);

        Assert.StartsWith("ModelName,FilePath,FileSizeBytes,Artist,Collection,LastModified", csv, StringComparison.Ordinal);
        Assert.Contains("\"Dragon \"\"Statue\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"C:/Models/dragon.stl\"", csv, StringComparison.Ordinal);
        Assert.Contains("10240", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("old.stl", csv, StringComparison.Ordinal);
        Assert.Contains("Dragon \\\"Statue\\\"", json, StringComparison.Ordinal);
        Assert.Contains("hash_abc", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hash_old", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackupAndRestore_RoundTripTheCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await InsertActiveAndInactiveAsync(token);
        var maintenance = CreateMaintenance();

        await maintenance.VacuumDatabaseAsync(token);
        await maintenance.BackupDatabaseAsync(_backupPath, token);

        Assert.True(new FileInfo(_backupPath).Length > 0);
        await InsertExtraModelAsync(token);
        var beforeRestore = await CreateExport().ExportToJsonAsync(token);
        Assert.Contains("Second Model", beforeRestore, StringComparison.Ordinal);

        await maintenance.RestoreDatabaseAsync(_backupPath, token);
        var restored = await CreateExport().ExportToJsonAsync(token);
        Assert.Contains("hash_abc", restored, StringComparison.Ordinal);
        Assert.DoesNotContain("Second Model", restored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreDatabase_RejectsAMissingBackup()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"dpv-missing-{Guid.NewGuid():N}.db");

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            CreateMaintenance().RestoreDatabaseAsync(missing, TestContext.Current.CancellationToken));

        Assert.Equal(missing, exception.FileName);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[]
        {
            _databasePath,
            _databasePath + "-wal",
            _databasePath + "-shm",
            _backupPath,
            _backupPath + "-wal",
            _backupPath + "-shm"
        })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private CatalogExportService CreateExport() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner());

    private SqliteDbMaintenanceService CreateMaintenance() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner());

    private async Task InsertActiveAndInactiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await new MigrationRunner().MigrateAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Models (Id, Name, ScanRoot, CreatedAt)
            VALUES ('mod_1', 'Dragon "Statue"', 'C:/Models', '2026-11-04T00:00:00Z');
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ('mod_1', 'C:/Models/dragon.stl', 'hash_abc', 10240, '2026-11-04T01:00:00Z', 1);
            INSERT INTO Models (Id, Name, ScanRoot, CreatedAt)
            VALUES ('mod_old', 'Old Dragon', 'C:/Models', '2026-11-04T00:00:00Z');
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ('mod_old', 'C:/Models/old.stl', 'hash_old', 10, '2026-11-04T01:00:00Z', 0);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertExtraModelAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Models (Id, Name, ScanRoot, CreatedAt)
            VALUES ('mod_2', 'Second Model', 'C:/Models', '2026-11-04T00:00:00Z');
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ('mod_2', 'C:/Models/second.stl', 'hash_second', 20, '2026-11-04T01:00:00Z', 1);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
