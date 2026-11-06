using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DPV.Domain.Enums;
using DPV.Domain.Models;

namespace DPV.Infrastructure.Import.Discovery;

public static class ConfidenceScorer
{
    public static (double Score, ModelConfidenceTier Tier, IReadOnlyList<string> Evidence) CalculateDetailedConfidence(
        IReadOnlyList<DiscoveredAsset> assets,
        string enclosingDirectory,
        string scanRoot,
        bool hasSidecar = false,
        bool isUserVerified = false)
    {
        var evidence = new List<string>();

        if (isUserVerified)
        {
            evidence.Add("Explicit User Verified (1.00)");
            return (1.0, ModelConfidenceTier.UserVerified, evidence);
        }

        if (hasSidecar)
        {
            evidence.Add("Sidecar Manifest Authority (1.00)");
            return (1.0, ModelConfidenceTier.HighConfidence, evidence);
        }

        var score = 0.0;
        var geometryAssets = assets.Where(a => a.Category == AssetCategory.Geometry).ToList();
        var hasRaw = geometryAssets.Any(a => a.Role == FileRole.PrimaryGeometry || a.Role == FileRole.AssemblyComponent);
        var hasSupported = geometryAssets.Any(a => a.Role == FileRole.SupportedVariant);
        var hasHero = assets.Any(a => a.Category == AssetCategory.VisualMedia);
        var hasDoc = assets.Any(a => a.Category == AssetCategory.Documentation);
        var isLeaf = !string.Equals(enclosingDirectory, scanRoot, StringComparison.OrdinalIgnoreCase);

        // Positive evidence
        if (isLeaf && geometryAssets.Count <= 15)
        {
            score += 0.40;
            evidence.Add("Dedicated Leaf Folder (+0.40)");
        }

        if (hasRaw && hasSupported)
        {
            score += 0.30;
            evidence.Add("Paired Raw and Supported Variants (+0.30)");
        }

        if (hasHero)
        {
            score += 0.20;
            evidence.Add("Hero Preview Present (+0.20)");
        }

        if (hasDoc)
        {
            score += 0.15;
            evidence.Add("Documentation Guide Present (+0.15)");
        }

        if (isLeaf)
        {
            score += 0.10;
            evidence.Add("Leaf Hierarchy Boundary (+0.10)");
        }

        // Stem matching evidence
        if (assets.Count > 1)
        {
            var stems = assets
                .Select(a => StemClusterer.GetClusterKey(Path.GetFileNameWithoutExtension(a.FileName)))
                .Where(s => !StemClusterer.IsGenericStem(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (stems.Count == 1 && stems[0].Length >= 5)
            {
                score += 0.50;
                evidence.Add($"Stem Pattern Match: '{stems[0]}' (+0.50)");
            }
        }
        else if (assets.Count == 1 && geometryAssets.Count == 1)
        {
            score += 0.50;
            evidence.Add("Single Geometry Asset (+0.50)");
        }

        if (assets.Any(a => a.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            evidence.Add("Archive Package Container");
        }

        // Penalties
        if (geometryAssets.Count > 20)
        {
            score -= 0.40;
            evidence.Add("Excessive Geometry Count Penalty (-0.40)");
        }

        // When a kit contains verified raw + supported pairs inside a dedicated leaf folder,
        // anatomical names (body, head) are expected components of the model kit, not random grab-bag files.
        var genericOnly = assets.All(a => StemClusterer.IsGenericStem(StemClusterer.ExtractStem(Path.GetFileNameWithoutExtension(a.FileName))));
        if (genericOnly && assets.Count > 1 && !(isLeaf && hasRaw && hasSupported))
        {
            score -= 0.30;
            evidence.Add("Generic Stem Penalty (-0.30)");
        }

        var imageCount = assets.Count(a => a.Category == AssetCategory.VisualMedia);
        if (imageCount > 3)
        {
            score -= 0.25;
            evidence.Add("Excessive Media Penalty (-0.25)");
        }

        // Eliminate floating-point imprecision (e.g. 0.49999999999999994 -> 0.50)
        score = Math.Round(Math.Clamp(score, 0.0, 1.0), 4);

        var tier = score switch
        {
            >= 0.80 => ModelConfidenceTier.HighConfidence,
            >= 0.50 => ModelConfidenceTier.MediumConfidence,
            _ => ModelConfidenceTier.LowConfidence
        };

        return (score, tier, evidence);
    }

    public static (double Score, ModelConfidenceTier Tier) CalculateConfidence(
        IReadOnlyList<DiscoveredAsset> assets,
        string enclosingDirectory,
        string scanRoot,
        bool hasSidecar = false,
        bool isUserVerified = false)
    {
        var (score, tier, _) = CalculateDetailedConfidence(assets, enclosingDirectory, scanRoot, hasSidecar, isUserVerified);
        return (score, tier);
    }
}
