using System.Globalization;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Import;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperDuplicateRepository(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IDuplicateRepository
{
    public async Task<IReadOnlyCollection<DuplicateGroupDto>> GetDuplicateGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = (await connection.QueryAsync<DuplicateRow>(
            new CommandDefinition(
                """
                SELECT f.ContentHash, f.ModelId, m.Name AS ModelName, f.NormalizedPath AS FilePath,
                       f.FileSizeBytes, f.ModifiedAtUtc
                FROM ModelFiles f
                INNER JOIN Models m ON m.Id = f.ModelId
                WHERE f.IsActive = 1
                  AND f.ContentHash IN (
                      SELECT ContentHash
                      FROM ModelFiles
                      WHERE IsActive = 1
                      GROUP BY ContentHash
                      HAVING COUNT(*) > 1)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM DuplicateSuppressions s
                      WHERE s.ContentHash = f.ContentHash)
                ORDER BY f.FileSizeBytes DESC, f.ContentHash, f.NormalizedPath
                """,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return rows
            .GroupBy(row => row.ContentHash, StringComparer.Ordinal)
            .Select(group =>
            {
                var files = group.Select(row =>
                {
                    var modelId = Guid.Parse(row.ModelId);
                    return new DuplicateFileItem(
                        modelId,
                        modelId,
                        row.FilePath,
                        row.FileSizeBytes,
                        ParseTimestamp(row.ModifiedAtUtc),
                        true,
                        row.ModelName);
                }).ToList();
                return new DuplicateGroupDto(
                    group.Key,
                    files.Max(file => file.FileSizeBytes),
                    files.Count,
                    files);
            })
            .OrderByDescending(group => group.FileSizeBytes)
            .ThenBy(group => group.ContentHash, StringComparer.Ordinal)
            .ToList();
    }

    public async Task ResolveDuplicateMergeAsync(
        Guid primaryFileId,
        IEnumerable<Guid> duplicateFileIdsToDeactivate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(duplicateFileIdsToDeactivate);
        var canonical = primaryFileId.ToString("D");
        var duplicateIds = duplicateFileIdsToDeactivate
            .Where(id => id != primaryFileId)
            .Distinct()
            .Select(id => id.ToString("D"))
            .ToArray();
        if (duplicateIds.Length == 0)
        {
            throw new InvalidOperationException("Choose at least one other copy to deactivate.");
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var hash = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT ContentHash FROM ModelFiles WHERE ModelId = @Id AND IsActive = 1",
                new { Id = canonical },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException("The primary file is not an active catalog record.");
        }

        var matched = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM ModelFiles
                WHERE ModelId IN @Ids AND ContentHash = @Hash AND IsActive = 1
                """,
                new { Ids = duplicateIds, Hash = hash },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (matched != duplicateIds.Length)
        {
            throw new InvalidOperationException("Every file to deactivate must be another active copy of the primary hash.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Models
                SET ArtistId = COALESCE(ArtistId, (
                        SELECT donor.ArtistId
                        FROM Models donor
                        WHERE donor.Id IN @Ids AND donor.ArtistId IS NOT NULL
                        LIMIT 1)),
                    CollectionId = COALESCE(CollectionId, (
                        SELECT donor.CollectionId
                        FROM Models donor
                        WHERE donor.Id IN @Ids AND donor.CollectionId IS NOT NULL
                        LIMIT 1))
                WHERE Id = @Canonical
                """,
                new { Ids = duplicateIds, Canonical = canonical },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT OR IGNORE INTO ModelTags (ModelId, TagId)
                SELECT @Canonical, source.TagId
                FROM ModelTags source
                WHERE source.ModelId IN @Ids
                """,
                new { Ids = duplicateIds, Canonical = canonical },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE ModelFiles
                SET IsActive = 0
                WHERE ModelId IN @Ids AND ContentHash = @Hash AND IsActive = 1
                """,
                new { Ids = duplicateIds, Hash = hash },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        await RefreshTagsAsync(connection, transaction, canonical, cancellationToken).ConfigureAwait(false);
        transaction.Commit();
    }

    public async Task ResolveDuplicateIgnoreAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var hash = RequireHash(contentHash);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var known = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM ModelFiles WHERE ContentHash = @Hash",
                new { Hash = hash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (known == 0)
        {
            throw new InvalidOperationException($"Content hash '{hash}' is not in the catalog.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO DuplicateSuppressions (ContentHash, SuppressedAtUtc)
                VALUES (@Hash, @SuppressedAtUtc)
                ON CONFLICT(ContentHash) DO UPDATE SET SuppressedAtUtc = excluded.SuppressedAtUtc
                """,
                new
                {
                    Hash = hash,
                    SuppressedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task ResolveDuplicateDeleteAsync(
        Guid fileIdToDelete,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var id = fileIdToDelete.ToString("D");
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var record = await connection.QuerySingleOrDefaultAsync<DuplicateRow>(
            new CommandDefinition(
                """
                SELECT ContentHash, ModelId, '' AS ModelName, NormalizedPath AS FilePath,
                       FileSizeBytes, ModifiedAtUtc
                FROM ModelFiles
                WHERE ModelId = @Id AND IsActive = 1
                """,
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (record is null)
        {
            throw new InvalidOperationException("The selected file is not an active catalog record.");
        }

        if (!string.Equals(
                PathNormalizer.Normalize(filePath),
                PathNormalizer.Normalize(record.FilePath),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The path does not match the catalog record.");
        }

        var hash = record.ContentHash;
        var activeCopies = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM ModelFiles WHERE ContentHash = @Hash AND IsActive = 1",
                new { Hash = hash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (activeCopies < 2)
        {
            throw new InvalidOperationException("Refusing to delete a file that is not an exact-content duplicate.");
        }

        var fullPath = Path.GetFullPath(record.FilePath);
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException("The catalog path is not a file that can be deleted.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ModelFiles SET IsActive = 0 WHERE ModelId = @Id AND ContentHash = @Hash",
                new { Id = id, Hash = hash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task RefreshTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string modelId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Models_Fts WHERE rowid IN (SELECT rowid FROM ModelFiles WHERE ModelId = @Id)",
                new { Id = modelId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT f.rowid, f.ModelId, m.Name, f.NormalizedPath,
                       COALESCE((
                           SELECT GROUP_CONCAT(t.Name, ' ')
                           FROM ModelTags mt
                           INNER JOIN Tags t ON t.Id = mt.TagId
                           WHERE mt.ModelId = f.ModelId
                       ), '')
                FROM ModelFiles f
                INNER JOIN Models m ON m.Id = f.ModelId
                WHERE f.ModelId = @Id
                """,
                new { Id = modelId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string RequireHash(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return contentHash.Trim();
    }

    private static DateTime ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
    }

    private sealed class DuplicateRow
    {
        public string ContentHash { get; set; } = string.Empty;

        public string ModelId { get; set; } = string.Empty;

        public string ModelName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string ModifiedAtUtc { get; set; } = string.Empty;
    }
}
