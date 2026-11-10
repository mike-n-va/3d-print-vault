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
        var scanCompleted = true;

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
            if (cancellationToken.IsCancellationRequested)
            {
                scanCompleted = false;
                break;
            }
            processedArtists++;

            var artistName = new DirectoryInfo(artistDir).Name;
            var artistNormalized = Path.GetFullPath(artistDir);

            // Tier 1: Artist (idempotent by Name; reactivates retired rows)
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
                if (cancellationToken.IsCancellationRequested)
                {
                    scanCompleted = false;
                    break;
                }

                var releaseNormalized = Path.GetFullPath(releaseDir);
                var releaseName = new DirectoryInfo(releaseDir).Name;
                var releaseLastWrite = Directory.GetLastWriteTimeUtc(releaseNormalized).ToString("o");

                // Check if directory changed in incremental mode
                if (incremental && scanStates.TryGetValue(releaseNormalized, out var prevWrite) && prevWrite == releaseLastWrite)
                {
                    // Unchanged release, skip deep re-indexing
                    continue;
                }

                // Sibling and internal cover art discovery (no DB access; safe outside the transaction)
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

                // Tier 2-4 upserts for this release run in one transaction so a
                // crash mid-release cannot leave partial catalog state behind.
                try
                {
                    using var transaction = connection.BeginTransaction();

                    // Tier 2: Release (idempotent by Name+ArtistId; reactivates retired rows)
                    var releaseId = await UpsertReleaseAsync(
                        connection, transaction, releaseName, artistId, releaseNormalized, coverArtPath, cancellationToken)
                        .ConfigureAwait(false);
                    totalReleases++;

                    // Tier 3 & 4: Models and Constituent Files (idempotent by FolderPath / FilePath)
                    var modelsAdded = await IngestModelsAndFilesAsync(
                        connection, transaction, releaseId, artistId, normalizedRoot, releaseDir, releaseName, coverArtPath, cancellationToken)
                        .ConfigureAwait(false);

                    totalModels += modelsAdded.ModelsCount;
                    totalFiles += modelsAdded.FilesCount;

                    // Update ScanState
                    await UpdateScanStateAsync(connection, transaction, releaseNormalized, releaseLastWrite, cancellationToken)
                        .ConfigureAwait(false);

                    transaction.Commit();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to index release '{releaseDir}': {ex.Message}");
                }

                progress?.Report(new ScanProgressReport(
                    totalArtists, totalReleases, totalModels, totalFiles, releaseDir,
                    (double)processedArtists / artistDirs.Length * 100));
            }

            if (!scanCompleted)
            {
                break;
            }
        }

        // Reconciliation sweep: retire catalog rows whose files/folders no longer
        // exist on disk. Scoped to this scan root so multi-root libraries are not
        // affected by scanning a different root. Skipped when the scan was
        // cancelled, because unvisited folders must not be mistaken for deleted.
        var reconciliation = new ScanReconciliation(0, 0, 0, 0);
        if (scanCompleted)
        {
            try
            {
                reconciliation = await ReconcileAsync(connection, normalizedRoot, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Reconciliation failed: {ex.Message}");
            }
        }

        stopwatch.Stop();
        return new ScanResult(
            totalArtists, totalReleases, totalModels, totalFiles,
            stopwatch.Elapsed, incremental, errors,
            reconciliation.RetiredFiles, reconciliation.RetiredModels,
            reconciliation.RetiredReleases, reconciliation.RetiredArtists);
    }

    /// <summary>
    /// End-of-scan reconciliation. Retires (soft-deletes) rows whose backing
    /// files or folders no longer exist on disk, bottom-up: Files, then Models
    /// with no active files, then Releases with no active models, then Artists
    /// with no active releases. Also drops ScanState rows for folders that have
    /// disappeared. All checks are scoped to the scan root.
    /// </summary>
    private static async Task<ScanReconciliation> ReconcileAsync(
        SqliteConnection connection,
        string scanRoot,
        CancellationToken cancellationToken)
    {
        var root = scanRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // SQL-side root scoping must include the separator boundary, otherwise a
        // scan of "C:\lib" would also reconcile rows under "C:\lib-extra".
        var rootPrefix = root + Path.DirectorySeparatorChar;

        using var transaction = connection.BeginTransaction();

        // 1. Retire Files under the scan root that no longer exist on disk.
        var activeFilePaths = (await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT FilePath FROM Files WHERE IsActive = 1;",
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var missingFiles = activeFilePaths
            .Where(p => IsUnderRoot(root, p, comparison) && !File.Exists(p))
            .ToList();

        if (missingFiles.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Files SET IsActive = 0 WHERE FilePath = @Path;",
                missingFiles.Select(p => new { Path = p }),
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        // 2. Retire Models under the scan root with no remaining active files.
        // The separator boundary prevents a scan of "C:\lib" from touching
        // rows under sibling roots such as "C:\lib-extra".
        var retiredModels = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Models SET IsActive = 0
            WHERE IsActive = 1
              AND FolderPath IS NOT NULL
              AND substr(FolderPath, 1, @RootPrefixLength) = @RootPrefix
              AND NOT EXISTS (SELECT 1 FROM Files f WHERE f.ModelId = Models.Id AND f.IsActive = 1);
            """,
            new { RootPrefix = rootPrefix, RootPrefixLength = rootPrefix.Length },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // 3. Retire Releases under the scan root with no remaining active models.
        var retiredReleases = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Releases SET IsActive = 0
            WHERE IsActive = 1
              AND FolderPath IS NOT NULL
              AND substr(FolderPath, 1, @RootPrefixLength) = @RootPrefix
              AND NOT EXISTS (SELECT 1 FROM Models m WHERE m.ReleaseId = Releases.Id AND m.IsActive = 1);
            """,
            new { RootPrefix = rootPrefix, RootPrefixLength = rootPrefix.Length },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // 4. Retire Artists under the scan root with no remaining active releases.
        var retiredArtists = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Artists SET IsActive = 0
            WHERE IsActive = 1
              AND substr(FolderPath, 1, @RootPrefixLength) = @RootPrefix
              AND NOT EXISTS (SELECT 1 FROM Releases r WHERE r.ArtistId = Artists.Id AND r.IsActive = 1);
            """,
            new { RootPrefix = rootPrefix, RootPrefixLength = rootPrefix.Length },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // 5. Drop ScanState rows for folders that have disappeared.
        var scanStatePaths = (await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT FolderPath FROM ScanState;",
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var staleScanStates = scanStatePaths
            .Where(p => IsUnderRoot(root, p, comparison) && !Directory.Exists(p))
            .ToList();

        if (staleScanStates.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM ScanState WHERE FolderPath = @Path;",
                staleScanStates.Select(p => new { Path = p }),
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        transaction.Commit();

        return new ScanReconciliation(missingFiles.Count, retiredModels, retiredReleases, retiredArtists);
    }

    private static bool IsUnderRoot(string root, string path, StringComparison comparison)
    {
        return path.Length > root.Length
            && (path[root.Length] == Path.DirectorySeparatorChar || path[root.Length] == Path.AltDirectorySeparatorChar)
            && path.StartsWith(root, comparison);
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
        SqliteTransaction transaction,
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
            // Mixed layout support (Defect 1.5): loose geometry files sitting directly in releaseDir root
            var rootGeomFiles = GetGeometryFiles(releaseDir, SearchOption.TopDirectoryOnly);
            if (rootGeomFiles.Count > 0)
            {
                var scale = DetectScale(releaseName);
                var activeImg = releaseCoverArt;
                if (string.IsNullOrWhiteSpace(activeImg) && artworkService is not null)
                {
                    try
                    {
                        var res = await artworkService.ResolveModelArtworkAsync(Guid.NewGuid(), releaseDir, null, cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(res.ResolvedPath))
                        {
                            activeImg = res.ResolvedPath;
                        }
                    }
                    catch
                    {
                    }
                }

                modelsToIngest.Add((releaseName, Path.GetFullPath(releaseDir), scale, activeImg, rootGeomFiles));
            }

            foreach (var modelDir in candidateModelDirs)
            {
                var dirName = new DirectoryInfo(modelDir).Name;
                var scale = DetectScale(dirName) ?? DetectScale(releaseName);
                var activeImg = FindBestImageInDirectory(modelDir) ?? releaseCoverArt;
                if (string.IsNullOrWhiteSpace(activeImg) && artworkService is not null)
                {
                    try
                    {
                        var res = await artworkService.ResolveModelArtworkAsync(Guid.NewGuid(), modelDir, null, cancellationToken).ConfigureAwait(false);
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
            // Tier 3: Model — idempotent upsert keyed on the natural key FolderPath.
            // The pre-v11 behavior minted a fresh GUID per scan and upserted on
            // ON CONFLICT(Id), which could never fire; re-scans therefore duplicated
            // every model row. FolderPath is stable for a given folder, so the row
            // identity (and its GUID) now survives across scans.
            // ORDER BY IsActive DESC: post-migration data can hold several rows for
            // one folder (one active, the rest retired by the v11 dedup). Reactivating
            // a retired sibling would trip UX_Models_FolderPath, so the active row
            // always wins; a retired row is only revived when no active row exists.
            var modelId = await connection.ExecuteScalarAsync<string>(
                new CommandDefinition(
                    """
                    SELECT Id FROM Models
                    WHERE FolderPath = @FolderPath
                    ORDER BY IsActive DESC, CreatedAt DESC
                    LIMIT 1;
                    """,
                    new { m.FolderPath },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(modelId))
            {
                modelId = Guid.NewGuid().ToString("D");
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO Models (Id, Name, ScanRoot, ArtistId, ReleaseId, Scale, FolderPath, ActiveImagePath, CreatedAt, IsActive, SourcePipeline)
                        VALUES (@Id, @Name, @ScanRoot, @ArtistId, @ReleaseId, @Scale, @FolderPath, @ActiveImagePath, @CreatedAt, 1, 'v2')
                        """,
                        new
                        {
                            Id = modelId,
                            m.Name,
                            ScanRoot = scanRoot,
                            ArtistId = artistId,
                            ReleaseId = releaseId,
                            m.Scale,
                            m.FolderPath,
                            ActiveImagePath = m.ActiveImage,
                            CreatedAt = now
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        UPDATE Models
                        SET Name = @Name,
                            ScanRoot = @ScanRoot,
                            ArtistId = @ArtistId,
                            ReleaseId = @ReleaseId,
                            Scale = @Scale,
                            FolderPath = @FolderPath,
                            ActiveImagePath = @ActiveImagePath,
                            IsActive = 1,
                            SourcePipeline = 'v2'
                        WHERE Id = @Id
                        """,
                        new
                        {
                            Id = modelId,
                            m.Name,
                            ScanRoot = scanRoot,
                            ArtistId = artistId,
                            ReleaseId = releaseId,
                            m.Scale,
                            m.FolderPath,
                            ActiveImagePath = m.ActiveImage
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            modelsCount++;

            // Tier 4: Constituent Part Files (idempotent by FilePath)
            foreach (var gf in m.GeometryFiles)
            {
                var fileInfo = new FileInfo(gf);
                if (!fileInfo.Exists) continue;

                var normalizedPath = Path.GetFullPath(gf);
                var fileId = Guid.NewGuid().ToString("D");
                var ext = fileInfo.Extension.ToLowerInvariant();
                var size = fileInfo.Length;
                var modifiedUtc = fileInfo.LastWriteTimeUtc.ToString("o");
                var contentHash = ComputeContentHash(normalizedPath, size);

                // Insert into Files
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO Files (Id, ModelId, FileName, FilePath, FileExtension, FileSize, ContentHash, IsActive)
                        VALUES (@Id, @ModelId, @FileName, @FilePath, @FileExtension, @FileSize, @ContentHash, 1)
                        ON CONFLICT(FilePath) DO UPDATE SET
                            ModelId = excluded.ModelId,
                            FileName = excluded.FileName,
                            FileExtension = excluded.FileExtension,
                            FileSize = excluded.FileSize,
                            ContentHash = excluded.ContentHash,
                            IsActive = 1
                        """,
                        new
                        {
                            Id = fileId,
                            ModelId = modelId,
                            FileName = fileInfo.Name,
                            FilePath = normalizedPath,
                            FileExtension = ext,
                            FileSize = size,
                            ContentHash = contentHash
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Sync into ModelFiles for backward compatibility and FTS search triggers
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
                        VALUES (@ModelId, @FilePath, @ContentHash, @FileSize, @ModifiedAtUtc, 1)
                        ON CONFLICT(NormalizedPath) DO UPDATE SET
                            ModelId = excluded.ModelId,
                            ContentHash = excluded.ContentHash,
                            FileSizeBytes = excluded.FileSizeBytes,
                            ModifiedAtUtc = excluded.ModifiedAtUtc,
                            IsActive = 1
                        """,
                        new
                        {
                            ModelId = modelId,
                            FilePath = normalizedPath,
                            ContentHash = contentHash,
                            FileSize = size,
                            ModifiedAtUtc = modifiedUtc
                        },
                        transaction: transaction,
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

    /// <summary>
    /// Computes a real content-based hash (SHA-256 of file contents, edge-sampled
    /// for large files) so duplicate detection works across paths and renames.
    /// Falls back to the legacy path+size hash when the file cannot be opened.
    /// </summary>
    private static string ComputeContentHash(string filePath, long size)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return ContentHasher.Hash(stream, size);
        }
        catch (IOException)
        {
            return ComputeQuickHash(filePath, size);
        }
        catch (UnauthorizedAccessException)
        {
            return ComputeQuickHash(filePath, size);
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
                    """
                    UPDATE Artists
                    SET FolderPath = COALESCE(@FolderPath, FolderPath), IsActive = 1
                    WHERE Id = @Id;
                    """,
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
                    FolderPath = COALESCE(excluded.FolderPath, Artists.FolderPath),
                    IsActive = 1;
                """,
                new { Id = newId, Name = name, FolderPath = folderPath },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return newId;
    }

    private static async Task<string> UpsertReleaseAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
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
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Releases
                    SET FolderPath = COALESCE(@FolderPath, FolderPath),
                        CoverImagePath = COALESCE(@CoverImagePath, CoverImagePath),
                        IsActive = 1,
                        SourcePipeline = 'v2'
                    WHERE Id = @Id;
                    """,
                    new { Id = existingId, FolderPath = folderPath, CoverImagePath = coverImagePath },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return existingId;
        }

        var newId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow.ToString("o");
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Releases (Id, Name, ArtistId, FolderPath, CoverImagePath, CreatedAt, IsActive, SourcePipeline)
                VALUES (@Id, @Name, @ArtistId, @FolderPath, @CoverImagePath, @CreatedAt, 1, 'v2')
                ON CONFLICT(Name, ArtistId) DO UPDATE SET
                    FolderPath = COALESCE(excluded.FolderPath, Releases.FolderPath),
                    CoverImagePath = COALESCE(excluded.CoverImagePath, Releases.CoverImagePath),
                    IsActive = 1,
                    SourcePipeline = 'v2';
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
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return newId;
    }

    private static async Task UpdateScanStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
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
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record ScanReconciliation(
        int RetiredFiles,
        int RetiredModels,
        int RetiredReleases,
        int RetiredArtists);
}
