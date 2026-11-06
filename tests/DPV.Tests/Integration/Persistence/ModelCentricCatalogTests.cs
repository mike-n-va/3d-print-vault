using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;

namespace DPV.Tests.Integration.Persistence;

public class ModelCentricCatalogTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;

    public ModelCentricCatalogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"dpv-model-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "catalog.db");
    }

    [Fact]
    public async Task Import_GroupsMultipartModelAndInfersHierarchy()
    {
        // Setup hierarchy: Root / ArtisanGuild / Dragons / RedDragon / (dragon_body.stl, dragon_wings.stl, Supported/dragon_body_supported.stl, Renders/render.png)
        var dragonFolder = Path.Combine(_root, "ArtisanGuild", "Dragons", "RedDragon");
        WriteBinaryStl(Path.Combine(dragonFolder, "dragon_body.stl"));
        WriteBinaryStl(Path.Combine(dragonFolder, "dragon_wings.stl"));
        WriteBinaryStl(Path.Combine(dragonFolder, "Supported", "dragon_body_supported.stl"));
        WriteDummyFile(Path.Combine(dragonFolder, "Renders", "render.png"), [1, 2, 3]);

        var service = new ModelImportService(
            new SqliteConnectionFactory(_dbPath),
            new MigrationRunner(),
            new SqliteCatalogStore());

        var result = await service.ImportAsync(
            new ImportLibraryRequest(_root, 2L * 1024 * 1024 * 1024),
            TestContext.Current.CancellationToken);

        Assert.True(result.Completed);
        Assert.Equal(3, result.TotalAdded); // 3 printable stl assets

        // Query catalog
        var query = new DapperCatalogQuery(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        var models = await query.SearchModelsAsync(
            new ModelQueryParameters(ScanRoot: _root),
            TestContext.Current.CancellationToken);

        // Crucial validation: S5-010 Model-based search returns 1 Model, NOT 3 individual files!
        var model = Assert.Single(models);
        Assert.Equal("RedDragon", model.Name);
        Assert.Equal(3, model.AssetCount);
        Assert.Equal("ArtisanGuild", model.ArtistName);
        Assert.Equal("Dragons", model.CollectionName);
        Assert.NotNull(model.ThumbnailPath);
        Assert.EndsWith("render.png", model.ThumbnailPath);
        Assert.True(model.ConfidenceScore >= 0.80);
        Assert.Equal("HighConfidence", model.ConfidenceTier);

        // Check Grouping Query for Artists and Collections
        var artists = await query.GetArtistsAsync(_root, TestContext.Current.CancellationToken);
        var artist = Assert.Single(artists);
        Assert.Equal("ArtisanGuild", artist.Name);
        Assert.Equal(1, artist.ModelCount);

        var collections = await query.GetCollectionsAsync(_root, TestContext.Current.CancellationToken);
        var collection = Assert.Single(collections);
        Assert.Equal("Dragons", collection.Name);
        Assert.Equal(1, collection.ModelCount);

        // Check Detail Repository for diagnostics (S5-002)
        var detailRepo = new DapperModelDetailRepository(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        var detail = await detailRepo.GetModelDetailAsync(model.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal("RedDragon", detail.Name);
        Assert.Equal("ArtisanGuild", detail.ArtistName);
        Assert.Equal("Dragons", detail.CollectionName);
        Assert.Equal(3, detail.Files.Count);
        Assert.NotNull(detail.DiscoveryEvidence);
        Assert.NotEmpty(detail.DiscoveryEvidence);
    }

    [Fact]
    public async Task MetadataOwnership_UserEditsTakePrecedenceOverInferred()
    {
        var modelFolder = Path.Combine(_root, "UnknownArtist", "Misc", "TestModel");
        WriteBinaryStl(Path.Combine(modelFolder, "model.stl"));

        var service = new ModelImportService(
            new SqliteConnectionFactory(_dbPath),
            new MigrationRunner(),
            new SqliteCatalogStore());

        await service.ImportAsync(new ImportLibraryRequest(_root, 2L * 1024 * 1024 * 1024), TestContext.Current.CancellationToken);

        var query = new DapperCatalogQuery(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        var models = await query.SearchModelsAsync(new ModelQueryParameters(ScanRoot: _root), TestContext.Current.CancellationToken);
        var model = Assert.Single(models);
        Assert.Equal("UnknownArtist", model.ArtistName);

        // User manually edits metadata
        var detailRepo = new DapperModelDetailRepository(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        await detailRepo.UpdateModelMetadataAsync(model.Id, "VerifiedCreator", "EpicSet", ["featured"], TestContext.Current.CancellationToken);

        // Re-import (rescan)
        await service.ImportAsync(new ImportLibraryRequest(_root, 2L * 1024 * 1024 * 1024), TestContext.Current.CancellationToken);

        // Verify that user edits were preserved and not overwritten by folder inference
        var rechecked = await detailRepo.GetModelDetailAsync(model.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(rechecked);
        Assert.Equal("VerifiedCreator", rechecked.ArtistName);
        Assert.Equal("EpicSet", rechecked.CollectionName);
        Assert.Contains("featured", rechecked.Tags);
    }

    [Fact]
    public async Task Favorites_ToggleAndFilter_PersistsAndReportsCorrectCount()
    {
        var modelFolder = Path.Combine(_root, "StudioX", "Monsters", "GoblinWarrior");
        WriteBinaryStl(Path.Combine(modelFolder, "goblin.stl"));

        var service = new ModelImportService(
            new SqliteConnectionFactory(_dbPath),
            new MigrationRunner(),
            new SqliteCatalogStore());

        await service.ImportAsync(new ImportLibraryRequest(_root, 2L * 1024 * 1024 * 1024), TestContext.Current.CancellationToken);

        var query = new DapperCatalogQuery(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        var detailRepo = new DapperModelDetailRepository(new SqliteConnectionFactory(_dbPath), new MigrationRunner());

        var models = await query.SearchModelsAsync(new ModelQueryParameters(ScanRoot: _root), TestContext.Current.CancellationToken);
        var model = Assert.Single(models);
        Assert.False(model.IsFavorite);

        var countBefore = await query.GetFavoritesCountAsync(_root, TestContext.Current.CancellationToken);
        Assert.Equal(0, countBefore);

        // Toggle favorite ON
        await detailRepo.ToggleFavoriteAsync(model.Id, TestContext.Current.CancellationToken);

        var countAfterFav = await query.GetFavoritesCountAsync(_root, TestContext.Current.CancellationToken);
        Assert.Equal(1, countAfterFav);

        var favModels = await query.SearchModelsAsync(new ModelQueryParameters(ScanRoot: _root, OnlyFavorites: true), TestContext.Current.CancellationToken);
        var favModel = Assert.Single(favModels);
        Assert.Equal(model.Id, favModel.Id);
        Assert.True(favModel.IsFavorite);

        var detail = await detailRepo.GetModelDetailAsync(model.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.True(detail.IsFavorite);

        // Toggle favorite OFF
        await detailRepo.ToggleFavoriteAsync(model.Id, TestContext.Current.CancellationToken);

        var countAfterUnfav = await query.GetFavoritesCountAsync(_root, TestContext.Current.CancellationToken);
        Assert.Equal(0, countAfterUnfav);

        var noFavModels = await query.SearchModelsAsync(new ModelQueryParameters(ScanRoot: _root, OnlyFavorites: true), TestContext.Current.CancellationToken);
        Assert.Empty(noFavModels);
    }

    [Fact]
    public async Task ModelDescriptionAndPrintNotes_UpdateAndRetrieve_Persists()
    {
        var modelFolder = Path.Combine(_root, "StudioX", "Terrain", "RuinedTower");
        WriteBinaryStl(Path.Combine(modelFolder, "tower.stl"));

        var service = new ModelImportService(
            new SqliteConnectionFactory(_dbPath),
            new MigrationRunner(),
            new SqliteCatalogStore());

        await service.ImportAsync(new ImportLibraryRequest(_root, 2L * 1024 * 1024 * 1024), TestContext.Current.CancellationToken);

        var query = new DapperCatalogQuery(new SqliteConnectionFactory(_dbPath), new MigrationRunner());
        var detailRepo = new DapperModelDetailRepository(new SqliteConnectionFactory(_dbPath), new MigrationRunner());

        var models = await query.SearchModelsAsync(new ModelQueryParameters(ScanRoot: _root), TestContext.Current.CancellationToken);
        var model = Assert.Single(models);

        await detailRepo.UpdateModelDescriptionAsync(
            model.Id,
            "Ancient crumbling outpost from the first era.",
            "Print at 0.16mm layer height, 15% infill, tree supports.",
            TestContext.Current.CancellationToken);

        var detail = await detailRepo.GetModelDetailAsync(model.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal("Ancient crumbling outpost from the first era.", detail.Description);
        Assert.Equal("Print at 0.16mm layer height, 15% infill, tree supports.", detail.PrintNotes);
    }

    private void WriteBinaryStl(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80, 4), 0);
        File.WriteAllBytes(path, bytes);
    }

    private void WriteDummyFile(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
