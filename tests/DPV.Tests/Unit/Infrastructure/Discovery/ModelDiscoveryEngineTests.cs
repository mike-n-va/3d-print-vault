using System;
using System.IO;
using System.Linq;
using DPV.Domain.Enums;
using DPV.Domain.Models;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class ModelDiscoveryEngineTests
{
    [Fact]
    public void DiscoverModels_GroupsLeafFolderIntoSingleModelWithSupportingAssets()
    {
        var scanRoot = @"C:\vault";
        var folder = Path.Combine(scanRoot, "ArtisanGuild", "Dragons", "RedDragon");
        var assets = new[]
        {
            CreateAsset(Path.Combine(folder, "dragon_body.stl")),
            CreateAsset(Path.Combine(folder, "dragon_wings.stl")),
            CreateAsset(Path.Combine(folder, "Supported", "dragon_body_supported.stl")),
            CreateAsset(Path.Combine(folder, "plate.3mf")),
            CreateAsset(Path.Combine(folder, "Renders", "dragon_render.png")),
            CreateAsset(Path.Combine(folder, "Docs", "assembly.pdf"))
        };

        var engine = new ModelDiscoveryEngine();
        var models = engine.DiscoverModels(scanRoot, assets);

        var model = Assert.Single(models);
        Assert.Equal("RedDragon", model.DisplayName);
        Assert.Equal(6, model.Assets.Count);
        Assert.Equal("ArtisanGuild", model.InferredArtist);
        Assert.Equal("Dragons", model.InferredCollection);
        Assert.EndsWith("dragon_render.png", model.HeroPreviewPath);
        Assert.EndsWith("dragon_body_supported.stl", model.PrimaryMeshPath);
        Assert.Contains("supported", model.Tags);
        Assert.Contains("3mf", model.Tags);
        Assert.True(model.ConfidenceScore >= 0.80);
        Assert.Equal("LeafFolderPackage", model.DiscoveryMethod);
        Assert.NotNull(model.DiscoveryEvidence);
        Assert.NotEmpty(model.DiscoveryEvidence);
    }

    [Fact]
    public void DiscoverModels_SupportedUnsupportedKitsGroupIntoSingleModel()
    {
        var scanRoot = @"C:\vault";
        var folder = Path.Combine(scanRoot, "Dragon");
        var assets = new[]
        {
            CreateAsset(Path.Combine(folder, "Supported", "body_supported.stl")),
            CreateAsset(Path.Combine(folder, "Supported", "head_supported.stl")),
            CreateAsset(Path.Combine(folder, "Unsupported", "body.stl")),
            CreateAsset(Path.Combine(folder, "Unsupported", "head.stl"))
        };

        var engine = new ModelDiscoveryEngine();
        var models = engine.DiscoverModels(scanRoot, assets);

        var model = Assert.Single(models);
        Assert.Equal("Dragon", model.DisplayName);
        Assert.Equal(4, model.Assets.Count);
        Assert.True(model.ConfidenceScore >= 0.80);
        Assert.Equal(ModelConfidenceTier.HighConfidence, model.ConfidenceTier);
    }

    [Fact]
    public void DiscoverModels_InfersArtistReleaseAndCollection()
    {
        var scanRoot = @"D:\Vault";
        var folder = Path.Combine(scanRoot, "Artists", "TitanForge", "Jan2026", "Dragon");
        var (artist, collection, release) = ModelDiscoveryEngine.InferFolderMetadata(folder, scanRoot);

        Assert.Equal("TitanForge", artist);
        Assert.Equal("Jan2026", release);
        Assert.Null(collection);
    }

    [Fact]
    public void DiscoverModels_DiscoversFlatFolderStemsIntoDistinctModels()
    {
        var scanRoot = @"C:\vault";
        var assets = new[]
        {
            CreateAsset(Path.Combine(scanRoot, "orc_warrior_1.stl")),
            CreateAsset(Path.Combine(scanRoot, "orc_warrior_2.stl")),
            CreateAsset(Path.Combine(scanRoot, "goblin_archer_1.stl")),
            CreateAsset(Path.Combine(scanRoot, "goblin_archer_2.stl")),
            CreateAsset(Path.Combine(scanRoot, "base_25mm.stl"))
        };

        var engine = new ModelDiscoveryEngine();
        var models = engine.DiscoverModels(scanRoot, assets);

        // Expect: 1 orc model, 1 goblin model, 1 base model
        Assert.Equal(3, models.Count);
        Assert.Contains(models, m => m.DisplayName.StartsWith("orc_warrior"));
        Assert.Contains(models, m => m.DisplayName.StartsWith("goblin_archer"));
        Assert.Contains(models, m => m.DisplayName.StartsWith("base_25mm"));
    }

    [Fact]
    public void DiscoverModels_SkipsFoldersWithNo3DPrintableAssets()
    {
        var scanRoot = @"C:\vault";
        var folder = Path.Combine(scanRoot, "DocumentsOnly");
        var assets = new[]
        {
            CreateAsset(Path.Combine(folder, "readme.txt")),
            CreateAsset(Path.Combine(folder, "license.txt"))
        };

        var engine = new ModelDiscoveryEngine();
        var models = engine.DiscoverModels(scanRoot, assets);

        Assert.Empty(models);
    }

    private static DiscoveredAsset CreateAsset(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var (category, role) = AssetRoleClassifier.Classify(fullPath);
        return new DiscoveredAsset(
            FilePath: fullPath,
            NormalizedPath: fullPath,
            FileName: fileName,
            FileExtension: Path.GetExtension(fileName),
            FileSizeBytes: 1024,
            ModifiedAtUtc: DateTime.UtcNow,
            ContentHash: Guid.NewGuid().ToString("N"),
            Category: category,
            Role: role);
    }
}
