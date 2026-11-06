using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DPV.Domain.Contracts.Import;
using DPV.Domain.Enums;
using DPV.Domain.Models;

namespace DPV.Infrastructure.Import.Discovery;

public sealed class ModelDiscoveryEngine : IModelDiscoveryEngine
{
    private static readonly HashSet<string> GenericCategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "artists", "artist", "creators", "creator", "patreon", "patrons", "myminifactory", "mmf",
        "kickstarter", "library", "models", "miniatures", "minis", "releases", "release"
    };

    private static readonly Regex ReleasePatternRegex = new(
        @"^(\d{4}[-_]\d{2}|\d{2}[-_]\d{4}|(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[-_]?\d{2,4}|\d{2,4}[-_]?(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<DiscoveredModelCluster> DiscoverModels(
        string scanRoot,
        IReadOnlyList<DiscoveredAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.Count == 0)
        {
            return [];
        }

        var results = new List<DiscoveredModelCluster>();

        // 1. Group assets by their resolved logical folder (collapsing virtual variant subfolders)
        var byLogicalFolder = new Dictionary<string, List<DiscoveredAsset>>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            var logicalDir = LeafFolderCollapser.ResolveLogicalDirectory(asset.FilePath, scanRoot);
            if (!byLogicalFolder.TryGetValue(logicalDir, out var folderAssets))
            {
                folderAssets = [];
                byLogicalFolder[logicalDir] = folderAssets;
            }
            folderAssets.Add(asset);
        }

        // 2. Process each folder
        foreach (var (folder, folderAssets) in byLogicalFolder)
        {
            // Check for sidecar manifest (.dpv.json)
            var sidecarAsset = folderAssets.FirstOrDefault(a => a.Role == FileRole.SidecarManifest);
            if (sidecarAsset != null && SidecarReader.TryReadSidecar(sidecarAsset.FilePath, out var manifest) && manifest != null)
            {
                var cluster = CreateClusterFromSidecar(manifest, folderAssets, folder, scanRoot);
                if (cluster.Assets.Any(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload))
                {
                    results.Add(cluster);
                }
                continue;
            }

            var isScanRoot = string.Equals(folder, scanRoot, StringComparison.OrdinalIgnoreCase);
            var geometryAssets = folderAssets.Where(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload).ToList();
            if (geometryAssets.Count == 0)
            {
                continue; // Skip non-printable folders with no 3D meshes
            }

            // If it's a dedicated leaf folder with a reasonable number of items, treat as 1 Model Package
            if (!isScanRoot && geometryAssets.Count <= 15)
            {
                var (score, tier, evidence) = ConfidenceScorer.CalculateDetailedConfidence(folderAssets, folder, scanRoot);
                if (tier != ModelConfidenceTier.LowConfidence || folderAssets.Count == 1)
                {
                    results.Add(BuildCluster(folderAssets, folder, scanRoot, score, tier, evidence, "LeafFolderPackage"));
                    continue;
                }
            }

            // Otherwise, it's a flat directory or large folder with multiple models: apply Stem Clustering
            var stemClusters = StemClusterer.ClusterByStem(folderAssets);
            foreach (var stemGroup in stemClusters)
            {
                if (!stemGroup.Any(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload))
                {
                    continue;
                }

                var (score, tier, evidence) = ConfidenceScorer.CalculateDetailedConfidence(stemGroup, folder, scanRoot);

                // If low confidence and multiple assets, split safely into singletons
                if (tier == ModelConfidenceTier.LowConfidence && stemGroup.Count > 1)
                {
                    foreach (var solo in stemGroup.Where(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload))
                    {
                        var (soloScore, soloTier, soloEvidence) = ConfidenceScorer.CalculateDetailedConfidence([solo], folder, scanRoot);
                        results.Add(BuildCluster([solo], folder, scanRoot, soloScore, soloTier, soloEvidence, "SingletonFallback"));
                    }
                }
                else
                {
                    var isArchive = stemGroup.Any(a => a.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase));
                    var method = isArchive ? "ArchivePackage" : "StemCluster";
                    results.Add(BuildCluster(stemGroup, folder, scanRoot, score, tier, evidence, method));
                }
            }
        }

        return results;
    }

    private static DiscoveredModelCluster CreateClusterFromSidecar(
        SidecarManifest manifest,
        IReadOnlyList<DiscoveredAsset> assets,
        string folder,
        string scanRoot)
    {
        var id = manifest.ModelId ?? Guid.NewGuid();
        var name = !string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Name : new DirectoryInfo(folder).Name;

        string? heroPath = null;
        if (!string.IsNullOrWhiteSpace(manifest.HeroThumbnail))
        {
            var match = assets.FirstOrDefault(a => a.FileName.Equals(manifest.HeroThumbnail, StringComparison.OrdinalIgnoreCase));
            heroPath = match?.FilePath;
        }

        heroPath ??= SelectHeroPreview(assets);
        var primaryMesh = SelectPrimaryMesh(assets);
        var (artist, collection, release) = (manifest.Artist, manifest.Collection, manifest.Release);
        if (string.IsNullOrWhiteSpace(artist))
        {
            var inferred = InferFolderMetadata(folder, scanRoot);
            artist = inferred.Artist;
            collection ??= inferred.Collection;
            release ??= inferred.Release;
        }

        return new DiscoveredModelCluster(
            Id: id,
            DisplayName: name,
            EnclosingDirectory: folder,
            Assets: assets,
            ConfidenceScore: 1.0,
            ConfidenceTier: ModelConfidenceTier.HighConfidence,
            HeroPreviewPath: heroPath,
            PrimaryMeshPath: primaryMesh,
            InferredArtist: artist,
            InferredCollection: collection,
            Tags: manifest.Tags ?? [],
            InferredRelease: release,
            DiscoveryMethod: "SidecarManifest",
            DiscoveryEvidence: ["Sidecar Manifest Authority (1.00)"]);
    }

    private static DiscoveredModelCluster BuildCluster(
        IReadOnlyList<DiscoveredAsset> assets,
        string folder,
        string scanRoot,
        double score,
        ModelConfidenceTier tier,
        IReadOnlyList<string> evidence,
        string discoveryMethod)
    {
        var id = Guid.NewGuid();
        var displayName = DetermineDisplayName(assets, folder, scanRoot);
        var heroPreview = SelectHeroPreview(assets);
        var primaryMesh = SelectPrimaryMesh(assets);
        var (artist, collection, release) = InferFolderMetadata(folder, scanRoot);
        var tags = InferTags(assets, folder);

        return new DiscoveredModelCluster(
            Id: id,
            DisplayName: displayName,
            EnclosingDirectory: folder,
            Assets: assets,
            ConfidenceScore: score,
            ConfidenceTier: tier,
            HeroPreviewPath: heroPreview,
            PrimaryMeshPath: primaryMesh,
            InferredArtist: artist,
            InferredCollection: collection,
            Tags: tags,
            InferredRelease: release,
            DiscoveryMethod: discoveryMethod,
            DiscoveryEvidence: evidence);
    }

    private static string DetermineDisplayName(IReadOnlyList<DiscoveredAsset> assets, string folder, string scanRoot)
    {
        var isLeaf = !string.Equals(folder, scanRoot, StringComparison.OrdinalIgnoreCase);

        // If in a dedicated leaf folder, folder name is usually the model name
        if (isLeaf && assets.Count > 1)
        {
            var folderName = new DirectoryInfo(folder).Name;
            if (!LeafFolderCollapser.IsVirtualVariantSubfolder(folderName))
            {
                return folderName;
            }
        }

        // Otherwise derive from primary asset stem
        var single = assets.FirstOrDefault(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload) ?? assets[0];
        return Path.GetFileNameWithoutExtension(single.FileName);
    }

    private static string? SelectHeroPreview(IReadOnlyList<DiscoveredAsset> assets)
    {
        // 1. Explicit Hero render image
        var hero = assets.FirstOrDefault(a => a.Role == FileRole.HeroRender);
        if (hero != null)
        {
            return hero.FilePath;
        }

        // 2. Any visual media image
        var image = assets.FirstOrDefault(a => a.Category == AssetCategory.VisualMedia);
        if (image != null)
        {
            return image.FilePath;
        }

        // 3. Fallback to primary geometry or slicing payload
        var mesh = assets.FirstOrDefault(a => a.Role == FileRole.PrimaryGeometry)
            ?? assets.FirstOrDefault(a => a.Role == FileRole.SupportedVariant)
            ?? assets.FirstOrDefault(a => a.Role == FileRole.SlicedPlate)
            ?? assets.FirstOrDefault();

        return mesh?.FilePath;
    }

    private static string? SelectPrimaryMesh(IReadOnlyList<DiscoveredAsset> assets)
    {
        var geometry = assets.Where(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload).ToList();
        if (geometry.Count == 0)
        {
            return null;
        }

        // 1. Pre-supported variant
        var supported = geometry.FirstOrDefault(a => a.Role == FileRole.SupportedVariant);
        if (supported != null)
        {
            return supported.FilePath;
        }

        // 2. Primary geometry or assembly component
        var primary = geometry.FirstOrDefault(a => a.Role == FileRole.PrimaryGeometry)
            ?? geometry.FirstOrDefault(a => a.Role == FileRole.AssemblyComponent);
        if (primary != null)
        {
            return primary.FilePath;
        }

        // 3. Sliced plate (.3mf)
        var plate = geometry.FirstOrDefault(a => a.Role == FileRole.SlicedPlate);
        if (plate != null)
        {
            return plate.FilePath;
        }

        return geometry.FirstOrDefault()?.FilePath;
    }

    public static (string? Artist, string? Collection, string? Release) InferFolderMetadata(string folder, string scanRoot)
    {
        if (string.Equals(folder, scanRoot, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        var relative = Path.GetRelativePath(scanRoot, folder);
        var rawParts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var parts = rawParts.Where(p => !GenericCategoryFolders.Contains(p)).ToList();

        if (parts.Count == 0)
        {
            return (null, null, null);
        }

        string? artist = null;
        string? collection = null;
        string? release = null;

        if (parts.Count >= 3)
        {
            artist = parts[0];
            if (ReleasePatternRegex.IsMatch(parts[1]))
            {
                release = parts[1];
                collection = parts.Count >= 4 ? parts[2] : null;
            }
            else
            {
                collection = parts[1];
                if (ReleasePatternRegex.IsMatch(parts[2]))
                {
                    release = parts[2];
                }
            }
        }
        else if (parts.Count == 2)
        {
            artist = parts[0];
            if (ReleasePatternRegex.IsMatch(parts[1]))
            {
                release = parts[1];
            }
            else
            {
                collection = parts[1];
            }
        }
        else if (parts.Count == 1)
        {
            artist = parts[0];
        }

        // Suffix release check in Artist token (e.g. TitanForge_Jan2026 -> Artist: TitanForge, Release: Jan2026)
        if (!string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(release))
        {
            var underscore = artist.LastIndexOf('_');
            if (underscore > 0)
            {
                var possibleRelease = artist[(underscore + 1)..];
                if (ReleasePatternRegex.IsMatch(possibleRelease))
                {
                    release = possibleRelease;
                    artist = artist[..underscore];
                }
            }
        }

        return (artist, collection, release);
    }

    private static IReadOnlyList<string> InferTags(IReadOnlyList<DiscoveredAsset> assets, string folder)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (assets.Any(a => a.Role == FileRole.SupportedVariant))
        {
            tags.Add("supported");
        }

        if (assets.Any(a => a.Role == FileRole.SlicedPlate))
        {
            tags.Add("3mf");
        }

        if (assets.Any(a => a.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            tags.Add("archive");
            tags.Add("zip");
        }

        var folderName = new DirectoryInfo(folder).Name.ToLowerInvariant();
        if (folderName.Contains("mini") || folderName.Contains("miniature"))
        {
            tags.Add("miniature");
        }
        if (folderName.Contains("terrain"))
        {
            tags.Add("terrain");
        }

        return tags.ToList();
    }
}
