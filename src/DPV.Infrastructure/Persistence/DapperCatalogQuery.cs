using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Import;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperCatalogQuery(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IModelCatalogQuery
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
        var scanRoot = string.IsNullOrWhiteSpace(parameters.ScanRoot)
            ? null
            : PathNormalizer.Normalize(parameters.ScanRoot);
        var useLike = search is { Length: < 3 };
        var match = BuildMatch(useLike ? null : search, tag);

        var sql = """
            SELECT m.Id, m.Name, mf.NormalizedPath AS FilePath, mf.FileSizeBytes,
                   mf.ModifiedAtUtc AS LastModified, mf.IsActive, mf.ContentHash
            FROM ModelFiles mf
            INNER JOIN Models m ON m.Id = mf.ModelId
            INNER JOIN Models_Fts ON Models_Fts.rowid = mf.rowid
            WHERE (@IncludeInactive = 1 OR mf.IsActive = 1)
              AND (@ScanRoot IS NULL OR m.ScanRoot = @ScanRoot)
            """;
        if (match is not null)
        {
            sql += " AND Models_Fts MATCH @Match";
        }

        if (useLike)
        {
            sql += " AND (m.Name LIKE @Like ESCAPE '\\' OR mf.NormalizedPath LIKE @Like ESCAPE '\\')";
        }

        sql += " ORDER BY mf.ModifiedAtUtc DESC LIMIT @Limit OFFSET @Offset;";

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
                    Match = match,
                    Like = useLike ? ToLike(search!) : null,
                    Limit = limit,
                    Offset = offset
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows
            .Select(row => new ModelSummaryDto(
                Guid.Parse(row.Id),
                row.Name,
                row.FilePath,
                row.FileSizeBytes,
                row.LastModified,
                row.IsActive != 0,
                row.ContentHash))
            .ToList();
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
    }
}
