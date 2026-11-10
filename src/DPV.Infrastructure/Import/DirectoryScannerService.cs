using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Import;

public sealed class DirectoryScannerService(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner,
    IArtworkResolutionService? artworkService = null) : IDirectoryScannerService
{
    private static readonly HashSet<string> GeometryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".stl", ".3mf", ".obj", ".step", ".stp", ".ply"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private static readonly Regex ScaleRatioRegex = new(
        @"(?:^|[\s_\-\(\[])(?:scale[\s_\-]*)?(?<ratio>1[\s_\-]*[:/x\-]*(?:[2468]|10|12|16|24|32|48|64|72))(?:[\s_\-\)\]]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScaleMmRegex = new(
        @"(?<val>\d+(?:\.\d+)?)\s*(?<unit>mm|cm)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScaleNamedRegex = new(
        @"\b(?<name>life[- ]?size|1:1|chibi|bust|miniature|heroic)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArtworkFolderRegex = new(
        @"(?:pictures?|renders?|promos?|photos?|images?|art(?:work)?|media)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<ScanResult> BulkScanAsync(
        string rootPath,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ScanAsync(rootPath, incremental: false, progress, cancellationToken);
    }

    public Task<ScanResult> IncrementalScanAsync(
        string rootPath,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ScanAsync(rootPath, incremental: true, progress, cancellationToken);
    }

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        bool incremental,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();

        if (!Directory.Exists(rootPath))
        {
            errors.Add($"Root directory not found: {rootPath}");
            return new ScanResult(0, 0, 0, 0, stopwatch.Elapsed, incremental, errors);
        }

        var normalizedRoot = Path.GetFullPath(rootPath);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        // Load existing scan timestamps for incremental mode
        var scanStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (incremental)
        {
            try
            {
                var rows = await connection.QueryAsync<(string FolderPath, string DirectoryLastWriteUtc)>(
                    new CommandDefinition(
                        "SELECT FolderPath, DirectoryLastWriteUtc FROM ScanState;",
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                foreach (var r in rows)
                {
                    scanStates[r.FolderPath] = r.DirectoryLastWriteUtc;
                }
            }
            catch
            {
                // Non-fatal if table not yet populated
            }
        }

        int totalArtists = 0;
        int totalReleases = 0;
        int totalModels = 0;
        int totalFiles = 0;

        string[] artistDirs;
        try
        {
            artistDirs = Directory.GetDirectories(normalizedRoot);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to enumerate artist directories: {ex.Message}");
            return new ScanResult(0, 0, 0, 0, stopwatch.Elapsed, incremental, errors);
        }

        // If no subdirectories, treat rootPath as a single artist folder
        if (artistDirs.Length == 0)
        {
            artistDirs = [normalizedRoot];
        }

        int processedArtists = 0;
        foreach (var artistDir in artistDirs)
        {
            if (cancellationToken.IsCancellationRequested) break;
            processedArtists++;

            var artistName = new DirectoryInfo(artistDir).Name;
            var artistNormalized = Path.GetFullPath(artistDir);

            // Tier 1: Artist
            var artistId = await UpsertArtistAsync(connection, artistName, artistNormalized, cancellationToken).ConfigureAwait(false);
            totalArtists++;

            progress?.Report(new ScanProgressReport(
                totalArtists, totalReleases, totalModels, totalFiles, artistDir,
                (double)processedArtists / artistDirs.Length * 100));

            // Enumerate Tier 2 candidate folders under Artist
            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(artistNormalized);
            }
            catch (Exception ex)
            {
                errors.Add($"Error reading artist dir '{artistDir}': {ex.Message}");
                continue;
            }

            // Distinguish release folders from sibling artwork/render folders
            var (releaseDirs, siblingArtworkDirs) = ClassifyFolders(subdirs);

            // If there are no release subdirectories, but artist directory contains files directly
            if (releaseDirs.Count == 0)
            {
                var directFiles = GetGeometryFiles(artistNormalized, SearchOption.TopDirectoryOnly);
                if (directFiles.Count > 0)
                {
                    releaseDirs.Add(artistNormalized);
                }
            }

            foreach (var releaseDir in releaseDirs)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var releaseNormalized = Path.GetFullPath(releaseDir);
                var releaseName = new DirectoryInfo(releaseDir).Name;
                var releaseLastWrite = Directory.GetLastWriteTimeUtc(releaseNormalized).ToString("o");

                // Check if directory changed in incremental mode
                if (incremental && scanStates.TryGetValue(releaseNormalized, out var prevWrite) && prevWrite == releaseLastWrite)
                {
                    // Unchanged release, skip deep re-indexing
                    continue;
                }

// Sibling and internal cover art discovery
                var coverArtPath = ResolveReleaseCoverArt(releaseDir, releaseName, siblingArtworkDirs);
                if (string.IsNullOrWhiteSpace(coverArtPath) && artworkService is not null)
                {
                    try
                    {
                        var res = await artworkService.ResolveReleaseArtworkAsync(
                            Guid.NewGuid(), releaseNormalized, null, cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(res.ResolvedPath))
                        {
                            coverArtPath = res.ResolvedPath;
                        }
                    }
                    catch
                    {
                    }
                }

                // Tier 2: Release
                var releaseId = await UpsertReleaseAsync(
                    connection, releaseName, artistId, releaseNormalized, coverArtPath, cancellationToken)
                    .ConfigureAwait(false);
                totalReleases++;

                // Tier 3 & 4: Models and Constituent Files
                var modelsAdded = await IngestModelsAndFilesAsync(
                    connection, releaseId, artistId, normalizedRoot, releaseDir, releaseName, coverArtPath, cancellationToken)
                    .ConfigureAwait(false);

                totalModels += modelsAdded.ModelsCount;
                totalFiles += modelsAdded.FilesCount;

                // Update ScanState
                await UpdateScanStateAsync(connection, releaseNormalized, releaseLastWrite, cancellationToken).ConfigureAwait(false);

                progress?.Report(new ScanProgressReport(
                    totalArtists, totalReleases, totalModels, totalFiles, releaseDir,
                    (double)processedArtists / artistDirs.Length * 100));
            }
        }

        stopwatch.Stop();
        return new ScanResult(totalArtists, totalReleases, totalModels, totalFiles, stopwatch.Elapsed, incremental, errors);
    }

    private static (List<string> Releases, List<string> SiblingArtworks) ClassifyFolders(string[] subdirs)
    {
        var releases = new List<string>();
        var artworks = new List<string>();

        foreach (var dir in subdirs)
        {
            var dirName = new DirectoryInfo(dir).Name;
            if (ArtworkFolderRegex.IsMatch(dirName))
            {
                artworks.Add(dir);
            }
            else
            {
                releases.Add(dir);
            }
        }

        return (releases, artworks);
    }

    private string? ResolveReleaseCoverArt(string releaseDir, string releaseName, List<string> siblingArtworkDirs)
    {
        // 1. Check sibling artwork folders matching this release name
        var matchingSiblings = siblingArtworkDirs
            .Where(s => new DirectoryInfo(s).Name.Contains(releaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingSiblings.Count == 0 && siblingArtworkDirs.Count > 0)
        {
            matchingSiblings = siblingArtworkDirs;
        }

        foreach (var sibling in matchingSiblings)
        {
            var art = FindBestImageInDirectory(sibling);
            if (!string.IsNullOrWhiteSpace(art))
            {
                return art;
            }
        }

        // 2. Check internal subfolders (Pictures, Renders, Art) or root of release folder
        var internalArt = FindBestImageInDirectory(releaseDir);
        if (!string.IsNullOrWhiteSpace(internalArt))
        {
            return internalArt;
        }

        return null;
    }

    private static string? FindBestImageInDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return null;

        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            return null;
        }

        var images = files
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (images.Count == 0) return null;

        // Priority 1: explicit cover art naming
        var explicitMatch = images.FirstOrDefault(f =>
        {
            var stem = Path.GetFileNameWithoutExtension(f);
            return stem.Equals("cover", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("poster", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("folder", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("banner", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("setposter", StringComparison.OrdinalIgnoreCase) ||
                   stem.Equals("promo", StringComparison.OrdinalIgnoreCase);
        });

        if (explicitMatch is not null) return Path.GetFullPath(explicitMatch);

        // Priority 2: beauty / render / promo
        var renderMatch = images.FirstOrDefault(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            return name.Contains("beauty", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("render", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("promo", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("clay", StringComparison.OrdinalIgnoreCase);
        });

        if (renderMatch is not null) return Path.GetFullPath(renderMatch);

        // Fallback: first available image
        return Path.GetFullPath(images[0]);
    }

    private async Task<(int ModelsCount, int FilesCount)> IngestModelsAndFilesAsync(
        SqliteConnection connection,
        string releaseId,
        string artistId,
        string scanRoot,
        string releaseDir,
        string releaseName,
        string? releaseCoverArt,
        CancellationToken cancellationToken)
    {
        int modelsCount = 0;
        int filesCount = 0;

        // Check if releaseDir has subdirectories that contain 3D files (e.g. scale variants like "1-6 scale", "1-10 scale")
        string[] childDirs;
        try
        {
            childDirs = Directory.GetDirectories(releaseDir);
        }
        catch
        {
            childDirs = [];
        }

        var candidateModelDirs = new List<string>();
        foreach (var cd in childDirs)
        {
            var cdName = new DirectoryInfo(cd).Name;
            if (ArtworkFolderRegex.IsMatch(cdName)) continue;

            if (GetGeometryFiles(cd, SearchOption.AllDirectories).Count > 0)
            {
                candidateModelDirs.Add(cd);
            }
        }

        var modelsToIngest = new List<(string Name, string FolderPath, string? Scale, string? ActiveImage, List<string> GeometryFiles)>();

        if (candidateModelDirs.Count > 0)
        {
            foreach (var modelDir in candidateModelDirs)
            {
                var dirName = new DirectoryInfo(modelDir).Name;
                var scale = DetectScale(dirName) ?? DetectScale(releaseName);
var activeImg = FindBestImageInDirectory(modelDir) ?? releaseCoverArt;
                if (string.IsNullOrWhiteSpace(activeImg) && artworkService is not null)
                {
                    try
                    {
                        var res = await artworkService.ResolveModelArtworkAsync(
                            Guid.NewGuid(), modelDir, null, cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(res.ResolvedPath))
                        {
                            activeImg = res.ResolvedPath;
                        }
                    }
                    catch
                    {
                    }
                }
                var geomFiles = GetGeometryFiles(modelDir, SearchOption.AllDirectories);

                modelsToIngest.Add((dirName, Path.GetFullPath(modelDir), scale, activeImg, geomFiles));
            }
        }
        else
        {
            // Release directory itself acts as the single model container
            var geomFiles = GetGeometryFiles(releaseDir, SearchOption.AllDirectories);
            if (geomFiles.Count > 0)
            {
                var scale = DetectScale(releaseName);
                modelsToIngest.Add((releaseName, Path.GetFullPath(releaseDir), scale, releaseCoverArt, geomFiles));
            }
        }

        var now = DateTime.UtcNow.ToString("o");

        foreach (var m in modelsToIngest)
        {
            var modelId = Guid.NewGuid().ToString("D");

            // Tier 3: Model
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO Models (Id, Name, ScanRoot, ArtistId, ReleaseId, Scale, FolderPath, ActiveImagePath, CreatedAt)
                    VALUES (@Id, @Name, @ScanRoot, @ArtistId, @ReleaseId, @Scale, @FolderPath, @ActiveImagePath, @CreatedAt)
                    ON CONFLICT(Id) DO UPDATE SET
                        Name = excluded.Name,
                        ReleaseId = excluded.ReleaseId,
                        Scale = excluded.Scale,
                        FolderPath = excluded.FolderPath,
                        ActiveImagePath = excluded.ActiveImagePath;
                    """,
                    new
                    {
                        Id = modelId,
                        Name = m.Name,
                        ScanRoot = scanRoot,
                        ArtistId = artistId,
                        ReleaseId = releaseId,
                        Scale = m.Scale,
                        FolderPath = m.FolderPath,
                        ActiveImagePath = m.ActiveImage,
                        CreatedAt = now
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            modelsCount++;

            // Tier 4: Constituent Part Files
            foreach (var gf in m.GeometryFiles)
            {
                var fileInfo = new FileInfo(gf);
                if (!fileInfo.Exists) continue;

                var normalizedPath = Path.GetFullPath(gf);
                var fileId = Guid.NewGuid().ToString("D");
                var ext = fileInfo.Extension.ToLowerInvariant();
                var size = fileInfo.Length;
                var modifiedUtc = fileInfo.LastWriteTimeUtc.ToString("o");

                // Insert into Files
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO Files (Id, ModelId, FileName, FilePath, FileExtension, FileSize)
                        VALUES (@Id, @ModelId, @FileName, @FilePath, @FileExtension, @FileSize)
                        ON CONFLICT(FilePath) DO UPDATE SET
                            ModelId = excluded.ModelId,
                            FileName = excluded.FileName,
                            FileExtension = excluded.FileExtension,
                            FileSize = excluded.FileSize;
                        """,
                        new
                        {
                            Id = fileId,
                            ModelId = modelId,
                            FileName = fileInfo.Name,
                            FilePath = normalizedPath,
                            FileExtension = ext,
                            FileSize = size
                        },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Sync into ModelFiles for backward compatibility and FTS search triggers
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
                        VALUES (@ModelId, @FilePath, @ContentHash, @FileSize, @ModifiedAtUtc, 1)
                        ON CONFLICT(NormalizedPath) DO UPDATE SET
                            ModelId = excluded.ModelId,
                            FileSizeBytes = excluded.FileSizeBytes,
                            ModifiedAtUtc = excluded.ModifiedAtUtc,
                            IsActive = 1;
                        """,
                        new
                        {
                            ModelId = modelId,
                            FilePath = normalizedPath,
                            ContentHash = ComputeQuickHash(normalizedPath, size),
                            FileSize = size,
                            ModifiedAtUtc = modifiedUtc
                        },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                filesCount++;
            }
        }

        return (modelsCount, filesCount);
    }

    private static List<string> GetGeometryFiles(string dir, SearchOption option)
    {
        if (!Directory.Exists(dir)) return [];
        try
        {
            return Directory.GetFiles(dir, "*.*", option)
                .Where(f => GeometryExtensions.Contains(Path.GetExtension(f)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? DetectScale(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var ratioMatch = ScaleRatioRegex.Match(text);
        if (ratioMatch.Success)
        {
            var raw = ratioMatch.Groups["ratio"].Value;
            var parts = Regex.Split(raw, @"[\s_\-:/x]+");
            if (parts.Length == 2)
            {
                return $"1/{parts[1]}";
            }
        }

        var mmMatch = ScaleMmRegex.Match(text);
        if (mmMatch.Success)
        {
            return $"{mmMatch.Groups["val"].Value}{mmMatch.Groups["unit"].Value.ToLowerInvariant()}";
        }

        var namedMatch = ScaleNamedRegex.Match(text);
        if (namedMatch.Success)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(namedMatch.Groups["name"].Value.ToLowerInvariant());
        }

        return null;
    }

    private static string ComputeQuickHash(string filePath, long size)
    {
        var input = $"{filePath}:{size}";
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task<string> UpsertArtistAsync(
        SqliteConnection connection,
        string name,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var existingId = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM Artists WHERE Name = @Name;",
                new { Name = name },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Artists SET FolderPath = COALESCE(@FolderPath, FolderPath) WHERE Id = @Id;",
                    new { Id = existingId, FolderPath = folderPath },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return existingId;
        }

        var newId = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Artists (Id, Name, FolderPath)
                VALUES (@Id, @Name, @FolderPath)
                ON CONFLICT(Name) DO UPDATE SET
                    FolderPath = COALESCE(excluded.FolderPath, Artists.FolderPath);
                """,
                new { Id = newId, Name = name, FolderPath = folderPath },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return newId;
    }

    private static async Task<string> UpsertReleaseAsync(
        SqliteConnection connection,
        string name,
        string artistId,
        string folderPath,
        string? coverImagePath,
        CancellationToken cancellationToken)
    {
        var existingId = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM Releases WHERE Name = @Name AND ArtistId = @ArtistId;",
                new { Name = name, ArtistId = artistId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Releases
                    SET FolderPath = COALESCE(@FolderPath, FolderPath),
                        CoverImagePath = COALESCE(@CoverImagePath, CoverImagePath)
                    WHERE Id = @Id;
                    """,
                    new { Id = existingId, FolderPath = folderPath, CoverImagePath = coverImagePath },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return existingId;
        }

        var newId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow.ToString("o");
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Releases (Id, Name, ArtistId, FolderPath, CoverImagePath, CreatedAt)
                VALUES (@Id, @Name, @ArtistId, @FolderPath, @CoverImagePath, @CreatedAt)
                ON CONFLICT(Name, ArtistId) DO UPDATE SET
                    FolderPath = COALESCE(excluded.FolderPath, Releases.FolderPath),
                    CoverImagePath = COALESCE(excluded.CoverImagePath, Releases.CoverImagePath);
                """,
                new
                {
                    Id = newId,
                    Name = name,
                    ArtistId = artistId,
                    FolderPath = folderPath,
                    CoverImagePath = coverImagePath,
                    CreatedAt = now
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return newId;
    }

    private static async Task UpdateScanStateAsync(
        SqliteConnection connection,
        string folderPath,
        string directoryLastWriteUtc,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO ScanState (FolderPath, LastScannedUtc, DirectoryLastWriteUtc)
                VALUES (@FolderPath, @LastScannedUtc, @DirectoryLastWriteUtc)
                ON CONFLICT(FolderPath) DO UPDATE SET
                    LastScannedUtc = excluded.LastScannedUtc,
                    DirectoryLastWriteUtc = excluded.DirectoryLastWriteUtc;
                """,
                new
                {
                    FolderPath = folderPath,
                    LastScannedUtc = DateTime.UtcNow.ToString("o"),
                    DirectoryLastWriteUtc = directoryLastWriteUtc
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
