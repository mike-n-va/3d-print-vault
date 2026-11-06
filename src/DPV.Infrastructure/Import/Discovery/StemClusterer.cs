using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DPV.Domain.Models;

namespace DPV.Infrastructure.Import.Discovery;

public static class StemClusterer
{
    private static readonly HashSet<string> GenericNounTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "base", "stand", "wall", "floor", "tile", "weapon", "door", "crate", "box", "rock",
        "tree", "chair", "table", "barrel", "head", "arm", "leg", "body", "wing", "tail",
        "shield", "sword", "mini", "miniature", "model", "print", "part", "piece", "item",
        "render", "preview", "photo", "image", "guide", "manual", "license", "instructions"
    };

    private static readonly Regex VariantSuffixRegex = new(
        @"[-_](pre[-_]?supported|supported|supp|pre|part[-_]?\d+|piece[-_]?\d+|\d+|v\d+|rev\d+|scaled|fixed|repaired|plate[-_]?\d+|mesh|render|preview|thumb|hero|guide|manual|license)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ExtractStem(string fileNameWithoutExtension)
    {
        var cleaned = fileNameWithoutExtension.Trim();
        var prev = string.Empty;
        while (prev != cleaned)
        {
            prev = cleaned;
            cleaned = VariantSuffixRegex.Replace(cleaned, string.Empty).TrimEnd('_', '-', ' ');
        }

        return cleaned;
    }

    public static bool IsGenericStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem) || stem.Length < 3)
        {
            return true;
        }

        var tokens = stem.Split(['_', '-', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 1 && GenericNounTokens.Contains(tokens[0]);
    }

    public static string GetClusterKey(string fileNameWithoutExtension)
    {
        var stem = ExtractStem(fileNameWithoutExtension);
        if (IsGenericStem(stem))
        {
            return stem;
        }

        // If stem contains a sub-part token (e.g. dragon_body or dragon_wings), cluster on primary prefix
        var lastDelim = stem.LastIndexOfAny(['_', '-', ' ']);
        if (lastDelim > 0)
        {
            var prefix = stem[..lastDelim];
            if (prefix.Length >= 4 && !IsGenericStem(prefix))
            {
                return prefix;
            }
        }

        return stem;
    }

    public static IReadOnlyList<List<DiscoveredAsset>> ClusterByStem(IReadOnlyList<DiscoveredAsset> assets)
    {
        if (assets.Count <= 1)
        {
            return [assets.ToList()];
        }

        var groups = new Dictionary<string, List<DiscoveredAsset>>(StringComparer.OrdinalIgnoreCase);
        var unclustered = new List<DiscoveredAsset>();

        foreach (var asset in assets)
        {
            var rawName = Path.GetFileNameWithoutExtension(asset.FileName);
            var key = GetClusterKey(rawName);

            if (IsGenericStem(key) || key.Length < 4)
            {
                unclustered.Add(asset);
                continue;
            }

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(asset);
        }

        var result = new List<List<DiscoveredAsset>>();

        foreach (var pair in groups)
        {
            if (pair.Value.Count > 1 || pair.Value.Any(a => a.Category == DPV.Domain.Enums.AssetCategory.Geometry))
            {
                result.Add(pair.Value);
            }
            else
            {
                unclustered.AddRange(pair.Value);
            }
        }

        foreach (var solo in unclustered)
        {
            result.Add([solo]);
        }

        return result;
    }
}
