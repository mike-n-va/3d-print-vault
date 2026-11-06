using System;
using System.IO;
using DPV.Domain.Enums;
using DPV.Domain.Models;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class ConfidenceScorerTests
{
    [Fact]
    public void CalculateConfidence_SidecarReturnsHighConfidence()
    {
        var assets = new[] { CreateAsset("model.stl") };
        var (score, tier) = ConfidenceScorer.CalculateConfidence(assets, @"C:\models\Dragon", @"C:\models", hasSidecar: true);

        Assert.Equal(1.0, score);
        Assert.Equal(ModelConfidenceTier.HighConfidence, tier);
    }

    [Fact]
    public void CalculateConfidence_UserVerifiedReturnsUserVerifiedTier()
    {
        var assets = new[] { CreateAsset("model.stl") };
        var (score, tier) = ConfidenceScorer.CalculateConfidence(assets, @"C:\models\Dragon", @"C:\models", isUserVerified: true);

        Assert.Equal(1.0, score);
        Assert.Equal(ModelConfidenceTier.UserVerified, tier);
    }

    [Fact]
    public void CalculateConfidence_LeafFolderWithMeshSupportedAndHeroReturnsHighConfidence()
    {
        var assets = new[]
        {
            CreateAsset("dragon.stl"),
            CreateAsset("dragon_supported.stl"),
            CreateAsset("dragon_render.png"),
            CreateAsset("guide.pdf")
        };

        var (score, tier) = ConfidenceScorer.CalculateConfidence(assets, @"C:\models\AncientDragon", @"C:\models");

        Assert.True(score >= 0.80, $"Expected >= 0.80 but got {score}");
        Assert.Equal(ModelConfidenceTier.HighConfidence, tier);
    }

    [Fact]
    public void CalculateConfidence_ExcessiveFilesPenalizesScore()
    {
        var assets = new DiscoveredAsset[25];
        for (var i = 0; i < 25; i++)
        {
            assets[i] = CreateAsset($"file_{i}.stl");
        }

        var (score, tier) = ConfidenceScorer.CalculateConfidence(assets, @"C:\models\BigFolder", @"C:\models");

        Assert.True(score < 0.50, $"Expected < 0.50 but got {score}");
        Assert.Equal(ModelConfidenceTier.LowConfidence, tier);
    }

    private static DiscoveredAsset CreateAsset(string fileName)
    {
        var (category, role) = AssetRoleClassifier.Classify(fileName);
        return new DiscoveredAsset(
            FilePath: Path.Combine(@"C:\models", fileName),
            NormalizedPath: Path.Combine(@"C:\models", fileName),
            FileName: fileName,
            FileExtension: Path.GetExtension(fileName),
            FileSizeBytes: 1024,
            ModifiedAtUtc: DateTime.UtcNow,
            ContentHash: Guid.NewGuid().ToString("N"),
            Category: category,
            Role: role);
    }
}
