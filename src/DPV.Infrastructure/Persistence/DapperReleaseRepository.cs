using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Import;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperReleaseRepository(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner,
    IReleaseArtworkService? artworkService = null) : IReleaseCatalogQuery, IReleaseDetailRepository
{
    public async Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesAsync(
        ReleaseQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var limit = parameters.Limit > 0 ? parameters.Limit : 100;
        var offset = parameters.Offset > 0 ? parameters.Offset : 0;
        var search = string.IsNullOrWhiteSpace(parameters.SearchTerm) ? null : parameters.SearchTerm.Trim();
        var searchLike = search is not null ? $"%{search}%" : null;
        var artistName = string.IsNullOrWhiteSpace(parameters.ArtistName) ? null : parameters.ArtistName.Trim();
        var scanRoot = string.IsNullOrWhiteSpace(parameters.ScanRoot)
            ? null
            : PathNormalizer.Normalize(parameters.ScanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var sql = """
            SELECT
                r.Id AS ReleaseId,
                r.Name,
                a.Name AS ArtistName,
                r.Description,
                COUNT(DISTINCT m.Id) AS ModelCount,
                r.CoverImagePath,
                r.FolderPath,
                COALESCE(r.IsFavorite, 0) AS IsFavorite,
                r.ReleaseDate,
                r.ArtistId
            FROM Releases r
            LEFT JOIN Artists a ON a.Id = r.ArtistId
            LEFT JOIN Models m ON m.ReleaseId = r.Id
            WHERE (@OnlyFavorites = 0 OR r.IsFavorite = 1)
              AND (@ArtistId IS NULL OR r.ArtistId = @ArtistId)
              AND (@ArtistName IS NULL OR lower(a.Name) = lower(@ArtistName))
              AND (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot OR m.ScanRoot IS NULL)
              AND (
                  @SearchTerm IS NULL
                  OR r.Name LIKE @SearchLike
                  OR a.Name LIKE @SearchLike
                  OR r.Description LIKE @SearchLike
              )
            GROUP BY r.Id, r.Name, a.Name, r.Description, r.CoverImagePath, r.FolderPath, r.IsFavorite, r.ReleaseDate, r.ArtistId
            ORDER BY r.IsFavorite DESC, r.Name ASC
            LIMIT @Limit OFFSET @Offset;
            """;

        var rows = await connection.QueryAsync<ReleaseRow>(
            new CommandDefinition(
                sql,
                new
                {
                    parameters.OnlyFavorites,
                    ArtistId = parameters.ArtistId?.ToString("D"),
                    ArtistName = artistName,
                    ScanRoot = scanRoot,
                    SearchTerm = search,
                    SearchLike = searchLike,
                    Limit = limit,
                    Offset = offset
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var results = new List<ReleaseSummaryDto>();
        foreach (var row in rows)
        {
            var cover = row.CoverImagePath;
            if (string.IsNullOrWhiteSpace(cover) && artworkService is not null && Guid.TryParse(row.ReleaseId, out var relGuid))
            {
                // Retrieve member model files to resolve cover
                var memberFiles = await connection.QueryAsync<string>(
                    new CommandDefinition(
                        """
                        SELECT mf.NormalizedPath
                        FROM ModelFiles mf
                        INNER JOIN Models m ON m.Id = mf.ModelId
                        WHERE m.ReleaseId = @ReleaseId
                        ORDER BY mf.FileRole ASC
                        LIMIT 10;
                        """,
                        new { ReleaseId = row.ReleaseId },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                cover = await artworkService.ResolveReleaseCoverAsync(relGuid, row.Name, memberFiles, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(cover))
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            "UPDATE Releases SET CoverImagePath = @Cover WHERE Id = @Id;",
                            new { Cover = cover, Id = row.ReleaseId },
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                }
            }

            results.Add(new ReleaseSummaryDto(
                Guid.TryParse(row.ReleaseId, out var g) ? g : Guid.NewGuid(),
                row.Name,
                row.ArtistName,
                row.Description,
                (int)row.ModelCount,
                cover,
                row.IsFavorite != 0,
                row.ReleaseDate,
                Guid.TryParse(row.ArtistId, out var ag) ? ag : null,
                FolderPath: row.FolderPath));
        }

        return results;
    }

    public async Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesByArtistAsync(
        Guid artistId,
        CancellationToken cancellationToken = default)
    {
        return await GetReleasesAsync(
            new ReleaseQueryParameters(ArtistId: artistId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReleaseStatisticsDto> GetReleaseStatisticsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanRoot = string.IsNullOrWhiteSpace(scanRoot)
            ? null
            : PathNormalizer.Normalize(scanRoot);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var sql = """
            SELECT
                COUNT(DISTINCT r.Id) AS TotalReleases,
                COUNT(DISTINCT m.Id) AS TotalModelsInReleases,
                COUNT(DISTINCT CASE WHEN r.IsFavorite = 1 THEN r.Id ELSE NULL END) AS FavoritedReleases
            FROM Releases r
            LEFT JOIN Models m ON m.ReleaseId = r.Id
            WHERE (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot OR m.ScanRoot IS NULL);
            """;

        var statRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                sql,
                new { ScanRoot = normalizedScanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (statRow is null)
        {
            return new ReleaseStatisticsDto(0, 0, 0);
        }

        return new ReleaseStatisticsDto(
            (int)(statRow.TotalReleases ?? 0),
            (int)(statRow.TotalModelsInReleases ?? 0),
            (int)(statRow.FavoritedReleases ?? 0));
    }

    public async Task<ReleaseDetailDto?> GetReleaseDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var idStr = id.ToString("D");

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var releaseRow = await connection.QuerySingleOrDefaultAsync<ReleaseRow>(
            new CommandDefinition(
                """
                SELECT
                    r.Id AS ReleaseId,
                    r.Name,
                    a.Name AS ArtistName,
                    r.Description,
                    COUNT(DISTINCT m.Id) AS ModelCount,
                    r.CoverImagePath,
                    COALESCE(r.IsFavorite, 0) AS IsFavorite,
                    r.ReleaseDate,
                    r.ArtistId
                FROM Releases r
                LEFT JOIN Artists a ON a.Id = r.ArtistId
                LEFT JOIN Models m ON m.ReleaseId = r.Id
                WHERE r.Id = @Id
                GROUP BY r.Id, r.Name, a.Name, r.Description, r.CoverImagePath, r.FolderPath, r.IsFavorite, r.ReleaseDate, r.ArtistId;
                """,
                new { Id = idStr },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (releaseRow is null)
        {
            return null;
        }

        // Fetch member models
        var models = await connection.QueryAsync<ModelSummaryRow>(
            new CommandDefinition(
                """
                SELECT
                    m.Id,
                    m.Name,
                    COALESCE(
                        (SELECT mf2.NormalizedPath FROM ModelFiles mf2 WHERE mf2.ModelId = m.Id AND mf2.FileRole = 1 AND mf2.IsActive = 1 LIMIT 1),
                        (SELECT mf2.NormalizedPath FROM ModelFiles mf2 WHERE mf2.ModelId = m.Id AND mf2.IsActive = 1 ORDER BY mf2.FileRole ASC, mf2.NormalizedPath ASC LIMIT 1),
                        ''
                    ) AS FilePath,
                    COALESCE(SUM(mf.FileSizeBytes), 0) AS FileSizeBytes,
                    COALESCE(MAX(mf.ModifiedAtUtc), m.CreatedAt, '') AS LastModified,
                    MAX(mf.IsActive) AS IsActive,
                    COALESCE(MIN(mf.ContentHash), '') AS ContentHash,
                    COALESCE(m.HeroPreviewPath, (SELECT mf3.NormalizedPath FROM ModelFiles mf3 WHERE mf3.ModelId = m.Id AND (mf3.NormalizedPath LIKE '%.png' OR mf3.NormalizedPath LIKE '%.jpg') LIMIT 1)) AS ThumbnailPath,
                    COALESCE(a.Name, '') AS ArtistName,
                    COALESCE(c.Name, '') AS CollectionName,
                    COUNT(mf.Id) AS AssetCount,
                    COALESCE(m.ConfidenceScore, 1.0) AS ConfidenceScore,
                    COALESCE(m.IsFavorite, 0) AS IsFavorite
                FROM Models m
                INNER JOIN ModelFiles mf ON mf.ModelId = m.Id
                LEFT JOIN Artists a ON a.Id = m.ArtistId
                LEFT JOIN Collections c ON c.Id = m.CollectionId
                WHERE m.ReleaseId = @Id
                  AND mf.IsActive = 1
                GROUP BY m.Id, m.Name, m.HeroPreviewPath, m.ConfidenceScore, m.IsFavorite, m.CreatedAt, a.Name, c.Name
                ORDER BY m.Name ASC;
                """,
                new { Id = idStr },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var modelSummaries = models.Select(m => new ModelSummaryDto(
            Guid.TryParse(m.Id, out var mg) ? mg : Guid.NewGuid(),
            m.Name,
            m.FilePath,
            m.FileSizeBytes,
            m.LastModified,
            m.IsActive != 0,
            m.ContentHash,
            m.ThumbnailPath,
            m.ArtistName,
            m.CollectionName,
            m.AssetCount,
            m.ConfidenceScore,
            m.ConfidenceScore >= 0.8 ? "HighConfidence" : m.ConfidenceScore >= 0.5 ? "MediumConfidence" : "LowConfidence",
            "Deterministic",
            null,
            m.IsFavorite != 0)).ToList();

        var cover = releaseRow.CoverImagePath;
        if (string.IsNullOrWhiteSpace(cover) && artworkService is not null)
        {
            var candidatePaths = modelSummaries.Select(ms => ms.ThumbnailPath ?? ms.FilePath).Where(p => !string.IsNullOrWhiteSpace(p))!;
            cover = await artworkService.ResolveReleaseCoverAsync(id, releaseRow.Name, candidatePaths!, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cover))
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE Releases SET CoverImagePath = @Cover WHERE Id = @Id;",
                        new { Cover = cover, Id = idStr },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }

        return new ReleaseDetailDto(
            id,
            releaseRow.Name,
            releaseRow.ArtistName,
            releaseRow.Description,
            (int)releaseRow.ModelCount,
            cover,
            releaseRow.IsFavorite != 0,
            releaseRow.ReleaseDate,
            modelSummaries,
            Guid.TryParse(releaseRow.ArtistId, out var artistGuid) ? artistGuid : null);
    }

    public async Task ToggleReleaseFavoriteAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Releases
                SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END
                WHERE Id = @Id;
                """,
                new { Id = releaseId.ToString("D") },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateReleaseCoverAsync(
        Guid releaseId,
        string coverPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coverPath);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Releases SET CoverImagePath = @Cover WHERE Id = @Id;",
                new { Cover = coverPath, Id = releaseId.ToString("D") },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateReleaseDescriptionAsync(
        Guid releaseId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Releases SET Description = @Description WHERE Id = @Id;",
                new { Description = description, Id = releaseId.ToString("D") },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class ReleaseRow
    {
        public string ReleaseId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ArtistName { get; set; }
        public string? Description { get; set; }
        public long ModelCount { get; set; }
        public string? CoverImagePath { get; set; }
        public string? FolderPath { get; set; }
        public long IsFavorite { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ArtistId { get; set; }
    }

    private sealed class ModelSummaryRow
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
    }
}
