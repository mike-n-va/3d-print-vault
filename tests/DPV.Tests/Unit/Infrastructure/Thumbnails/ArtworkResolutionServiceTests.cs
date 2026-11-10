using System;
using System.IO;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Persistence;
using DPV.Infrastructure.Thumbnails;
using DPV.UI.ViewModels;
using SkiaSharp;
using Xunit;

namespace DPV.Tests.Unit.Infrastructure.Thumbnails;

public sealed class ArtworkResolutionServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _cacheDirectory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly SkiaProceduralCollageSynthesizer _synthesizer;
    private readonly ArtworkResolutionService _artworkService;

    public ArtworkResolutionServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "dpv-artwork-tests-" + Guid.NewGuid().ToString("N"));
        _cacheDirectory = Path.Combine(_testRoot, "Cache");
        _databasePath = Path.Combine(_testRoot, "test.db");

        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_cacheDirectory);

        _connectionFactory = new SqliteConnectionFactory(_databasePath);
        _migrationRunner = new MigrationRunner();

        // Initialize schema
        using var conn = _connectionFactory.CreateConnection();
        conn.Open();
        _migrationRunner.MigrateAsync(conn).GetAwaiter().GetResult();

        _synthesizer = new SkiaProceduralCollageSynthesizer();
        _artworkService = new ArtworkResolutionService(_connectionFactory, _synthesizer, _cacheDirectory);
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
    public async Task ResolveModelArtwork_PrioritizesTier1ExplicitCover_OverRendersAndCollage()
    {
        var modelDir = Path.Combine(_testRoot, "Model_Paladin");
        Directory.CreateDirectory(modelDir);

        var coverFile = Path.Combine(modelDir, "cover.jpg");
        var renderFile = Path.Combine(modelDir, "Beauty Render.png");
        var partFile = Path.Combine(modelDir, "part_01.png");

        CreateDummyImage(coverFile, color: SKColors.Red);
        CreateDummyImage(renderFile, color: SKColors.Green);
        CreateDummyImage(partFile, color: SKColors.Blue);

        var modelId = Guid.NewGuid();
        var result = await _artworkService.ResolveModelArtworkAsync(
            modelId,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkTier.Tier1ExplicitCover, result.Tier);
        Assert.Equal(coverFile, result.ResolvedPath);
        Assert.False(result.IsSynthesized);
    }

    [Fact]
    public async Task ResolveModelArtwork_PrioritizesTier1SidecarDpvJson_WhenPresent()
    {
        var modelDir = Path.Combine(_testRoot, "Model_Dragon");
        Directory.CreateDirectory(modelDir);

        var customHero = Path.Combine(modelDir, "art_showcase.png");
        CreateDummyImage(customHero, color: SKColors.Purple);

        var sidecarPath = Path.Combine(modelDir, ".dpv.json");
        File.WriteAllText(sidecarPath, "{\"cover\": \"art_showcase.png\"}");

        var modelId = Guid.NewGuid();
        var result = await _artworkService.ResolveModelArtworkAsync(
            modelId,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkTier.Tier1ExplicitCover, result.Tier);
        Assert.Equal(customHero, result.ResolvedPath);
    }

    [Fact]
    public async Task ResolveModelArtwork_PrioritizesTier2Renders_WhenExplicitCoverMissing()
    {
        var modelDir = Path.Combine(_testRoot, "Model_Sorceress");
        var rendersDir = Path.Combine(modelDir, "Renders");
        Directory.CreateDirectory(rendersDir);

        var beautyRender = Path.Combine(rendersDir, "Sorceress_Beauty_Render.png");
        var meshPreview = Path.Combine(modelDir, "mesh_screenshot.png");

        CreateDummyImage(beautyRender, color: SKColors.Gold);
        CreateDummyImage(meshPreview, color: SKColors.Gray);

        var modelId = Guid.NewGuid();
        var result = await _artworkService.ResolveModelArtworkAsync(
            modelId,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkTier.Tier2Render, result.Tier);
        Assert.Equal(beautyRender, result.ResolvedPath);
        Assert.False(result.IsSynthesized);
    }

    [Fact]
    public async Task ResolveModelArtwork_GeneratesTier3Collage_WhenOnlyComponentImagesExist()
    {
        var modelDir = Path.Combine(_testRoot, "Model_RawGeometry");
        Directory.CreateDirectory(modelDir);

        // Raw geometry folder with zero explicit covers and zero renders, but 4 component previews
        var p1 = Path.Combine(modelDir, "component_arm.png");
        var p2 = Path.Combine(modelDir, "component_leg.png");
        var p3 = Path.Combine(modelDir, "component_torso.png");
        var p4 = Path.Combine(modelDir, "component_head.png");

        CreateDummyImage(p1, 200, 200, SKColors.Crimson);
        CreateDummyImage(p2, 200, 200, SKColors.DarkBlue);
        CreateDummyImage(p3, 200, 200, SKColors.DarkGreen);
        CreateDummyImage(p4, 200, 200, SKColors.DarkOrange);

        var modelId = Guid.NewGuid();
        var result = await _artworkService.ResolveModelArtworkAsync(
            modelId,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkTier.Tier3Collage, result.Tier);
        Assert.True(result.IsSynthesized);
        Assert.NotNull(result.ResolvedPath);
        Assert.True(File.Exists(result.ResolvedPath));

        // Verify synthesized collage properties
        using var decoded = SKBitmap.Decode(result.ResolvedPath);
        Assert.NotNull(decoded);
        Assert.Equal(1000, decoded.Width);
        Assert.Equal(1000, decoded.Height);
    }

    [Fact]
    public async Task ResolveModelArtwork_ReturnsTier4GeometricFallback_WhenNoImagesExist()
    {
        var modelDir = Path.Combine(_testRoot, "Model_StlOnly");
        Directory.CreateDirectory(modelDir);

        File.WriteAllText(Path.Combine(modelDir, "bracket.stl"), "solid bracket endsolid");

        var modelId = Guid.NewGuid();
        var result = await _artworkService.ResolveModelArtworkAsync(
            modelId,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkTier.Tier4GeometricFallback, result.Tier);
        Assert.Null(result.ResolvedPath);
        Assert.False(result.IsSynthesized);
    }

    [Fact]
    public async Task SkiaProceduralCollageSynthesizer_Generates1000x1000MasterMosaic_WithSeparators()
    {
        var src1 = Path.Combine(_testRoot, "tile1.png");
        var src2 = Path.Combine(_testRoot, "tile2.png");
        var src3 = Path.Combine(_testRoot, "tile3.png");
        var src4 = Path.Combine(_testRoot, "tile4.png");

        CreateDummyImage(src1, 300, 300, SKColors.Teal);
        CreateDummyImage(src2, 300, 300, SKColors.Magenta);
        CreateDummyImage(src3, 300, 300, SKColors.LimeGreen);
        CreateDummyImage(src4, 300, 300, SKColors.SlateBlue);

        var outFile = Path.Combine(_cacheDirectory, "mosaic_test.png");
        var synthesized = await _synthesizer.SynthesizeCollageAsync(
            [src1, src2, src3, src4],
            outFile,
            canvasSize: 1000,
            separatorThickness: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(synthesized);
        Assert.True(File.Exists(synthesized));

        using var bmp = SKBitmap.Decode(synthesized);
        Assert.Equal(1000, bmp.Width);
        Assert.Equal(1000, bmp.Height);
    }

    [Fact]
    public async Task ModelCardViewModel_ResolveArtworkAsync_BindsSmoothly_AndSetsTier()
    {
        var modelDir = Path.Combine(_testRoot, "Model_VM_Test");
        Directory.CreateDirectory(modelDir);
        var coverFile = Path.Combine(modelDir, "cover.jpg");
        CreateDummyImage(coverFile, color: SKColors.Coral);

        var card = new ModelCardViewModel(
            id: Guid.NewGuid(),
            name: "Paladin Knight",
            thumbnailPath: null);

        await card.ResolveArtworkAsync(
            _artworkService,
            modelDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(coverFile, card.ThumbnailPath);
        Assert.Equal(coverFile, card.ActiveImagePath);
        Assert.Equal(ArtworkTier.Tier1ExplicitCover, card.ResolutionTier);
    }

    [Fact]
    public async Task ReleaseCardViewModel_ResolveArtworkAsync_BindsSmoothly_AndSetsTier()
    {
        var relDir = Path.Combine(_testRoot, "Release_VM_Test");
        Directory.CreateDirectory(relDir);
        var bannerFile = Path.Combine(relDir, "banner.jpg");
        CreateDummyImage(bannerFile, color: SKColors.Indigo);

        var card = new ReleaseCardViewModel(
            releaseId: Guid.NewGuid(),
            name: "March 2026 Release",
            bannerPath: null);

        await card.ResolveArtworkAsync(
            _artworkService,
            relDir,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(bannerFile, card.BannerPath);
        Assert.Equal(ArtworkTier.Tier1ExplicitCover, card.ResolutionTier);
    }

    [Fact]
    public async Task ResolveAllUnresolvedModelsAsync_ResolvesUnresolvedModelsInDatabase_AndCachesPaths()
    {
        var modelId = Guid.NewGuid();
        var modelDir = Path.Combine(_testRoot, "Batch_Model");
        Directory.CreateDirectory(modelDir);
        var coverFile = Path.Combine(modelDir, "cover.png");
        CreateDummyImage(coverFile, color: SKColors.DeepPink);

        var stlFile = Path.Combine(modelDir, "model.stl");
        File.WriteAllText(stlFile, "solid model endsolid");

        // Insert model without hero preview into SQLite
        using var conn = _connectionFactory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Models (Id, Name, ScanRoot) VALUES (@id, 'Batch Model', @root);
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES (@id, @path, 'hash123', 100, '2026-11-06T00:00:00Z', 1);
        ";
        cmd.Parameters.AddWithValue("@id", modelId.ToString());
        cmd.Parameters.AddWithValue("@root", _testRoot);
        cmd.Parameters.AddWithValue("@path", stlFile);
        cmd.ExecuteNonQuery();

        var resolved = await _artworkService.ResolveAllUnresolvedModelsAsync(
            scanRoot: _testRoot,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(resolved.ContainsKey(modelId));
        Assert.Equal(coverFile, resolved[modelId]);

        // Verify SQLite cache was updated
        using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT HeroPreviewPath FROM Models WHERE Id = @id;";
        verifyCmd.Parameters.AddWithValue("@id", modelId.ToString());
        var cached = (string?)verifyCmd.ExecuteScalar();
        Assert.Equal(coverFile, cached);
    }

    private static void CreateDummyImage(string filePath, int width = 100, int height = 100, SKColor? color = null)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color ?? SKColors.CornflowerBlue);
        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.Create(filePath);
        encoded.SaveTo(stream);
    }
}
