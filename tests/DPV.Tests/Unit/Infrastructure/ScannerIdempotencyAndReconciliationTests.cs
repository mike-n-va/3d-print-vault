using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DPV.Tests.Unit.Infrastructure;

/// <summary>
/// Sprint A regression suite: scan idempotency (schema v11 natural keys),
/// end-of-scan reconciliation, real content hashing, and the v10 -> v11
/// legacy-row retirement migration.
/// </summary>
public sealed class ScannerIdempotencyAndReconciliationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _libraryRoot;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly DirectoryScannerService _scanner;

    public ScannerIdempotencyAndReconciliationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "dpv-sprint-a-" + Guid.NewGuid().ToString("N"));
        _libraryRoot = Path.Combine(_testRoot, "library");
        _databasePath = Path.Combine(_testRoot, "test.db");
        Directory.CreateDirectory(_libraryRoot);

        _connectionFactory = new SqliteConnectionFactory(_databasePath);
        _migrationRunner = new MigrationRunner();

        using var conn = _connectionFactory.CreateConnection();
        conn.Open();
        _migrationRunner.MigrateAsync(conn).GetAwaiter().GetResult();

        _scanner = new DirectoryScannerService(_connectionFactory, _migrationRunner);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
        }
    }

    // ---------------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Rescan_SameLibrary_DoesNotDuplicateAnyTier()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildXalatathFixtureAsync(ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var first = await SnapshotCountsAsync(ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var third = await SnapshotCountsAsync(ct);

        Assert.Equal(first, third);
        Assert.Equal(1, third.ActiveArtists);
        Assert.Equal(1, third.ActiveReleases);
        Assert.Equal(2, third.ActiveModels);
        Assert.Equal(4, third.ActiveFiles);
    }

    [Fact]
    public async Task Rescan_PreservesModelIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildXalatathFixtureAsync(ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var idsBefore = await GetActiveModelIdsAsync(ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var idsAfter = await GetActiveModelIdsAsync(ct);

        Assert.Equal(idsBefore.OrderBy(x => x), idsAfter.OrderBy(x => x));
    }

    [Fact]
    public async Task Rescan_PreservesUserMetadataOnModels()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildXalatathFixtureAsync(ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        await using (var conn = _connectionFactory.CreateConnection())
        {
            await conn.OpenAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Models SET IsFavorite = 1, PrintNotes = 'printed in PETG' WHERE Name LIKE '%1-6%';",
                cancellationToken: ct));
        }

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        await using var verify = _connectionFactory.CreateConnection();
        await verify.OpenAsync(ct);
        var row = await verify.QuerySingleAsync<(long IsFavorite, string PrintNotes)>(new CommandDefinition(
            "SELECT IsFavorite, PrintNotes FROM Models WHERE Name LIKE '%1-6%' AND IsActive = 1;",
            cancellationToken: ct));

        Assert.Equal(1, row.IsFavorite);
        Assert.Equal("printed in PETG", row.PrintNotes);
    }

    [Fact]
    public async Task Rescan_ThroughCatalogQuery_ReturnsSameModelCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildXalatathFixtureAsync(ct);
        var query = new DapperCatalogQuery(_connectionFactory, _migrationRunner);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var first = await query.SearchModelsAsync(new ModelQueryParameters(), ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var third = await query.SearchModelsAsync(new ModelQueryParameters(), ct);

        Assert.Equal(first.Count, third.Count);
        Assert.Equal(2, third.Count);
    }

    // ---------------------------------------------------------------------
    // Reconciliation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reconcile_DeletedFile_RetiresFileButKeepsModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var modelDir = await BuildSingleModelAsync("Artist", "Release", "Model", ["a.stl", "b.stl"], ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        File.Delete(Path.Combine(modelDir, "b.stl"));
        var result = await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(1, result.RetiredFiles);
        Assert.Equal(0, result.RetiredModels);
        Assert.Equal(1, counts.ActiveModels);
        Assert.Equal(1, counts.ActiveFiles);
    }

    [Fact]
    public async Task Reconcile_DeletedModelFolder_RetiresModelAndCascadesUpwardWhenEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var modelDir = await BuildSingleModelAsync("Artist", "Release", "Model", ["a.stl"], ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        Directory.Delete(Path.Combine(_libraryRoot, "Artist", "Release"), recursive: true);
        Assert.False(Directory.Exists(modelDir));

        var result = await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(1, result.RetiredFiles);
        Assert.Equal(1, result.RetiredModels);
        Assert.Equal(1, result.RetiredReleases);
        Assert.Equal(1, result.RetiredArtists);
        Assert.Equal(0, counts.ActiveFiles);
        Assert.Equal(0, counts.ActiveModels);
        Assert.Equal(0, counts.ActiveReleases);
        Assert.Equal(0, counts.ActiveArtists);

        // Soft delete: rows still exist for audit / re-activation.
        Assert.Equal(1, counts.TotalModels);
    }

    [Fact]
    public async Task Reconcile_RemovedRelease_HidesCardFromReleaseQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildSingleModelAsync("Artist", "Keeper", "Model", ["k.stl"], ct);
        await BuildSingleModelAsync("Artist", "Goner", "Model", ["g.stl"], ct);
        var repo = new DapperReleaseRepository(_connectionFactory, _migrationRunner);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        Assert.Equal(2, (await repo.GetReleasesAsync(new ReleaseQueryParameters(), ct)).Count);

        Directory.Delete(Path.Combine(_libraryRoot, "Artist", "Goner"), recursive: true);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var releases = await repo.GetReleasesAsync(new ReleaseQueryParameters(), ct);
        var single = Assert.Single(releases);
        Assert.Equal("Keeper", single.Name);
    }

    [Fact]
    public async Task Reconcile_RestoredFolder_ReactivatesSameRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildSingleModelAsync("Artist", "Release", "Model", ["a.stl"], ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var originalIds = await GetActiveModelIdsAsync(ct);

        var releaseDir = Path.Combine(_libraryRoot, "Artist", "Release");
        var backup = Path.Combine(_testRoot, "backup");
        Directory.Move(releaseDir, backup);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        Assert.Equal(0, (await SnapshotCountsAsync(ct)).ActiveModels);

        Directory.Move(backup, releaseDir);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(1, counts.ActiveModels);
        Assert.Equal(1, counts.TotalModels); // no new row; the retired one was reactivated
        Assert.Equal(originalIds, await GetActiveModelIdsAsync(ct));
    }

    [Fact]
    public async Task Reconcile_IsScopedToScanRoot_OtherRootsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootA = Path.Combine(_testRoot, "rootA");
        var rootB = Path.Combine(_testRoot, "rootB");
        await BuildModelUnderAsync(rootA, "ArtistA", "ReleaseA", "Model", ["a.stl"], ct);
        await BuildModelUnderAsync(rootB, "ArtistB", "ReleaseB", "Model", ["b.stl"], ct);

        await _scanner.BulkScanAsync(rootA, cancellationToken: ct);
        await _scanner.BulkScanAsync(rootB, cancellationToken: ct);

        // Re-scanning root A must not retire anything that lives under root B.
        var result = await _scanner.BulkScanAsync(rootA, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(0, result.RetiredFiles + result.RetiredModels + result.RetiredReleases + result.RetiredArtists);
        Assert.Equal(2, counts.ActiveModels);
        Assert.Equal(2, counts.ActiveFiles);
    }

    [Fact]
    public async Task Reconcile_CancelledScan_DoesNotRetireUnvisitedRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuildSingleModelAsync("Artist", "Release", "Model", ["a.stl"], ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        // Cancel mid-scan: progress fires per artist before the release loop,
        // so the scan aborts without re-visiting rows, and reconciliation
        // (which would retire anything not re-visited) must be skipped.
        using var cts = new CancellationTokenSource();
        var result = await _scanner.BulkScanAsync(_libraryRoot, new CancellingProgress(cts), cts.Token);

        Assert.True(result.Success);
        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(0, result.RetiredModels);
        Assert.Equal(0, result.RetiredFiles);
        Assert.Equal(1, counts.ActiveModels);
        Assert.Equal(1, counts.ActiveFiles);
    }

    [Fact]
    public async Task Reconcile_DoesNotRetireSiblingFolderSharingNamePrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        // "lib" vs "lib-extra": a naive prefix check (StartsWith / substr
        // without a separator boundary) would treat the second as being under
        // the first. The file under lib-extra is deleted so its model WOULD be
        // retired by a scan of lib if scoping were broken.
        var rootShort = Path.Combine(_testRoot, "lib");
        var rootLong = Path.Combine(_testRoot, "lib-extra");
        await BuildModelUnderAsync(rootShort, "A", "R", "Model", ["s.stl"], ct);
        var longModel = await BuildModelUnderAsync(rootLong, "B", "R2", "Model", ["l.stl"], ct);

        await _scanner.BulkScanAsync(rootShort, cancellationToken: ct);
        await _scanner.BulkScanAsync(rootLong, cancellationToken: ct);
        Assert.Equal(2, (await SnapshotCountsAsync(ct)).ActiveModels);

        File.Delete(Path.Combine(longModel, "l.stl"));
        var result = await _scanner.BulkScanAsync(rootShort, cancellationToken: ct);

        // Reconciliation is scoped to the scan root: the deleted lib-extra file
        // and its model are outside "lib" and must not be retired by this scan.
        // (The stale file row is cleaned up by the next scan of lib-extra.)
        Assert.Equal(0, result.RetiredFiles);
        Assert.Equal(0, result.RetiredModels);
        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(2, counts.ActiveModels);
        Assert.Equal(2, counts.ActiveFiles);

        // Scanning lib-extra itself retires the stale row — scoped as expected.
        var longResult = await _scanner.BulkScanAsync(rootLong, cancellationToken: ct);
        Assert.Equal(1, longResult.RetiredFiles);
        Assert.Equal(1, longResult.RetiredModels);
        var after = await SnapshotCountsAsync(ct);
        Assert.Equal(1, after.ActiveModels);
        Assert.Equal(1, after.ActiveFiles);
    }

    private sealed class CancellingProgress(CancellationTokenSource cts) : IProgress<ScanProgressReport>
    {
        public void Report(ScanProgressReport value) => cts.Cancel();
    }

    // ---------------------------------------------------------------------
    // Content hash
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Scan_IdenticalContentAtDifferentPaths_ProducesSameContentHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var bytes = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        var dirA = Path.Combine(_libraryRoot, "Artist", "ReleaseA", "ModelA");
        var dirB = Path.Combine(_libraryRoot, "Artist", "ReleaseB", "ModelB");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        await File.WriteAllBytesAsync(Path.Combine(dirA, "same.stl"), bytes, ct);
        await File.WriteAllBytesAsync(Path.Combine(dirB, "copy.stl"), bytes, ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        var hashes = (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT ContentHash FROM Files WHERE IsActive = 1;", cancellationToken: ct))).ToList();

        Assert.Equal(2, hashes.Count);
        Assert.All(hashes, h => Assert.False(string.IsNullOrWhiteSpace(h)));
        Assert.Single(hashes.Distinct());
    }

    [Fact]
    public async Task Scan_DifferentContentSameSize_ProducesDifferentContentHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = Path.Combine(_libraryRoot, "Artist", "Release", "Model");
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, "one.stl"), [1, 2, 3], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir, "two.stl"), [3, 2, 1], ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        var hashes = (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT ContentHash FROM Files WHERE IsActive = 1;", cancellationToken: ct))).ToList();

        Assert.Equal(2, hashes.Distinct().Count());
    }

    [Fact]
    public async Task Scan_ModifiedFileContent_UpdatesContentHashOnRescan()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = await BuildSingleModelAsync("Artist", "Release", "Model", ["a.stl"], ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var before = await GetSingleFileHashAsync(ct);

        await File.WriteAllBytesAsync(Path.Combine(dir, "a.stl"), [42, 42, 42, 42], ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var after = await GetSingleFileHashAsync(ct);

        Assert.NotEqual(before, after);
    }

    // ---------------------------------------------------------------------
    // Mixed-state / failure isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Scan_MovedFileBetweenModels_RepointsFileWithoutDuplicating()
    {
        var ct = TestContext.Current.CancellationToken;
        var releaseDir = Path.Combine(_libraryRoot, "Artist", "Release");
        var dir16 = Path.Combine(releaseDir, "Figure 1-6 scale");
        var dir110 = Path.Combine(releaseDir, "Figure 1-10 scale");
        Directory.CreateDirectory(dir16);
        Directory.CreateDirectory(dir110);
        await File.WriteAllBytesAsync(Path.Combine(dir16, "torso.stl"), [1], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir110, "base.stl"), [2], ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        File.Move(Path.Combine(dir16, "torso.stl"), Path.Combine(dir110, "torso.stl"));
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(2, counts.ActiveFiles);
        // The 1-6 folder now has no geometry, so its model is retired.
        Assert.Equal(1, counts.ActiveModels);
    }

    // ---------------------------------------------------------------------
    // Migration v10 -> v11
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Migration_V11_AddsColumnsAndIndexes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        Assert.Equal(11, MigrationRunner.CurrentSchemaVersion);
        foreach (var (table, column) in new[]
                 {
                     ("Artists", "IsActive"), ("Releases", "IsActive"), ("Models", "IsActive"), ("Files", "IsActive"),
                     ("Models", "SourcePipeline"), ("Releases", "SourcePipeline"), ("Files", "ContentHash")
                 })
        {
            Assert.True(await ColumnExistsAsync(conn, table, column, ct), $"{table}.{column} missing");
        }

        var indexes = (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM sqlite_master WHERE type = 'index';", cancellationToken: ct))).ToList();
        Assert.Contains("UX_Models_FolderPath", indexes);
        Assert.Contains("IX_Files_ContentHash", indexes);
    }

    [Fact]
    public async Task Migration_V11_RetiresLegacyRowsAndDeduplicatesFolderPaths()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(_testRoot, "legacy.db");
        var factory = new SqliteConnectionFactory(path);

        await using (var conn = factory.CreateConnection())
        {
            await conn.OpenAsync(ct);
            // Build a database at schema v10 by applying all migrations then rolling back v11.
            await new MigrationRunner().MigrateAsync(conn, ct);
            await DowngradeToV10Async(conn, ct);

            // Legacy v1 rows: no FolderPath, no ReleaseId.
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO Models (Id, Name, ScanRoot, CreatedAt) VALUES
                    ('legacy-1', 'Torso', 'c:\lib', '2026-01-01T00:00:00Z'),
                    ('legacy-2', 'Cape',  'c:\lib', '2026-01-01T00:00:00Z');
                INSERT INTO Artists (Id, Name, FolderPath) VALUES ('a1', 'Artist', 'c:\lib\Artist');
                INSERT INTO Releases (Id, Name, ArtistId, FolderPath, CreatedAt)
                    VALUES ('r1', 'Release', 'a1', 'c:\lib\Artist\Release', '2026-01-01T00:00:00Z');
                -- Three duplicate v2 rows for one folder (the per-scan GUID bug).
                INSERT INTO Models (Id, Name, ScanRoot, ArtistId, ReleaseId, FolderPath, CreatedAt) VALUES
                    ('dup-old',  'Model', 'c:\lib', 'a1', 'r1', 'c:\lib\Artist\Release\Model', '2026-01-01T00:00:00Z'),
                    ('dup-mid',  'Model', 'c:\lib', 'a1', 'r1', 'c:\lib\Artist\Release\Model', '2026-02-01T00:00:00Z'),
                    ('dup-new',  'Model', 'c:\lib', 'a1', 'r1', 'c:\lib\Artist\Release\Model', '2026-03-01T00:00:00Z');
                """,
                cancellationToken: ct));
        }

        await using (var conn = factory.CreateConnection())
        {
            await conn.OpenAsync(ct);
            await new MigrationRunner().MigrateAsync(conn, ct); // applies v11
        }

        await using var verify = factory.CreateConnection();
        await verify.OpenAsync(ct);

        var legacyActive = await verify.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM Models WHERE Id IN ('legacy-1','legacy-2') AND IsActive = 1;", cancellationToken: ct));
        Assert.Equal(0, legacyActive);

        var legacyLineage = await verify.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM Models WHERE Id IN ('legacy-1','legacy-2') AND SourcePipeline = 'legacy';", cancellationToken: ct));
        Assert.Equal(2, legacyLineage);

        var activeDupes = (await verify.QueryAsync<string>(new CommandDefinition(
            "SELECT Id FROM Models WHERE FolderPath = 'c:\\lib\\Artist\\Release\\Model' AND IsActive = 1;",
            cancellationToken: ct))).ToList();
        Assert.Equal(["dup-new"], activeDupes); // newest row survives

        // Release/Artist rows with a FolderPath stay active.
        Assert.Equal(1, await verify.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT IsActive FROM Releases WHERE Id = 'r1';", cancellationToken: ct)));
    }

    [Fact]
    public async Task Migration_V11_EnforcesUniqueActiveFolderPath()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Models (Id, Name, ScanRoot, FolderPath, CreatedAt) VALUES ('m1','M','r','f','t');",
            cancellationToken: ct));

        await Assert.ThrowsAsync<SqliteException>(() => conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Models (Id, Name, ScanRoot, FolderPath, CreatedAt) VALUES ('m2','M','r','f','t');",
            cancellationToken: ct)));

        // A retired row for the same path is allowed (partial index only covers active rows).
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Models SET IsActive = 0 WHERE Id = 'm1';", cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Models (Id, Name, ScanRoot, FolderPath, CreatedAt) VALUES ('m2','M','r','f','t');",
            cancellationToken: ct));
    }

    [Fact]
    public async Task Scan_MixedLayoutRelease_IngestsLooseGeometryAsDistinctModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var releaseDir = Path.Combine(_libraryRoot, "Luftmensch", "Xalatath");
        var dir16 = Path.Combine(releaseDir, "Xalatath 1-6 scale");
        var dir110 = Path.Combine(releaseDir, "Xalatath 1-10 scale");
        Directory.CreateDirectory(dir16);
        Directory.CreateDirectory(dir110);
        await File.WriteAllBytesAsync(Path.Combine(dir16, "Torso.stl"), [1, 1], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir16, "Cape.stl"), [1, 2], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir110, "Torso.stl"), [2, 1], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir110, "Cape.stl"), [2, 2], ct);
        // Standalone loose geometry file sitting directly in releaseDir root next to scale subdirectories
        await File.WriteAllBytesAsync(Path.Combine(releaseDir, "Base.stl"), [3, 1], ct);

        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var counts = await SnapshotCountsAsync(ct);
        Assert.Equal(1, counts.ActiveArtists);
        Assert.Equal(1, counts.ActiveReleases);
        Assert.Equal(3, counts.ActiveModels); // 2 subfolders + 1 root model
        Assert.Equal(5, counts.ActiveFiles);  // 4 in subfolders + 1 in root

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        var models = (await conn.QueryAsync<(string Id, string Name, string FolderPath)>(
            new CommandDefinition("SELECT Id, Name, FolderPath FROM Models WHERE IsActive = 1 ORDER BY Name;", cancellationToken: ct))).ToList();

        Assert.Equal(3, models.Count);
        var rootModel = models.Single(m => m.FolderPath == Path.GetFullPath(releaseDir));
        Assert.Equal("Xalatath", rootModel.Name);

        var rootFiles = (await conn.QueryAsync<string>(
            new CommandDefinition("SELECT FileName FROM Files WHERE ModelId = @ModelId AND IsActive = 1;", new { ModelId = rootModel.Id }, cancellationToken: ct))).ToList();
        var singleFile = Assert.Single(rootFiles);
        Assert.Equal("Base.stl", singleFile);

        // Rescan idempotency check
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        var rescanCounts = await SnapshotCountsAsync(ct);
        Assert.Equal(counts, rescanCounts);

        // Delete loose root geometry file: only the root model should be retired; subfolders stay active
        var basePath = Path.Combine(releaseDir, "Base.stl");
        File.Delete(basePath);
        var deleteResult = await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);
        Assert.Equal(1, deleteResult.RetiredFiles);
        Assert.Equal(1, deleteResult.RetiredModels);
        Assert.Equal(0, deleteResult.RetiredReleases);

        var afterDeleteCounts = await SnapshotCountsAsync(ct);
        Assert.Equal(2, afterDeleteCounts.ActiveModels);
        Assert.Equal(4, afterDeleteCounts.ActiveFiles);
        Assert.Equal(3, afterDeleteCounts.TotalModels);

        // Restore loose file: root model reactivates without creating a new row
        await File.WriteAllBytesAsync(basePath, [3, 1], ct);
        await _scanner.BulkScanAsync(_libraryRoot, cancellationToken: ct);

        var restoredCounts = await SnapshotCountsAsync(ct);
        Assert.Equal(3, restoredCounts.ActiveModels);
        Assert.Equal(5, restoredCounts.ActiveFiles);
        Assert.Equal(3, restoredCounts.TotalModels);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private sealed record Counts(
        long ActiveArtists, long ActiveReleases, long ActiveModels, long ActiveFiles,
        long TotalModels);

    private async Task<Counts> SnapshotCountsAsync(System.Threading.CancellationToken ct)
    {
        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.QuerySingleAsync<Counts>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM Artists  WHERE IsActive = 1) AS ActiveArtists,
                (SELECT COUNT(*) FROM Releases WHERE IsActive = 1) AS ActiveReleases,
                (SELECT COUNT(*) FROM Models   WHERE IsActive = 1) AS ActiveModels,
                (SELECT COUNT(*) FROM Files    WHERE IsActive = 1) AS ActiveFiles,
                (SELECT COUNT(*) FROM Models)                      AS TotalModels;
            """,
            cancellationToken: ct));
    }

    private async Task<string[]> GetActiveModelIdsAsync(System.Threading.CancellationToken ct)
    {
        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        return (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT Id FROM Models WHERE IsActive = 1 ORDER BY Id;", cancellationToken: ct))).ToArray();
    }

    private async Task<string> GetSingleFileHashAsync(System.Threading.CancellationToken ct)
    {
        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT ContentHash FROM Files WHERE IsActive = 1;", cancellationToken: ct));
    }

    private async Task BuildXalatathFixtureAsync(System.Threading.CancellationToken ct)
    {
        var releaseDir = Path.Combine(_libraryRoot, "Luftmensch", "Xalatath");
        var dir16 = Path.Combine(releaseDir, "Xalatath 1-6 scale");
        var dir110 = Path.Combine(releaseDir, "Xalatath 1-10 scale");
        Directory.CreateDirectory(dir16);
        Directory.CreateDirectory(dir110);
        await File.WriteAllBytesAsync(Path.Combine(dir16, "Torso.stl"), [1, 1], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir16, "Cape.stl"), [1, 2], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir110, "Torso.stl"), [2, 1], ct);
        await File.WriteAllBytesAsync(Path.Combine(dir110, "Cape.stl"), [2, 2], ct);
    }

    private Task<string> BuildSingleModelAsync(
        string artist, string release, string model, string[] files, System.Threading.CancellationToken ct) =>
        BuildModelUnderAsync(_libraryRoot, artist, release, model, files, ct);

    private static async Task<string> BuildModelUnderAsync(
        string root, string artist, string release, string model, string[] files,
        System.Threading.CancellationToken ct)
    {
        var dir = Path.Combine(root, artist, release, model);
        Directory.CreateDirectory(dir);
        for (var i = 0; i < files.Length; i++)
        {
            await File.WriteAllBytesAsync(Path.Combine(dir, files[i]), [(byte)(i + 1), (byte)(files[i].Length)], ct);
        }

        return dir;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection conn, string table, string column, System.Threading.CancellationToken ct)
    {
        var cols = await conn.QueryAsync<(long cid, string name)>(new CommandDefinition(
            $"SELECT cid, name FROM pragma_table_info('{table}');", cancellationToken: ct));
        return cols.Any(c => string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Simulates a pre-v11 database: drops the v11 index and records, and resets
    /// user_version so the next MigrateAsync re-runs v11. Columns are left in
    /// place (AddColumnIfNotExists makes v11 re-entrant), which matches how an
    /// upgraded database would look apart from the new data rules.
    /// </summary>
    private static async Task DowngradeToV10Async(SqliteConnection conn, System.Threading.CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            """
            DROP INDEX IF EXISTS UX_Models_FolderPath;
            DROP INDEX IF EXISTS IX_Files_ContentHash;
            DELETE FROM DPVSchemaMigrations WHERE Version = 11;
            PRAGMA user_version = 10;
            """,
            cancellationToken: ct));
    }
}
