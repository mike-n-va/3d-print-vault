using System;
using System.IO;
using System.Linq;
using DPV.Domain.Enums;
using DPV.Domain.Models;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class StemClustererTests
{
    [Theory]
    [InlineData("dragon_body_supported", "dragon_body")]
    [InlineData("dragon_wings_presupported", "dragon_wings")]
    [InlineData("paladin_v2", "paladin")]
    [InlineData("warrior_part1", "warrior")]
    [InlineData("monster_mesh", "monster")]
    [InlineData("hero_preview", "hero")]
    [InlineData("boss_render", "boss")]
    public void ExtractStem_StripsKnownVariantSuffixes(string input, string expected)
    {
        var stem = StemClusterer.ExtractStem(input);
        Assert.Equal(expected, stem);
    }

    [Theory]
    [InlineData("base", true)]
    [InlineData("wall", true)]
    [InlineData("tile", true)]
    [InlineData("AncientRedDragon", false)]
    [InlineData("GoblinArcher", false)]
    public void IsGenericStem_IdentifiesGenericNounTokens(string stem, bool expected)
    {
        var actual = StemClusterer.IsGenericStem(stem);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ClusterByStem_ClustersCommonStemsAndSeparatesUnrelatedFiles()
    {
        var assets = new[]
        {
            CreateAsset("dragon_body.stl"),
            CreateAsset("dragon_wings.stl"),
            CreateAsset("dragon_render.png"),
            CreateAsset("goblin_archer_1.stl"),
            CreateAsset("goblin_archer_2.stl"),
            CreateAsset("base_25mm.stl")
        };

        var clusters = StemClusterer.ClusterByStem(assets);

        // Expect: 1 dragon cluster (3 assets), 1 goblin cluster (2 assets), 1 base singleton
        var dragonCluster = Assert.Single(clusters, c => c.Any(a => a.FileName.StartsWith("dragon")));
        Assert.Equal(3, dragonCluster.Count);

        var goblinCluster = Assert.Single(clusters, c => c.Any(a => a.FileName.StartsWith("goblin")));
        Assert.Equal(2, goblinCluster.Count);

        var baseCluster = Assert.Single(clusters, c => c.Any(a => a.FileName.StartsWith("base")));
        Assert.Single(baseCluster);
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
