using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Models;

namespace DPV.Infrastructure.Persistence;

public sealed class DapperModelDetailRepository(
    IDatabaseConnectionFactory connectionFactory,
    MigrationRunner migrationRunner) : IModelDetailRepository
{
    public async Task<ModelDetailDto?> GetModelDetailAsync(
        Guid modelId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = modelId.ToString("D");
        var model = await connection.QuerySingleOrDefaultAsync<ModelDetailRow>(
            new CommandDefinition(
                """
                SELECT m.Id, m.Name, m.CreatedAt, a.Name AS ArtistName, c.Name AS CollectionName
                FROM Models m
                LEFT JOIN Artists a ON a.Id = m.ArtistId
                LEFT JOIN Collections c ON c.Id = m.CollectionId
                WHERE m.Id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (model is null)
        {
            return null;
        }

        var files = (await connection.QueryAsync<ModelFileRow>(
            new CommandDefinition(
                """
                SELECT NormalizedPath AS FilePath, FileSizeBytes, ContentHash AS FileHash,
                       ModifiedAtUtc AS LastModified, IsActive
                FROM ModelFiles
                WHERE ModelId = @Id
                ORDER BY NormalizedPath
                """,
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();
        var tags = (await connection.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT t.Name
                FROM Tags t
                INNER JOIN ModelTags mt ON mt.TagId = t.Id
                WHERE mt.ModelId = @Id
                ORDER BY t.Name
                """,
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return new ModelDetailDto(
            Guid.Parse(model.Id),
            model.Name,
            model.ArtistName,
            model.CollectionName,
            tags,
            files.Select(file => new ModelFileDto(
                file.FilePath,
                file.FileSizeBytes,
                file.FileHash,
                ParseTimestamp(file.LastModified),
                file.IsActive != 0)).ToList(),
            ParseTimestamp(model.CreatedAt));
    }

    public async Task UpdateModelMetadataAsync(
        Guid modelId,
        string? artistName,
        string? collectionName,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var artist = ModelMetadataAssignment.NormalizeOptionalName(artistName, nameof(artistName));
        var collection = ModelMetadataAssignment.NormalizeOptionalName(collectionName, nameof(collectionName));
        var normalizedTags = ModelMetadataAssignment.NormalizeTags(tags);
        var id = modelId.ToString("D");

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var artistId = await FindOrCreateAsync(connection, transaction, "Artists", artist, cancellationToken)
            .ConfigureAwait(false);
        var collectionId = await FindOrCreateAsync(connection, transaction, "Collections", collection, cancellationToken)
            .ConfigureAwait(false);
        var updated = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Models
                SET ArtistId = @ArtistId, CollectionId = @CollectionId
                WHERE Id = @Id
                """,
                new { Id = id, ArtistId = artistId, CollectionId = collectionId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (updated == 0)
        {
            throw new InvalidOperationException($"Model '{id}' was not found.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM ModelTags WHERE ModelId = @Id",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var tag in normalizedTags)
        {
            var tagId = await FindOrCreateAsync(connection, transaction, "Tags", tag, cancellationToken)
                .ConfigureAwait(false);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT OR IGNORE INTO ModelTags (ModelId, TagId)
                    VALUES (@ModelId, @TagId)
                    """,
                    new { ModelId = id, TagId = tagId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM Models_Fts
                WHERE rowid IN (SELECT rowid FROM ModelFiles WHERE ModelId = @Id)
                """,
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT f.rowid, f.ModelId, m.Name, f.NormalizedPath, @Tags
                FROM ModelFiles f
                INNER JOIN Models m ON m.Id = f.ModelId
                WHERE f.ModelId = @Id
                """,
                new { Id = id, Tags = string.Join(' ', normalizedTags) },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        transaction.Commit();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<string?> FindOrCreateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string? name,
        CancellationToken cancellationToken)
    {
        if (name is null)
        {
            return null;
        }

        if (tableName is not ("Artists" or "Collections" or "Tags"))
        {
            throw new ArgumentOutOfRangeException(nameof(tableName));
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"INSERT OR IGNORE INTO {tableName} (Id, Name) VALUES (@Id, @Name)",
                new { Id = Guid.NewGuid().ToString("D"), Name = name },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                $"SELECT Id FROM {tableName} WHERE Name = @Name",
                new { Name = name },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static DateTime ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
    }

    private sealed class ModelDetailRow
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? CreatedAt { get; set; }

        public string? ArtistName { get; set; }

        public string? CollectionName { get; set; }
    }

    private sealed class ModelFileRow
    {
        public string FilePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string FileHash { get; set; } = string.Empty;

        public string LastModified { get; set; } = string.Empty;

        public long IsActive { get; set; }
    }
}
