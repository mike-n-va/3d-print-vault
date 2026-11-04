using Dapper;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;

namespace DPV.Infrastructure.Persistence;

public sealed class SqliteModelCatalogQuery(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IModelCatalogQuery
{
    public async Task<IReadOnlyList<CatalogModelSummary>> ListAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return [];
        }

        var scanRoot = PathNormalizer.Normalize(rootPath);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<CatalogListRow>(
            new CommandDefinition(
                """
                SELECT m.Id, m.Name, f.NormalizedPath, f.ContentHash, f.FileSizeBytes, f.IsActive
                FROM Models m
                INNER JOIN ModelFiles f ON f.ModelId = m.Id
                WHERE m.ScanRoot = @ScanRoot
                ORDER BY m.Name, f.NormalizedPath
                """,
                new { ScanRoot = scanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows
            .Select(row => new CatalogModelSummary(
                Guid.Parse(row.Id),
                row.Name,
                row.NormalizedPath,
                row.ContentHash,
                row.FileSizeBytes,
                row.IsActive != 0))
            .ToList();
    }

    private sealed class CatalogListRow
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string NormalizedPath { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public long IsActive { get; set; }
    }
}
