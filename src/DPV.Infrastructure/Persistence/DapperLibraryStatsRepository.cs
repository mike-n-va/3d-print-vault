using Dapper;
using DPV.Domain.Contracts.Catalog;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperLibraryStatsRepository(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : ILibraryStatsRepository
{
    public const int TopLimit = 10;

    public async Task<LibraryStatsDto> GetLibraryStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

        var basicStats = await connection.QuerySingleAsync<BasicStatsRow>(
            new CommandDefinition(
                """
                SELECT
                    (SELECT COUNT(*) FROM Models) AS TotalModels,
                    (SELECT COUNT(*) FROM ModelFiles WHERE IsActive = 1) AS TotalFiles,
                    (SELECT COALESCE(SUM(FileSizeBytes), 0) FROM ModelFiles WHERE IsActive = 1) AS TotalBytesUsed,
                    (SELECT COUNT(*) FROM Artists) AS TotalArtists,
                    (SELECT COUNT(*) FROM Collections) AS TotalCollections,
                    (SELECT COUNT(*) FROM Tags) AS TotalTags
                """,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var topArtists = await QueryRankingsAsync(
            connection,
            """
            SELECT a.Name, CAST(COUNT(m.Id) AS INTEGER) AS ModelCount
            FROM Artists a
            LEFT JOIN Models m ON m.ArtistId = a.Id
            GROUP BY a.Id, a.Name
            ORDER BY ModelCount DESC, a.Name
            LIMIT @Limit
            """,
            cancellationToken).ConfigureAwait(false);
        var topCollections = await QueryRankingsAsync(
            connection,
            """
            SELECT c.Name, CAST(COUNT(m.Id) AS INTEGER) AS ModelCount
            FROM Collections c
            LEFT JOIN Models m ON m.CollectionId = c.Id
            GROUP BY c.Id, c.Name
            ORDER BY ModelCount DESC, c.Name
            LIMIT @Limit
            """,
            cancellationToken).ConfigureAwait(false);
        var topTags = await QueryRankingsAsync(
            connection,
            """
            SELECT t.Name, CAST(COUNT(DISTINCT mt.ModelId) AS INTEGER) AS ModelCount
            FROM Tags t
            LEFT JOIN ModelTags mt ON mt.TagId = t.Id
            GROUP BY t.Id, t.Name
            ORDER BY ModelCount DESC, t.Name
            LIMIT @Limit
            """,
            cancellationToken).ConfigureAwait(false);

        return new LibraryStatsDto(
            basicStats.TotalModels,
            basicStats.TotalFiles,
            basicStats.TotalBytesUsed,
            checked((int)basicStats.TotalArtists),
            checked((int)basicStats.TotalCollections),
            checked((int)basicStats.TotalTags),
            topArtists.Select(row => new TopArtistDto(row.Name, checked((int)row.ModelCount))).ToList(),
            topCollections.Select(row => new TopCollectionDto(row.Name, checked((int)row.ModelCount))).ToList(),
            topTags.Select(row => new TopTagDto(row.Name, checked((int)row.ModelCount))).ToList());
    }

    private static async Task<IReadOnlyList<NameCountRow>> QueryRankingsAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<NameCountRow>(
            new CommandDefinition(sql, new { Limit = TopLimit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private sealed class NameCountRow
    {
        public string Name { get; set; } = string.Empty;

        public long ModelCount { get; set; }
    }

    private sealed class BasicStatsRow
    {
        public long TotalModels { get; set; }

        public long TotalFiles { get; set; }

        public long TotalBytesUsed { get; set; }

        public long TotalArtists { get; set; }

        public long TotalCollections { get; set; }

        public long TotalTags { get; set; }
    }
}
