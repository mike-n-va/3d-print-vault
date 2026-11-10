using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Persistence;

namespace DPV.Infrastructure.Thumbnails;

public sealed class ArtworkResolutionService : IArtworkResolutionService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private static readonly string[] ExplicitCoverNames =
    [
        "folder", "cover", "poster", "thumbnail", "setposter", "banner", "promo"
    ];

    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly IProceduralCollageSynthesizer _collageSynthesizer;
    private readonly string _cacheDirectory;

    public ArtworkResolutionService(
        IDatabaseConnectionFactory connectionFactory,
        IProceduralCollageSynthesizer collageSynthesizer,
        string? cacheDirectory = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _collageSynthesizer = collageSynthesizer ?? throw new ArgumentNullException(nameof(collageSynthesizer));

        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DPV",
                "Cache",
                "Artwork")
            : cacheDirectory;

        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<ArtworkResolutionResult> ResolveModelArtworkAsync(
        Guid modelId,
        string modelDirectoryPath,
        IEnumerable<string>? candidateFilePaths = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedDir = NormalizeDirectoryPath(modelDirectoryPath);
        var candidates = candidateFilePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [];

        var result = await ResolveCascadingArtworkAsync(
            entityType: "model",
            entityId: modelId,
            directoryPath: resolvedDir,
            candidateFiles: candidates,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.ResolvedPath))
        {
            await CacheModelArtworkAsync(modelId, result.ResolvedPath, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ArtworkResolutionResult> ResolveReleaseArtworkAsync(
        Guid releaseId,
        string releaseDirectoryPath,
        IEnumerable<string>? candidateFilePaths = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedDir = NormalizeDirectoryPath(releaseDirectoryPath);
        var candidates = candidateFilePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [];

        var result = await ResolveCascadingArtworkAsync(
            entityType: "release",
            entityId: releaseId,
            directoryPath: resolvedDir,
            candidateFiles: candidates,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.ResolvedPath))
        {
            await CacheReleaseArtworkAsync(releaseId, result.ResolvedPath, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAllUnresolvedModelsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<Guid, string>();

        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = """
                SELECT m.Id, m.Name, m.ScanRoot, mf.NormalizedPath AS FilePath
                FROM Models m
                LEFT JOIN ModelFiles mf ON mf.ModelId = m.Id
                WHERE (m.HeroPreviewPath IS NULL OR m.HeroPreviewPath = '')
                  AND (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
                GROUP BY m.Id;
                """;

            var models = await connection.QueryAsync<(string Id, string Name, string ScanRoot, string? FilePath)>(
                new CommandDefinition(sql, new { ScanRoot = scanRoot }, cancellationToken: cancellationToken)).ConfigureAwait(false);

            foreach (var model in models)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!Guid.TryParse(model.Id, out var modelId))
                {
                    continue;
                }

                var dir = !string.IsNullOrWhiteSpace(model.FilePath)
                    ? Path.GetDirectoryName(model.FilePath)
                    : model.ScanRoot;

                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                var candidates = !string.IsNullOrWhiteSpace(model.FilePath) ? new[] { model.FilePath } : null;
                var result = await ResolveModelArtworkAsync(modelId, dir, candidates, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.ResolvedPath))
                {
                    resolved[modelId] = result.ResolvedPath;
                }
            }
        }
        catch
        {
            // Non-critical background resolution
        }

        return resolved;
    }

    private async Task<ArtworkResolutionResult> ResolveCascadingArtworkAsync(
        string entityType,
        Guid entityId,
        string? directoryPath,
        IReadOnlyList<string> candidateFiles,
        CancellationToken cancellationToken)
    {
        // -------------------------------------------------------------
        // TIER 1: Explicit Cover Art (case-insensitive exact matches or .dpv.json sidecar)
        // -------------------------------------------------------------
        var tier1Path = FindTier1ExplicitCover(directoryPath, candidateFiles);
        if (tier1Path is not null)
        {
            return new ArtworkResolutionResult(tier1Path, ArtworkTier.Tier1ExplicitCover);
        }

        // -------------------------------------------------------------
        // TIER 2: Beauty & Clay Renders (renders, sculpts, promos)
        // -------------------------------------------------------------
        var tier2Path = FindTier2Renders(directoryPath, candidateFiles);
        if (tier2Path is not null)
        {
            return new ArtworkResolutionResult(tier2Path, ArtworkTier.Tier2Render);
        }

        // -------------------------------------------------------------
        // TIER 3: Procedural 2x2 Collage Synthesizer (Fallback when 1-4 images exist)
        // -------------------------------------------------------------
        var componentImages = GatherComponentImages(directoryPath, candidateFiles);
        if (componentImages.Count > 0)
        {
            var cacheFile = Path.Combine(_cacheDirectory, $"{entityType}_{entityId:N}_collage.png");
            var synthesizedPath = await _collageSynthesizer.SynthesizeCollageAsync(
                sourceImagePaths: componentImages,
                outputCacheFilePath: cacheFile,
                canvasSize: 1000,
                separatorThickness: 2,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(synthesizedPath) && File.Exists(synthesizedPath))
            {
                return new ArtworkResolutionResult(synthesizedPath, ArtworkTier.Tier3Collage, IsSynthesized: true);
            }
        }

        // -------------------------------------------------------------
        // TIER 4: Geometric Safe Fallback (Devoid of any valid image files)
        // -------------------------------------------------------------
        return new ArtworkResolutionResult(null, ArtworkTier.Tier4GeometricFallback);
    }

    private static string? FindTier1ExplicitCover(string? directoryPath, IReadOnlyList<string> candidateFiles)
    {
        // 1. Check sidecar metadata file (.dpv.json) if present
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            var sidecarPath = Path.Combine(directoryPath, ".dpv.json");
            if (File.Exists(sidecarPath))
            {
                var sidecarCover = ReadSidecarCoverPath(sidecarPath, directoryPath);
                if (sidecarCover is not null && File.Exists(sidecarCover))
                {
                    return sidecarCover;
                }
            }
        }

        // 2. Check root directory files for exact explicit cover names
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            try
            {
                var rootFiles = Directory.GetFiles(directoryPath);
                foreach (var file in rootFiles)
                {
                    var ext = Path.GetExtension(file);
                    if (!SupportedImageExtensions.Contains(ext))
                    {
                        continue;
                    }

                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    if (IsExplicitCoverName(nameWithoutExt))
                    {
                        return file;
                    }
                }
            }
            catch
            {
                // Ignore IO errors when querying directory
            }
        }

        // 3. Check candidate files
        foreach (var file in candidateFiles)
        {
            if (!File.Exists(file)) continue;

            var ext = Path.GetExtension(file);
            if (!SupportedImageExtensions.Contains(ext)) continue;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            if (IsExplicitCoverName(nameWithoutExt))
            {
                return file;
            }
        }

        return null;
    }

    private static string? FindTier2Renders(string? directoryPath, IReadOnlyList<string> candidateFiles)
    {
        var allImages = new List<string>();

        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            try
            {
                // Priority scan: Subfolders like Renders/, Promos/, Photos/, Images/ first
                var subDirs = Directory.GetDirectories(directoryPath)
                    .OrderByDescending(d =>
                    {
                        var name = Path.GetFileName(d);
                        return name.Contains("render", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("promo", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("photo", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("image", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    });

                foreach (var sub in subDirs)
                {
                    try
                    {
                        allImages.AddRange(Directory.GetFiles(sub).Where(IsImageFile));
                    }
                    catch
                    {
                    }
                }

                // Add root images as well
                allImages.AddRange(Directory.GetFiles(directoryPath).Where(IsImageFile));
            }
            catch
            {
            }
        }

        foreach (var candidate in candidateFiles)
        {
            if (IsImageFile(candidate) && !allImages.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                allImages.Add(candidate);
            }
        }

        // Priority 2.1: Beauty Render
        var beautyMatch = allImages.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Contains("beauty", StringComparison.OrdinalIgnoreCase));
        if (beautyMatch is not null) return beautyMatch;

        // Priority 2.2: Clay Render
        var clayMatch = allImages.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Contains("clay", StringComparison.OrdinalIgnoreCase));
        if (clayMatch is not null) return clayMatch;

        // Priority 2.3: Sculpt
        var sculptMatch = allImages.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Contains("sculpt", StringComparison.OrdinalIgnoreCase));
        if (sculptMatch is not null) return sculptMatch;

        // Priority 2.4: General Rendered / Render
        var renderMatch = allImages.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Contains("render", StringComparison.OrdinalIgnoreCase));
        if (renderMatch is not null) return renderMatch;

        return null;
    }

    private static List<string> GatherComponentImages(string? directoryPath, IReadOnlyList<string> candidateFiles)
    {
        var images = new List<string>();

        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            try
            {
                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(IsImageFile)
                    .Take(4);
                images.AddRange(files);
            }
            catch
            {
            }
        }

        if (images.Count < 4)
        {
            foreach (var candidate in candidateFiles)
            {
                if (IsImageFile(candidate) && !images.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    images.Add(candidate);
                    if (images.Count >= 4) break;
                }
            }
        }

        return images;
    }

    private static bool IsExplicitCoverName(string nameWithoutExt)
    {
        foreach (var explicitName in ExplicitCoverNames)
        {
            if (nameWithoutExt.Equals(explicitName, StringComparison.OrdinalIgnoreCase)
                || nameWithoutExt.StartsWith(explicitName + "_", StringComparison.OrdinalIgnoreCase)
                || nameWithoutExt.StartsWith(explicitName + "-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsImageFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path);
            return SupportedImageExtensions.Contains(ext) && File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadSidecarCoverPath(string sidecarJsonPath, string directoryPath)
    {
        try
        {
            var json = File.ReadAllText(sidecarJsonPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string[] candidateProps = ["coverImage", "coverImagePath", "cover", "thumbnail", "preview", "hero", "banner"];
            foreach (var prop in candidateProps)
            {
                if (root.TryGetProperty(prop, out var elem) && elem.ValueKind == JsonValueKind.String)
                {
                    var val = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var resolved = Path.IsPathRooted(val) ? val : Path.Combine(directoryPath, val);
                        if (File.Exists(resolved))
                        {
                            return resolved;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            if (File.Exists(path))
            {
                return Path.GetDirectoryName(path);
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                return dir;
            }
        }
        catch
        {
        }

        return path;
    }

    private async Task CacheModelArtworkAsync(Guid modelId, string resolvedPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(
                "UPDATE Models SET HeroPreviewPath = @HeroPreviewPath WHERE Id = @Id;",
                new { HeroPreviewPath = resolvedPath, Id = modelId.ToString() }).ConfigureAwait(false);
        }
        catch
        {
            // Non-critical persistence
        }
    }

    private async Task CacheReleaseArtworkAsync(Guid releaseId, string resolvedPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(
                "UPDATE Releases SET CoverImagePath = @CoverImagePath WHERE Id = @Id;",
                new { CoverImagePath = resolvedPath, Id = releaseId.ToString() }).ConfigureAwait(false);
        }
        catch
        {
            // Non-critical persistence
        }
    }
}
