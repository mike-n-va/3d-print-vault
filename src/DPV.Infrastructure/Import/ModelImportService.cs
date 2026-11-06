using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Import;
using DPV.Domain.Enums;
using DPV.Domain.Models;
using DPV.Infrastructure.Import.Discovery;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Import;

public sealed class ModelImportService(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner,
    SqliteCatalogStore catalogStore,
    IModelDiscoveryEngine? discoveryEngine = null) : IModelImportService
{
    private const int DefaultBatchLimit = 500;
    private readonly IModelDiscoveryEngine _discoveryEngine = discoveryEngine ?? new ModelDiscoveryEngine();

    public async Task<ImportLibraryResult> ImportAsync(
        ImportLibraryRequest request,
        CancellationToken cancellationToken = default,
        IProgress<ImportProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return Incomplete([]);
        }

        if (string.IsNullOrWhiteSpace(request.RootPath) || !Directory.Exists(request.RootPath))
        {
            return Incomplete(
            [
                new ImportError(
                    ImportErrorCodes.IoAccess,
                    request.RootPath,
                    "The scan root could not be read.")
            ]);
        }

        var scanRoot = PathNormalizer.Normalize(request.RootPath);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var existing = await catalogStore.LoadAsync(connection, scanRoot, cancellationToken).ConfigureAwait(false);
        var byPath = existing.ToDictionary(record => record.NormalizedPath, StringComparer.Ordinal);
        var errors = new List<ImportError>();
        var accepted = new List<ObservedFile>();
        var completed = true;
        var discovered = new List<string>();

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(request.RootPath);
        while (pendingDirectories.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completed = false;
                break;
            }

            var directory = pendingDirectories.Pop();
            if (!TryEnumerate(directory, out var files, out var directories, out var failure))
            {
                completed = false;
                errors.Add(failure!);
                continue;
            }

            foreach (var child in directories)
            {
                pendingDirectories.Push(child);
            }

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completed = false;
                    break;
                }

                discovered.Add(file);
            }
        }

        progress?.Report(new ImportProgress(0, discovered.Count, "Scanning"));
        for (var index = 0; index < discovered.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completed = false;
                break;
            }

            ObserveFile(discovered[index], scanRoot, request.MaxFileSizeBytes, accepted, errors);
            progress?.Report(new ImportProgress(index + 1, discovered.Count, "Scanning"));
        }

        // Convert accepted files into DiscoveredAsset domain records
        var discoveredAssets = accepted.Select(a => new DiscoveredAsset(
            FilePath: a.FilePath,
            NormalizedPath: a.NormalizedPath,
            FileName: Path.GetFileName(a.FilePath),
            FileExtension: Path.GetExtension(a.FilePath),
            FileSizeBytes: a.FileSizeBytes,
            ModifiedAtUtc: DateTime.TryParse(a.ModifiedAtUtc, out var dt) ? dt : DateTime.UtcNow,
            ContentHash: a.ContentHash,
            Category: a.Category,
            Role: a.Role)).ToList();

        // Multi-stage Model Discovery & Semantic Clustering
        var clusters = _discoveryEngine.DiscoverModels(scanRoot, discoveredAssets);

        var skipped = 0;
        var added = 0;
        var updated = 0;
        var moved = 0;
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cluster in clusters)
        {
            // If any asset in this cluster already exists in the catalog, preserve the existing ModelId
            var existingId = cluster.Assets
                .Where(a => byPath.TryGetValue(a.NormalizedPath, out var r) && !string.IsNullOrWhiteSpace(r.Id))
                .Select(a => byPath[a.NormalizedPath].Id)
                .FirstOrDefault();

            // If not found by path, check if any asset in this cluster is a moved file (same content hash)
            if (string.IsNullOrWhiteSpace(existingId))
            {
                existingId = cluster.Assets
                    .Select(a => byPath.Values.FirstOrDefault(r => r.ContentHash == a.ContentHash && !seenPaths.Contains(r.NormalizedPath))?.Id)
                    .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
            }

            var clusterModelId = existingId ?? (cluster.Id != Guid.Empty ? cluster.Id.ToString("D") : Guid.NewGuid().ToString("D"));

            // Filter for 3D printable files to track in ModelFiles
            var printableAssets = cluster.Assets
                .Where(a => a.Category is AssetCategory.Geometry or AssetCategory.SlicingPayload)
                .ToList();

            foreach (var asset in printableAssets)
            {
                seenPaths.Add(asset.NormalizedPath);

                if (byPath.TryGetValue(asset.NormalizedPath, out var current))
                {
                    var sameContent = current.ContentHash == asset.ContentHash
                        && current.ModifiedAtUtc == asset.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture);

                    if (sameContent)
                    {
                        skipped++;
                        if (current.IsActive == 0)
                        {
                            current.IsActive = 1;
                            current.Change = CatalogChange.Update;
                        }
                    }
                    else
                    {
                        current.ContentHash = asset.ContentHash;
                        current.FileSizeBytes = asset.FileSizeBytes;
                        current.ModifiedAtUtc = asset.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                        current.IsActive = 1;
                        current.Change = CatalogChange.Update;
                        updated++;
                    }

                    current.Id = clusterModelId;
                    current.Name = cluster.DisplayName;
                    current.HeroPreviewPath = cluster.HeroPreviewPath;
                    current.ConfidenceScore = cluster.ConfidenceScore;
                    current.FileRole = (int)asset.Role;
                    current.ArtistName = cluster.InferredArtist;
                    current.CollectionName = cluster.InferredCollection;
                    current.Release = cluster.InferredRelease;
                    current.Tags = cluster.Tags;
                    current.DiscoveryMethod = cluster.DiscoveryMethod;
                    current.DiscoveryEvidence = cluster.DiscoveryEvidence;
                    continue;
                }

                // Check candidates for moved file
                var candidates = byPath.Values
                    .Where(record => record.ContentHash == asset.ContentHash && !seenPaths.Contains(record.NormalizedPath))
                    .ToList();

                if (candidates.Count == 1)
                {
                    var movedRecord = candidates[0];
                    var oldNormalizedPath = movedRecord.NormalizedPath;
                    byPath.Remove(oldNormalizedPath);
                    movedRecord.PreviousNormalizedPath = oldNormalizedPath;
                    movedRecord.Id = clusterModelId;
                    movedRecord.Name = cluster.DisplayName;
                    movedRecord.NormalizedPath = asset.NormalizedPath;
                    movedRecord.FileSizeBytes = asset.FileSizeBytes;
                    movedRecord.ModifiedAtUtc = asset.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                    movedRecord.IsActive = 1;
                    movedRecord.Change = CatalogChange.Move;
                    movedRecord.HeroPreviewPath = cluster.HeroPreviewPath;
                    movedRecord.ConfidenceScore = cluster.ConfidenceScore;
                    movedRecord.FileRole = (int)asset.Role;
                    movedRecord.ArtistName = cluster.InferredArtist;
                    movedRecord.CollectionName = cluster.InferredCollection;
                    movedRecord.Release = cluster.InferredRelease;
                    movedRecord.Tags = cluster.Tags;
                    movedRecord.DiscoveryMethod = cluster.DiscoveryMethod;
                    movedRecord.DiscoveryEvidence = cluster.DiscoveryEvidence;
                    byPath[movedRecord.NormalizedPath] = movedRecord;
                    seenPaths.Add(movedRecord.NormalizedPath);
                    moved++;
                    continue;
                }

                var inserted = new CatalogFileRecord
                {
                    Id = clusterModelId,
                    Name = cluster.DisplayName,
                    ScanRoot = scanRoot,
                    NormalizedPath = asset.NormalizedPath,
                    ContentHash = asset.ContentHash,
                    FileSizeBytes = asset.FileSizeBytes,
                    ModifiedAtUtc = asset.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    IsActive = 1,
                    Change = CatalogChange.Insert,
                    HeroPreviewPath = cluster.HeroPreviewPath,
                    ConfidenceScore = cluster.ConfidenceScore,
                    FileRole = (int)asset.Role,
                    ArtistName = cluster.InferredArtist,
                    CollectionName = cluster.InferredCollection,
                    Release = cluster.InferredRelease,
                    Tags = cluster.Tags,
                    DiscoveryMethod = cluster.DiscoveryMethod,
                    DiscoveryEvidence = cluster.DiscoveryEvidence
                };
                byPath[inserted.NormalizedPath] = inserted;
                added++;
            }
        }

        var inactivated = 0;
        if (completed)
        {
            foreach (var record in byPath.Values)
            {
                if (record.Change is CatalogChange.Insert or CatalogChange.Move or CatalogChange.Update)
                {
                    continue;
                }

                if (seenPaths.Contains(record.NormalizedPath) || File.Exists(record.NormalizedPath) || record.IsActive == 0)
                {
                    continue;
                }

                record.IsActive = 0;
                record.Change = CatalogChange.Inactivate;
                inactivated++;
            }
        }

        var changes = byPath.Values.Where(record => record.Change != CatalogChange.None).ToList();
        var recordedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var errorRecords = errors
            .Select(error => new CatalogErrorRecord(error.Code, error.Path, error.Message, recordedAt))
            .ToList();

        await PersistAsync(connection, scanRoot, changes, errorRecords, request.BatchLimit, cancellationToken)
            .ConfigureAwait(false);

        return new ImportLibraryResult(skipped, added, updated, moved, inactivated, completed, errors);
    }

    private async Task PersistAsync(
        SqliteConnection connection,
        string scanRoot,
        List<CatalogFileRecord> changes,
        List<CatalogErrorRecord> errors,
        int batchLimit,
        CancellationToken cancellationToken)
    {
        var size = batchLimit > 0 ? batchLimit : DefaultBatchLimit;
        var errorIndex = 0;
        for (var index = 0; index < changes.Count || errorIndex < errors.Count; index += size)
        {
            var changeBatch = changes.Skip(index).Take(size).ToList();
            var errorBatch = errorIndex < errors.Count
                ? errors.Skip(errorIndex).Take(size).ToList()
                : [];
            errorIndex += errorBatch.Count;
            if (changeBatch.Count == 0 && errorBatch.Count == 0)
            {
                break;
            }

            using var transaction = connection.BeginTransaction();
            await catalogStore.SaveChangesAsync(
                connection,
                transaction,
                scanRoot,
                changeBatch,
                errorBatch,
                cancellationToken).ConfigureAwait(false);
            transaction.Commit();
        }
    }

    private static void ObserveFile(
        string filePath,
        string scanRoot,
        long maxFileSizeBytes,
        List<ObservedFile> accepted,
        List<ImportError> errors)
    {
        string normalized;
        try
        {
            normalized = PathNormalizer.Normalize(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add(new ImportError(ImportErrorCodes.IoAccess, filePath, exception.Message));
            return;
        }

        if (!PathNormalizer.IsUnderRoot(scanRoot, normalized))
        {
            errors.Add(new ImportError(
                ImportErrorCodes.IoAccess,
                normalized,
                "The file path is outside the scan root."));
            return;
        }

        if (!AssetRoleClassifier.IsSupportedAssetExtension(filePath))
        {
            return;
        }

        FileInfo info;
        try
        {
            info = new FileInfo(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add(new ImportError(ImportErrorCodes.IoAccess, normalized, exception.Message));
            return;
        }

        if (info.Length <= 0)
        {
            return;
        }

        if (info.Length > maxFileSizeBytes)
        {
            AddSizeDisposition(StlCandidateDisposition.TooLarge, normalized, errors);
            return;
        }

        var (category, role) = AssetRoleClassifier.Classify(filePath);

        // For STL files, enforce minimum byte requirement and binary header classification
        if (filePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
        {
            if (info.Length < StlFileInspector.MinimumAcceptedBytes)
            {
                AddSizeDisposition(StlCandidateDisposition.InvalidFormat, normalized, errors);
                return;
            }

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);

                if (stream.Length != info.Length)
                {
                    errors.Add(new ImportError(
                        ImportErrorCodes.HashFailure,
                        normalized,
                        "The file changed while it was being read."));
                    return;
                }

                var header = new byte[Math.Min(StlFileInspector.MinimumAcceptedBytes, stream.Length)];
                var read = stream.Read(header, 0, header.Length);
                var disposition = StlFileInspector.Classify(
                    filePath,
                    stream.Length,
                    maxFileSizeBytes,
                    header.AsSpan(0, read));

                if (disposition != StlCandidateDisposition.Eligible)
                {
                    AddSizeDisposition(disposition, normalized, errors);
                    return;
                }

                stream.Position = 0;
                var hash = ContentHasher.Hash(stream, stream.Length);
                var name = Path.GetFileNameWithoutExtension(filePath).Trim();
                if (name.Length == 0)
                {
                    name = "model";
                }

                accepted.Add(new ObservedFile(
                    normalized,
                    hash,
                    stream.Length,
                    File.GetLastWriteTimeUtc(filePath).ToString("O", CultureInfo.InvariantCulture),
                    name,
                    filePath,
                    category,
                    role));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add(new ImportError(ImportErrorCodes.IoAccess, normalized, exception.Message));
            }
        }
        else if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var hasPrintable = archive.Entries.Any(e =>
                {
                    if (string.IsNullOrWhiteSpace(e.Name))
                    {
                        return false;
                    }

                    var (cat, _) = AssetRoleClassifier.Classify(e.Name);
                    return cat is AssetCategory.Geometry or AssetCategory.SlicingPayload;
                });

                if (!hasPrintable)
                {
                    return; // Ignore archives without printable 3D models (e.g. docs-only or non-model archives)
                }

                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);

                if (stream.Length != info.Length)
                {
                    errors.Add(new ImportError(
                        ImportErrorCodes.HashFailure,
                        normalized,
                        "The file changed while it was being read."));
                    return;
                }

                var hash = ContentHasher.Hash(stream, stream.Length);
                var name = Path.GetFileNameWithoutExtension(filePath).Trim();
                if (name.Length == 0)
                {
                    name = "archive";
                }

                accepted.Add(new ObservedFile(
                    normalized,
                    hash,
                    stream.Length,
                    File.GetLastWriteTimeUtc(filePath).ToString("O", CultureInfo.InvariantCulture),
                    name,
                    filePath,
                    category,
                    role));
            }
            catch (InvalidDataException)
            {
                errors.Add(new ImportError(
                    ImportErrorCodes.InvalidFormat,
                    normalized,
                    "The zip archive is invalid or corrupted."));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add(new ImportError(ImportErrorCodes.IoAccess, normalized, exception.Message));
            }
        }
        else
        {
            // Non-STL supported asset (OBJ, 3MF, images, documentation, sidecars)
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);

                var hash = ContentHasher.Hash(stream, stream.Length);
                var name = Path.GetFileNameWithoutExtension(filePath).Trim();
                if (name.Length == 0)
                {
                    name = "asset";
                }

                accepted.Add(new ObservedFile(
                    normalized,
                    hash,
                    stream.Length,
                    File.GetLastWriteTimeUtc(filePath).ToString("O", CultureInfo.InvariantCulture),
                    name,
                    filePath,
                    category,
                    role));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add(new ImportError(ImportErrorCodes.IoAccess, normalized, exception.Message));
            }
        }
    }

    private static void AddSizeDisposition(
        StlCandidateDisposition disposition,
        string normalizedPath,
        List<ImportError> errors)
    {
        switch (disposition)
        {
            case StlCandidateDisposition.TooLarge:
                errors.Add(new ImportError(
                    ImportErrorCodes.FileTooLarge,
                    normalizedPath,
                    "The file exceeds the maximum allowed size."));
                break;
            case StlCandidateDisposition.InvalidFormat:
                errors.Add(new ImportError(
                    ImportErrorCodes.InvalidFormat,
                    normalizedPath,
                    "The file does not match a supported STL structure."));
                break;
        }
    }

    private static bool TryEnumerate(
        string directory,
        out List<string> files,
        out List<string> directories,
        out ImportError? failure)
    {
        files = [];
        directories = [];
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = false,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false
            };
            files = Directory.EnumerateFiles(directory, "*", options).ToList();
            directories = Directory.EnumerateDirectories(directory, "*", options).ToList();
            failure = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = new ImportError(ImportErrorCodes.IoAccess, directory, exception.Message);
            return false;
        }
    }

    private static ImportLibraryResult Incomplete(IReadOnlyList<ImportError> errors) =>
        new(0, 0, 0, 0, 0, false, errors);

    private sealed record ObservedFile(
        string NormalizedPath,
        string ContentHash,
        long FileSizeBytes,
        string ModifiedAtUtc,
        string Name,
        string FilePath,
        AssetCategory Category,
        FileRole Role);
}
