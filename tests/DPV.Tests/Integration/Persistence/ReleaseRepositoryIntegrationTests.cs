using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Persistence;
using DPV.Infrastructure.Thumbnails;
using Xunit;

namespace DPV.Tests.Integration.Persistence;

public class ReleaseRepositoryIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly ReleaseArtworkService _artworkService;
    private readonly DapperReleaseRepository _repository;

    public ReleaseRepositoryIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dpv-release-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "catalog.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrationRunner = new MigrationRunner();
        _artworkService = new ReleaseArtworkService(Path.Combine(_tempDir, "cache"));
        _repository = new DapperReleaseRepository(_connectionFactory, _migrationRunner, _artworkService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SchemaV9_CreatesReleasesTable_AndAllowsCrudOperations()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await _migrationRunner.MigrateAsync(connection, TestContext.Current.CancellationToken);

        // Verify Schema V9 tables & columns
        var releasesExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Releases';");
        Assert.Equal(1, releasesExists);

        // Insert Artist & Release
        var artistId = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            "INSERT INTO Artists (Id, Name) VALUES (@Id, 'TitanForge');",
            new { Id = artistId });

        var releaseId = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            """
            INSERT INTO Releases (Id, Name, ArtistId, Description, CreatedAt)
            VALUES (@Id, 'Blood Reaper Orcs', @ArtistId, 'Brutal fantasy orc warband', '2026-03-01T00:00:00Z');
            """,
            new { Id = releaseId, ArtistId = artistId });

        // Insert Member Models
        var model1Id = Guid.NewGuid().ToString("D");
        var model2Id = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            """
            INSERT INTO Models (Id, Name, ArtistId, ReleaseId, ScanRoot, CreatedAt)
            VALUES (@Id, 'Orc Warlord', @ArtistId, @ReleaseId, 'C:/vault', '2026-03-01T00:00:00Z');
            """,
            new { Id = model1Id, ArtistId = artistId, ReleaseId = releaseId });
        await connection.ExecuteAsync(
            """
            INSERT INTO Models (Id, Name, ArtistId, ReleaseId, ScanRoot, CreatedAt)
            VALUES (@Id, 'Orc Shaman', @ArtistId, @ReleaseId, 'C:/vault', '2026-03-01T00:00:00Z');
            """,
            new { Id = model2Id, ArtistId = artistId, ReleaseId = releaseId });

        // Insert ModelFiles
        await connection.ExecuteAsync(
            """
            INSERT INTO ModelFiles (Id, ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive, FileRole)
            VALUES ('f1', @ModelId, 'C:/vault/warlord.stl', 'hash1', 1024, '2026-03-01T00:00:00Z', 1, 1);
            """,
            new { ModelId = model1Id });
        await connection.ExecuteAsync(
            """
            INSERT INTO ModelFiles (Id, ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive, FileRole)
            VALUES ('f2', @ModelId, 'C:/vault/shaman.stl', 'hash2', 2048, '2026-03-01T00:00:00Z', 1, 1);
            """,
            new { ModelId = model2Id });

        // Query Releases
        var releases = await _repository.GetReleasesAsync(new ReleaseQueryParameters(), TestContext.Current.CancellationToken);
        var release = Assert.Single(releases);
        Assert.Equal("Blood Reaper Orcs", release.Name);
        Assert.Equal("TitanForge", release.ArtistName);
        Assert.Equal(2, release.ModelCount);
        Assert.False(release.IsFavorite);

        // Toggle Favorite
        await _repository.ToggleReleaseFavoriteAsync(Guid.Parse(releaseId), TestContext.Current.CancellationToken);
        var favReleases = await _repository.GetReleasesAsync(new ReleaseQueryParameters(OnlyFavorites: true), TestContext.Current.CancellationToken);
        Assert.Single(favReleases);
        Assert.True(favReleases.First().IsFavorite);

        // Query Release Detail with member models
        var detail = await _repository.GetReleaseDetailAsync(Guid.Parse(releaseId), TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal("Blood Reaper Orcs", detail.Name);
        Assert.Equal(2, detail.Models.Count);
        Assert.Contains(detail.Models, m => m.Name == "Orc Warlord");
        Assert.Contains(detail.Models, m => m.Name == "Orc Shaman");

        // Query Release Statistics
        var stats = await _repository.GetReleaseStatisticsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.TotalReleases);
        Assert.Equal(2, stats.TotalModelsInReleases);
        Assert.Equal(1, stats.FavoritedReleases);
    }
}
