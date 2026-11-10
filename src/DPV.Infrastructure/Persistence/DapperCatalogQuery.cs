using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Import;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperCatalogQuery(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IModelCatalogQuery, IModelGroupingQuery
{
    public async Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(
        ModelQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var limit = parameters.Limit > 0 ? parameters.Limit : 100;
        var offset = parameters.Offset > 0 ? parameters.Offset : 0;
        var search = string.IsNullOrWhiteSpace(parameters.SearchTerm) ? null : parameters.SearchTerm.Trim();
        var tag = string.IsNullOrWhiteSpace(parameters.TagFilter) || parameters.TagFilter.Trim() == "All"
            ? null
            : parameters.TagFilter.Trim();
        var artist = string.IsNullOrWhiteSpace(parameters.ArtistFilter) ? null : parameters.ArtistFilter.Trim();
        var collectionFilter = string.IsNullOrWhiteSpace(parameters.CollectionFilter) ? null : parameters.CollectionFilter.Trim();
        var scanRoot = string.IsNullOrWhiteSpace(parameters.ScanRoot)
            ? null
            : PathNormalizer.Normalize(parameters.ScanRoot);
        var useLike = search is { Length: < 3 };
        var match = BuildMatch(useLike ? null : search, tag);

        var sql = """
            SELECT
                m.Id,
                m.Name,
                m.Scale,
                m.FolderPath,
                m.ActiveImagePath,
                COALESCE(
                    (SELECT mf2.NormalizedPath FROM ModelFiles mf2 WHERE mf2.ModelId = m.Id AND mf2.FileRole = 1 AND (@IncludeInactive = 1 OR mf2.IsActive = 1) LIMIT 1),
                    (SELECT mf2.NormalizedPath FROM ModelFiles mf2 WHERE mf2.ModelId = m.Id AND (@IncludeInactive = 1 OR mf2.IsActive = 1) ORDER BY mf2.FileRole ASC, mf2.NormalizedPath ASC LIMIT 1),
                    ''
                ) AS FilePath,
                COALESCE(SUM(mf.FileSizeBytes), 0) AS FileSizeBytes,
                COALESCE(MAX(mf.ModifiedAtUtc), m.CreatedAt, '') AS LastModified,
                MAX(mf.IsActive) AS IsActive,
                COALESCE(MIN(mf.ContentHash), '') AS ContentHash,
                COALESCE(m.ActiveImagePath, m.HeroPreviewPath, (SELECT mf3.NormalizedPath FROM ModelFiles mf3 WHERE mf3.ModelId = m.Id AND (mf3.NormalizedPath LIKE '%.png' OR mf3.NormalizedPath LIKE '%.jpg') LIMIT 1), r.CoverImagePath) AS ThumbnailPath,
                COALESCE(a.Name, '') AS ArtistName,
                COALESCE(c.Name, r.Name, '') AS CollectionName,
                CASE WHEN EXISTS (SELECT 1 FROM Files f WHERE f.ModelId = m.Id) THEN (SELECT COUNT(*) FROM Files f WHERE f.ModelId = m.Id) ELSE COUNT(mf.Id) END AS AssetCount,
                COALESCE(m.ConfidenceScore, 1.0) AS ConfidenceScore,
                COALESCE(m.IsFavorite, 0) AS IsFavorite
            FROM Models m
            LEFT JOIN ModelFiles mf ON mf.ModelId = m.Id
            LEFT JOIN Artists a ON a.Id = m.ArtistId
            LEFT JOIN Collections c ON c.Id = m.CollectionId
            LEFT JOIN Releases r ON r.Id = m.ReleaseId
            WHERE (@IncludeInactive = 1 OR mf.IsActive IS NULL OR mf.IsActive = 1)
              AND (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
              AND (@ReleaseId IS NULL OR m.ReleaseId = @ReleaseId)
              AND (@ArtistFilter IS NULL OR lower(a.Name) = lower(@ArtistFilter))
              AND (@CollectionFilter IS NULL OR lower(COALESCE(c.Name, r.Name)) = lower(@CollectionFilter))
              AND (@OnlyFavorites = 0 OR m.IsFavorite = 1)
            """;

        if (match is not null)
        {
            sql += " AND (m.Id IN (SELECT model_id FROM Models_Fts WHERE Models_Fts MATCH @Match)";
            if (tag is not null)
            {
                sql += " OR m.Id IN (SELECT mt.ModelId FROM ModelTags mt INNER JOIN Tags t ON t.Id = mt.TagId WHERE lower(t.Name) = lower(@TagFilter))";
            }
            sql += ")";
        }
        else if (tag is not null)
        {
            sql += " AND (m.Id IN (SELECT model_id FROM Models_Fts WHERE tags MATCH @TagFilter) OR m.Id IN (SELECT mt.ModelId FROM ModelTags mt INNER JOIN Tags t ON t.Id = mt.TagId WHERE lower(t.Name) = lower(@TagFilter)))";
        }

        if (useLike)
        {
            sql += " AND (m.Name LIKE @Like ESCAPE '\\' OR mf.NormalizedPath LIKE @Like ESCAPE '\\' OR a.Name LIKE @Like ESCAPE '\\' OR c.Name LIKE @Like ESCAPE '\\' OR r.Name LIKE @Like ESCAPE '\\')";
        }

        sql += """
            GROUP BY m.Id, m.Name, m.Scale, m.FolderPath, m.ActiveImagePath, m.ScanRoot, m.HeroPreviewPath, m.ConfidenceScore, m.CreatedAt, a.Name, c.Name, r.Name, r.CoverImagePath, m.IsFavorite
            ORDER BY MAX(mf.ModifiedAtUtc) DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CatalogSearchRow>(
            new CommandDefinition(
                sql,
                new
                {
                    IncludeInactive = parameters.IncludeInactive ? 1 : 0,
                    ScanRoot = scanRoot,
                    ReleaseId = parameters.ReleaseId?.ToString("D"),
                    Match = match,
                    TagFilter = tag,
                    ArtistFilter = artist,
                    CollectionFilter = collectionFilter,
                    OnlyFavorites = parameters.OnlyFavorites ? 1 : 0,
                    Like = useLike ? ToLike(search!) : null,
                    Limit = limit,
                    Offset = offset
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows
            .Select(row =>
            {
                var tier = row.ConfidenceScore >= 0.80 ? "HighConfidence" : (row.ConfidenceScore >= 0.50 ? "NeedsReview" : "LowConfidence");
                var method = row.ConfidenceScore >= 0.80 ? "Deterministic" : "Heuristic";
                var evidence = new List<string>
                {
                    $"Confidence Score: {row.ConfidenceScore:P0} ({tier})",
                    $"Assets: {row.AssetCount} files"
                };
                if (!string.IsNullOrWhiteSpace(row.ArtistName))
                {
                    evidence.Add($"Artist: {row.ArtistName}");
                }
                if (!string.IsNullOrWhiteSpace(row.CollectionName))
                {
                    evidence.Add($"Collection: {row.CollectionName}");
                }

                return new ModelSummaryDto(
                    Guid.Parse(row.Id),
                    row.Name,
                    row.FilePath,
                    row.FileSizeBytes,
                    row.LastModified,
                    row.IsActive != 0,
                    row.ContentHash,
                    row.ThumbnailPath,
                    string.IsNullOrWhiteSpace(row.ArtistName) ? null : row.ArtistName,
                    string.IsNullOrWhiteSpace(row.CollectionName) ? null : row.CollectionName,
                    row.AssetCount,
                    row.ConfidenceScore,
                    tier,
                    method,
                    evidence,
                    row.IsFavorite != 0,
                    Scale: row.Scale,
                    FolderPath: row.FolderPath,
                    ActiveImagePath: row.ActiveImagePath);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanRoot = string.IsNullOrWhiteSpace(scanRoot)
            ? null
            : PathNormalizer.Normalize(scanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ArtistRow>(
            new CommandDefinition(
                """
                SELECT a.Name, COUNT(DISTINCT m.Id) AS ModelCount, COALESCE(a.IsFavorite, 0) AS IsFavorite, a.Id
                FROM Models m
                INNER JOIN Artists a ON a.Id = m.ArtistId
                WHERE (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
                GROUP BY a.Id, a.Name, a.IsFavorite
                ORDER BY a.Name ASC;
                """,
                new { ScanRoot = normalizedScanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new ArtistGroupDto(

            r.Name,

            r.ModelCount,

            r.IsFavorite != 0,

            Guid.TryParse(r.Id, out var g) ? g : null)).ToList();
    }

    public async Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanRoot = string.IsNullOrWhiteSpace(scanRoot)
            ? null
            : PathNormalizer.Normalize(scanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<CollectionRow>(
            new CommandDefinition(
                """
                SELECT c.Name, COUNT(DISTINCT m.Id) AS ModelCount, c.Description
                FROM Models m
                INNER JOIN Collections c ON c.Id = m.CollectionId
                WHERE (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
                GROUP BY c.Name, c.Description
                ORDER BY c.Name ASC;
                """,
                new { ScanRoot = normalizedScanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new CollectionGroupDto(r.Name, r.ModelCount, r.Description)).ToList();
    }

    public async Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanRoot = string.IsNullOrWhiteSpace(scanRoot)
            ? null
            : PathNormalizer.Normalize(scanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<TagGroupDto>(
            new CommandDefinition(
                """
                SELECT t.Name, COUNT(DISTINCT mt.ModelId) AS ModelCount
                FROM Tags t
                INNER JOIN ModelTags mt ON mt.TagId = t.Id
                INNER JOIN Models m ON m.Id = mt.ModelId
                WHERE (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
                GROUP BY t.Name
                ORDER BY t.Name ASC;
                """,
                new { ScanRoot = normalizedScanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task ToggleArtistFavoriteAsync(

        string artistName,

        CancellationToken cancellationToken = default)

    {

        ArgumentException.ThrowIfNullOrWhiteSpace(artistName);



        await using var connection = connectionFactory.CreateConnection();

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);



        await connection.ExecuteAsync(

            new CommandDefinition(

                """

                UPDATE Artists

                SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END

                WHERE lower(Name) = lower(@Name);

                """,

                new { Name = artistName.Trim() },

                cancellationToken: cancellationToken)).ConfigureAwait(false);

    }



    public async Task<long> GetFavoritesCountAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanRoot = string.IsNullOrWhiteSpace(scanRoot)
            ? null
            : PathNormalizer.Normalize(scanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM Models m
                WHERE m.IsFavorite = 1
                  AND (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot);
                """,
                new { ScanRoot = normalizedScanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string? BuildMatch(string? search, string? tag)
    {
        string? searchExpr = search is null ? null : "\"" + search.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        string? tagExpr = tag is null ? null : "tags : \"" + tag.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        if (searchExpr is not null && tagExpr is not null)
        {
            return searchExpr + " AND " + tagExpr;
        }

        return searchExpr ?? tagExpr;
    }

    private static string ToLike(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }

    private sealed class CatalogSearchRow
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string LastModified { get; set; } = string.Empty;

        public long IsActive { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public string? ThumbnailPath { get; set; }

        public string? ArtistName { get; set; }

        public string? CollectionName { get; set; }

        public int AssetCount { get; set; }

        public double ConfidenceScore { get; set; } = 1.0;

        public long IsFavorite { get; set; }
        public string? Scale { get; set; }
        public string? FolderPath { get; set; }
        public string? ActiveImagePath { get; set; }
    }


    private sealed class CollectionRow
    {
        public string Name { get; set; } = string.Empty;
        public long ModelCount { get; set; }
        public string? Description { get; set; }
    }

    private sealed class ArtistRow

    {

        public string Name { get; set; } = string.Empty;

        public long ModelCount { get; set; }

        public long IsFavorite { get; set; }
        public string? Scale { get; set; }
        public string? FolderPath { get; set; }
        public string? ActiveImagePath { get; set; }

        public string? Id { get; set; }

    }

}