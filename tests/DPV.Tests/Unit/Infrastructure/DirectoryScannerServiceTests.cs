using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using Xunit;

namespace DPV.Tests.Unit.Infrastructure;

public sealed class DirectoryScannerServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly DirectoryScannerService _scannerService;
    private readonly DapperModelDetailRepository _detailRepository;

    public DirectoryScannerServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "dpv-scanner-tests-" + Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testRoot, "test.db");

        Directory.CreateDirectory(_testRoot);

        _connectionFactory = new SqliteConnectionFactory(_databasePath);
        _migrationRunner = new MigrationRunner();

        using var conn = _connectionFactory.CreateConnection();
        conn.Open();
        _migrationRunner.MigrateAsync(conn).GetAwaiter().GetResult();

        _scannerService = new DirectoryScannerService(_connectionFactory, _migrationRunner);
        _detailRepository = new DapperModelDetailRepository(_connectionFactory, _migrationRunner);
    }

    public void Dispose()
    {
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

    [Fact]
    public async Task BulkScan_FourTierRelationalHierarchy_PopulatesAllTablesCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;

        // Setup: Root -> Artist -> Release -> Model -> Files
        var artistDir = Path.Combine(_testRoot, "Luftmensch");
        var releaseDir = Path.Combine(artistDir, "Xalatath");
        var modelDir = Path.Combine(releaseDir, "MainModel");
        Directory.CreateDirectory(modelDir);

        var stl1 = Path.Combine(modelDir, "body.stl");
        var obj2 = Path.Combine(modelDir, "wings.obj");
        await File.WriteAllBytesAsync(stl1, [1, 2, 3], ct);
        await File.WriteAllBytesAsync(obj2, [4, 5, 6, 7], ct);

        var result = await _scannerService.BulkScanAsync(_testRoot, cancellationToken: ct);

        Assert.Equal(1, result.TotalArtists);
        Assert.Equal(1, result.TotalReleases);
        Assert.Equal(1, result.TotalModels);
        Assert.Equal(2, result.TotalFiles);
        Assert.True(result.Success);

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        // 1. Verify Artists
        var artist = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                "SELECT Id, Name, FolderPath FROM Artists WHERE Name = @Name",
                new { Name = "Luftmensch" },
                cancellationToken: ct));
        Assert.NotNull(artist);
        Assert.Equal(Path.GetFullPath(artistDir), (string?)artist?.FolderPath);

        // 2. Verify Releases
        var release = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                "SELECT Id, Name, ArtistId, FolderPath FROM Releases WHERE Name = @Name",
                new { Name = "Xalatath" },
                cancellationToken: ct));
        Assert.NotNull(release);
        Assert.Equal((string?)artist?.Id, (string?)release?.ArtistId);
        Assert.Equal(Path.GetFullPath(releaseDir), (string?)release?.FolderPath);

        // 3. Verify Models
        var model = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                "SELECT Id, Name, ReleaseId, FolderPath FROM Models WHERE Name = @Name",
                new { Name = "MainModel" },
                cancellationToken: ct));
        Assert.NotNull(model);
        Assert.Equal((string?)release?.Id, (string?)model?.ReleaseId);
        Assert.Equal(Path.GetFullPath(modelDir), (string?)model?.FolderPath);

        // 4. Verify Files
        var files = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(
                "SELECT Id, ModelId, FileName, FilePath, FileExtension, FileSize FROM Files WHERE ModelId = @ModelId",
                new { ModelId = (string)model!.Id },
                cancellationToken: ct))).ToList();
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => (string)f.FileName == "body.stl" && (string)f.FileExtension == ".stl");
        Assert.Contains(files, f => (string)f.FileName == "wings.obj" && (string)f.FileExtension == ".obj");

        // Verify Inspector Detail Repository queries from Files
        var detail = await _detailRepository.GetModelDetailAsync(Guid.Parse((string)model.Id), ct);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Files.Count);
        Assert.Contains(detail.Files, f => f.FilePath == Path.GetFullPath(stl1));
        Assert.Contains(detail.Files, f => f.FilePath == Path.GetFullPath(obj2));
    }

    [Fact]
    public async Task BulkScan_ScaleVariants_DetectsNormalizedScales()
    {
        var ct = TestContext.Current.CancellationToken;

        var artistDir = Path.Combine(_testRoot, "ScaleArtist");
        var releaseDir = Path.Combine(artistDir, "Warrior");
        var modelDir1 = Path.Combine(releaseDir, "Warrior 1-6 scale");
        var modelDir2 = Path.Combine(releaseDir, "Warrior 1-10 scale");
        var modelDir3 = Path.Combine(releaseDir, "Bust 75mm");

        Directory.CreateDirectory(modelDir1);
        Directory.CreateDirectory(modelDir2);
        Directory.CreateDirectory(modelDir3);

        await File.WriteAllBytesAsync(Path.Combine(modelDir1, "part.stl"), [1], ct);
        await File.WriteAllBytesAsync(Path.Combine(modelDir2, "part.stl"), [2], ct);
        await File.WriteAllBytesAsync(Path.Combine(modelDir3, "part.stl"), [3], ct);

        var result = await _scannerService.BulkScanAsync(_testRoot, cancellationToken: ct);

        Assert.Equal(3, result.TotalModels);

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        var models = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(
                "SELECT Name, Scale FROM Models ORDER BY Name",
                cancellationToken: ct))).ToList();

        var m1 = models.First(m => ((string)m.Name).Contains("1-6"));
        var m2 = models.First(m => ((string)m.Name).Contains("1-10"));
        var m3 = models.First(m => ((string)m.Name).Contains("75mm"));

        Assert.Equal("1/6", (string)m1.Scale);
        Assert.Equal("1/10", (string)m2.Scale);
        Assert.Equal("75mm", (string)m3.Scale);
    }

    [Fact]
    public async Task BulkScan_SiblingArtworkFolder_DiscoversReleaseCoverArt()
    {
        var ct = TestContext.Current.CancellationToken;

        var artistDir = Path.Combine(_testRoot, "ArtArtist");
        var releaseDir = Path.Combine(artistDir, "Sorceress");
        var siblingArtDir = Path.Combine(artistDir, "Sorceress Pictures");
        var modelDir = Path.Combine(releaseDir, "Model");

        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(siblingArtDir);

        await File.WriteAllBytesAsync(Path.Combine(modelDir, "figure.stl"), [1, 2], ct);
        var coverImg = Path.Combine(siblingArtDir, "cover.jpg");
        await File.WriteAllBytesAsync(coverImg, [255, 216, 255], ct);

        var result = await _scannerService.BulkScanAsync(_testRoot, cancellationToken: ct);

        Assert.Equal(1, result.TotalReleases);

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        var release = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                "SELECT Name, CoverImagePath FROM Releases WHERE Name = 'Sorceress'",
                cancellationToken: ct));

        Assert.NotNull(release);
        Assert.Equal(Path.GetFullPath(coverImg), (string?)release?.CoverImagePath);
    }

    [Fact]
    public async Task IncrementalScan_SkipsUnmodifiedDirectories_AndIndexesNewAdditions()
    {
        var ct = TestContext.Current.CancellationToken;

        var artistDir = Path.Combine(_testRoot, "DeltaArtist");
        var releaseDir1 = Path.Combine(artistDir, "ReleaseOne");
        var modelDir1 = Path.Combine(releaseDir1, "Model");
        Directory.CreateDirectory(modelDir1);
        await File.WriteAllBytesAsync(Path.Combine(modelDir1, "part1.stl"), [1], ct);

        // Initial bulk scan
        var initialResult = await _scannerService.BulkScanAsync(_testRoot, cancellationToken: ct);
        Assert.Equal(1, initialResult.TotalReleases);
        Assert.Equal(1, initialResult.TotalModels);

        // Incremental scan without modifications
        var unchangedResult = await _scannerService.IncrementalScanAsync(_testRoot, cancellationToken: ct);
        Assert.Equal(0, unchangedResult.TotalModels); // Skipped unmodified release

        // Now add a second release
        var releaseDir2 = Path.Combine(artistDir, "ReleaseTwo");
        var modelDir2 = Path.Combine(releaseDir2, "Model");
        Directory.CreateDirectory(modelDir2);
        await File.WriteAllBytesAsync(Path.Combine(modelDir2, "part2.stl"), [2], ct);

        // Incremental scan picking up only the new release
        var deltaResult = await _scannerService.IncrementalScanAsync(_testRoot, cancellationToken: ct);
        Assert.Equal(1, deltaResult.TotalReleases);
        Assert.Equal(1, deltaResult.TotalModels);
        Assert.Equal(1, deltaResult.TotalFiles);
    }

    [Theory]
    [InlineData("Model 1-6 scale", "1/6")]
    [InlineData("Knight 1:10", "1/10")]
    [InlineData("Figure 1_12", "1/12")]
    [InlineData("Monster 75mm", "75mm")]
    [InlineData("Paladin 32mm", "32mm")]
    [InlineData("Dragon Bust", "Bust")]
    [InlineData("Chibi Hero", "Chibi")]
    [InlineData("Life-size Replica", "Life-Size")]
    [InlineData("NoScaleSpecified", null)]
    public void DetectScale_NormalizedOutputs(string input, string? expected)
    {
        var actual = DirectoryScannerService.DetectScale(input);
        Assert.Equal(expected, actual);
    }
}
