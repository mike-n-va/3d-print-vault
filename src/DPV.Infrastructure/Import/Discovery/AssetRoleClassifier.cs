using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DPV.Domain.Enums;

namespace DPV.Infrastructure.Import.Discovery;

public static class AssetRoleClassifier
{
    private static readonly Regex SupportedTokenRegex = new(
        @"(\b|_|-)(pre[-_]?supported|supported|supp|pre)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AssemblyComponentRegex = new(
        @"(\b|_|-)(part[-_]?\d+|piece[-_]?\d+|head|torso|body|arm|leg|wing|wings|tail|hand|foot|weapon|shield|base)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HeroRenderRegex = new(
        @"(\b|_|-)(hero|render|preview|cover|photo|display)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ThumbnailRegex = new(
        @"(\b|_|-)(thumb|thumbnail|icon)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LicenseRegex = new(
        @"(\b|_|-)(license|terms|copyright|disclaimer)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AssemblyGuideRegex = new(
        @"(\b|_|-)(guide|assembly|instruction|instructions|manual|readme|read[-_]?me)(\b|_|-|\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (AssetCategory Category, FileRole Role) Classify(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        if (filePath.EndsWith(".dpv.json", StringComparison.OrdinalIgnoreCase))
        {
            return (AssetCategory.MetadataSidecar, FileRole.SidecarManifest);
        }

        var dirPath = Path.GetDirectoryName(filePath);
        var dirName = !string.IsNullOrWhiteSpace(dirPath)
            ? Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToLowerInvariant()
            : string.Empty;

        var inSupportedFolder = dirName is "supported" or "pre-supported" or "presupported" or "pre_supported";
        var inRendersFolder = dirName is "renders" or "render" or "images" or "image" or "previews" or "preview";
        var inDocsFolder = dirName is "docs" or "doc" or "documentation" or "instructions" or "manual";

        switch (ext)
        {
            case ".stl":
            case ".obj":
                if (inSupportedFolder || SupportedTokenRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.Geometry, FileRole.SupportedVariant);
                }

                if (AssemblyComponentRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.Geometry, FileRole.AssemblyComponent);
                }

                return (AssetCategory.Geometry, FileRole.PrimaryGeometry);

            case ".3mf":
            case ".ctb":
            case ".lychee":
            case ".chitubox":
                return (AssetCategory.SlicingPayload, FileRole.SlicedPlate);

            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".webp":
                if (ThumbnailRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.VisualMedia, FileRole.Thumbnail);
                }

                if (inRendersFolder || HeroRenderRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.VisualMedia, FileRole.HeroRender);
                }

                return (AssetCategory.VisualMedia, FileRole.HeroRender);

            case ".pdf":
            case ".docx":
            case ".doc":
            case ".txt":
            case ".md":
                if (LicenseRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.Documentation, FileRole.License);
                }

                if (inDocsFolder || AssemblyGuideRegex.IsMatch(fileNameWithoutExt))
                {
                    return (AssetCategory.Documentation, FileRole.AssemblyGuide);
                }

                return (AssetCategory.Documentation, FileRole.AssemblyGuide);

            case ".json":
                return (AssetCategory.MetadataSidecar, FileRole.SidecarManifest);

            case ".zip":
                return (AssetCategory.Geometry, FileRole.PrimaryGeometry);

            default:
                return (AssetCategory.Documentation, FileRole.Auxiliary);
        }
    }

    public static bool IsSupportedAssetExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".stl"
            or ".obj"
            or ".3mf"
            or ".ctb"
            or ".lychee"
            or ".chitubox"
            or ".png"
            or ".jpg"
            or ".jpeg"
            or ".webp"
            or ".pdf"
            or ".txt"
            or ".md"
            or ".json"
            or ".zip";
    }
}
