using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using DPV.Domain.Contracts.Catalog;

namespace DPV.Infrastructure.Persistence;

public sealed class CatalogExportService(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : ICatalogExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<string> ExportToCsvAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadActiveRowsAsync(cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.AppendLine("ModelName,FilePath,FileSizeBytes,Artist,Collection,LastModified");
        foreach (var row in rows)
        {
            builder
                .Append('"').Append(EscapeCsv(row.Name)).Append("\",")
                .Append('"').Append(EscapeCsv(row.FilePath)).Append("\",")
                .Append(row.FileSizeBytes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append('"').Append(EscapeCsv(row.ArtistName)).Append("\",")
                .Append('"').Append(EscapeCsv(row.CollectionName)).Append("\",")
                .Append('"').Append(EscapeCsv(row.LastModified)).Append('"')
                .AppendLine();
        }

        return builder.ToString();
    }

    public async Task<string> ExportToJsonAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadActiveRowsAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(rows, JsonOptions);
    }

    private async Task<IReadOnlyList<CatalogExportRow>> LoadActiveRowsAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CatalogExportRow>(
            new CommandDefinition(
                """
                SELECT m.Name,
                       mf.NormalizedPath AS FilePath,
                       mf.FileSizeBytes,
                       mf.ContentHash,
                       ar.Name AS ArtistName,
                       c.Name AS CollectionName,
                       mf.ModifiedAtUtc AS LastModified
                FROM Models m
                JOIN ModelFiles mf ON m.Id = mf.ModelId
                LEFT JOIN Artists ar ON m.ArtistId = ar.Id
                LEFT JOIN Collections c ON m.CollectionId = c.Id
                WHERE mf.IsActive = 1
                ORDER BY mf.NormalizedPath
                """,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static string EscapeCsv(string? value) => value?.Replace("\"", "\"\"", StringComparison.Ordinal) ?? string.Empty;

    private sealed class CatalogExportRow
    {
        public string Name { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public string? ArtistName { get; set; }

        public string? CollectionName { get; set; }

        public string LastModified { get; set; } = string.Empty;
    }
}
