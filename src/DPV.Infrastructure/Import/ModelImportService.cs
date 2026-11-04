using System.Globalization;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Import;

public sealed class ModelImportService(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner,
    SqliteCatalogStore catalogStore) : IModelImportService
{
    private const int DefaultBatchLimit = 500;

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

        var skipped = 0;
        var added = 0;
        var updated = 0;
        var moved = 0;
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var pendingNew = new List<ObservedFile>();

        foreach (var observation in accepted)
        {
            seenPaths.Add(observation.NormalizedPath);
            if (!byPath.TryGetValue(observation.NormalizedPath, out var current))
            {
                pendingNew.Add(observation);
                continue;
            }

            var sameContent = current.ContentHash == observation.ContentHash
                && current.ModifiedAtUtc == observation.ModifiedAtUtc;
            if (sameContent)
            {
                skipped++;
                if (current.IsActive == 0)
                {
                    current.IsActive = 1;
                    current.Change = CatalogChange.Update;
                }

                continue;
            }

            current.ContentHash = observation.ContentHash;
            current.FileSizeBytes = observation.FileSizeBytes;
            current.ModifiedAtUtc = observation.ModifiedAtUtc;
            current.IsActive = 1;
            current.Change = CatalogChange.Update;
            updated++;
        }

        foreach (var observation in pendingNew)
        {
            var candidates = byPath.Values
                .Where(record =>
                    record.ContentHash == observation.ContentHash
                    && !seenPaths.Contains(record.NormalizedPath))
                .ToList();
            if (candidates.Count == 1)
            {
                var current = candidates[0];
                byPath.Remove(current.NormalizedPath);
                current.Name = observation.Name;
                current.NormalizedPath = observation.NormalizedPath;
                current.FileSizeBytes = observation.FileSizeBytes;
                current.ModifiedAtUtc = observation.ModifiedAtUtc;
                current.IsActive = 1;
                current.Change = CatalogChange.Move;
                byPath[current.NormalizedPath] = current;
                seenPaths.Add(current.NormalizedPath);
                moved++;
                continue;
            }

            var inserted = new CatalogFileRecord
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = observation.Name,
                ScanRoot = scanRoot,
                NormalizedPath = observation.NormalizedPath,
                ContentHash = observation.ContentHash,
                FileSizeBytes = observation.FileSizeBytes,
                ModifiedAtUtc = observation.ModifiedAtUtc,
                IsActive = 1,
                Change = CatalogChange.Insert
            };
            byPath[inserted.NormalizedPath] = inserted;
            added++;
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

        if (!StlFileInspector.IsStlExtension(filePath))
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
            string hash;
            try
            {
                hash = ContentHasher.Hash(stream, stream.Length);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add(new ImportError(ImportErrorCodes.HashFailure, normalized, exception.Message));
                return;
            }

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
                name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add(new ImportError(ImportErrorCodes.IoAccess, normalized, exception.Message));
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
        string Name);
}
