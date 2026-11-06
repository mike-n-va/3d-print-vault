using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class SqliteCatalogStore
{
    public async Task<IReadOnlyList<CatalogFileRecord>> LoadAsync(
        SqliteConnection connection,
        string scanRoot,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<CatalogFileRecord>(
            new CommandDefinition(
                """
                SELECT
                    f.Id AS FileRecordId,
                    m.Id,
                    m.Name,
                    m.ScanRoot,
                    m.HeroPreviewPath,
                    m.ConfidenceScore,
                    f.NormalizedPath,
                    f.ContentHash,
                    f.FileSizeBytes,
                    f.ModifiedAtUtc,
                    f.IsActive,
                    f.FileRole
                FROM Models m
                INNER JOIN ModelFiles f ON f.ModelId = m.Id
                WHERE m.ScanRoot = @ScanRoot
                """,
                new { ScanRoot = scanRoot },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SaveChangesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string scanRoot,
        IReadOnlyList<CatalogFileRecord> changes,
        IReadOnlyList<CatalogErrorRecord> errors,
        CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            string? artistId = null;
            if (!string.IsNullOrWhiteSpace(change.ArtistName))
            {
                artistId = await FindOrCreateAsync(connection, transaction, "Artists", change.ArtistName, cancellationToken).ConfigureAwait(false);
            }

            string? collectionId = null;
            if (!string.IsNullOrWhiteSpace(change.CollectionName))
            {
                collectionId = await FindOrCreateAsync(connection, transaction, "Collections", change.CollectionName, cancellationToken).ConfigureAwait(false);
            }

            var releaseName = !string.IsNullOrWhiteSpace(change.Release) ? change.Release : change.CollectionName;
            string? releaseId = null;
            if (!string.IsNullOrWhiteSpace(releaseName))
            {
                releaseId = await FindOrCreateReleaseAsync(connection, transaction, releaseName, artistId, collectionId, cancellationToken).ConfigureAwait(false);
            }

            var modelParams = new
            {
                change.Id,
                change.Name,
                change.ScanRoot,
                change.HeroPreviewPath,
                change.ConfidenceScore,
                ArtistId = artistId,
                CollectionId = collectionId,
                ReleaseId = releaseId
            };

            switch (change.Change)
            {
                case CatalogChange.Insert:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            INSERT INTO Models (Id, Name, ScanRoot, HeroPreviewPath, ConfidenceScore, ArtistId, CollectionId, ReleaseId, CreatedAt)
                            VALUES (@Id, @Name, @ScanRoot, @HeroPreviewPath, @ConfidenceScore, @ArtistId, @CollectionId, @ReleaseId, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                            ON CONFLICT(Id) DO UPDATE SET
                                Name = excluded.Name,
                                HeroPreviewPath = COALESCE(excluded.HeroPreviewPath, Models.HeroPreviewPath),
                                ConfidenceScore = excluded.ConfidenceScore,
                                ArtistId = COALESCE(Models.ArtistId, excluded.ArtistId),
                                CollectionId = COALESCE(Models.CollectionId, excluded.CollectionId),
                                ReleaseId = COALESCE(Models.ReleaseId, excluded.ReleaseId);
                            """,
                            modelParams,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            INSERT INTO ModelFiles (
                                ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive, FileRole, ConfidenceScore)
                            VALUES (
                                @Id, @NormalizedPath, @ContentHash, @FileSizeBytes, @ModifiedAtUtc, @IsActive, @FileRole, @ConfidenceScore)
                            ON CONFLICT(NormalizedPath) DO UPDATE SET
                                ModelId = excluded.ModelId,
                                ContentHash = excluded.ContentHash,
                                FileSizeBytes = excluded.FileSizeBytes,
                                ModifiedAtUtc = excluded.ModifiedAtUtc,
                                IsActive = excluded.IsActive,
                                FileRole = excluded.FileRole,
                                ConfidenceScore = excluded.ConfidenceScore;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
                case CatalogChange.Update:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE Models
                            SET Name = @Name,
                                HeroPreviewPath = COALESCE(@HeroPreviewPath, HeroPreviewPath),
                                ConfidenceScore = @ConfidenceScore,
                                ArtistId = COALESCE(ArtistId, @ArtistId),
                                CollectionId = COALESCE(CollectionId, @CollectionId),
                                ReleaseId = COALESCE(ReleaseId, @ReleaseId)
                            WHERE Id = @Id;
                            """,
                            modelParams,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE ModelFiles
                            SET ContentHash = @ContentHash,
                                FileSizeBytes = @FileSizeBytes,
                                ModifiedAtUtc = @ModifiedAtUtc,
                                IsActive = @IsActive,
                                FileRole = @FileRole,
                                ConfidenceScore = @ConfidenceScore
                            WHERE NormalizedPath = @NormalizedPath;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
                case CatalogChange.Move:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE Models
                            SET Name = @Name,
                                HeroPreviewPath = COALESCE(@HeroPreviewPath, HeroPreviewPath),
                                ConfidenceScore = @ConfidenceScore,
                                ArtistId = COALESCE(ArtistId, @ArtistId),
                                CollectionId = COALESCE(CollectionId, @CollectionId),
                                ReleaseId = COALESCE(ReleaseId, @ReleaseId)
                            WHERE Id = @Id;
                            """,
                            modelParams,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE ModelFiles
                            SET NormalizedPath = @NormalizedPath,
                                ModelId = @Id,
                                FileSizeBytes = @FileSizeBytes,
                                ModifiedAtUtc = @ModifiedAtUtc,
                                IsActive = @IsActive,
                                FileRole = @FileRole,
                                ConfidenceScore = @ConfidenceScore
                            WHERE (Id = @FileRecordId)
                               OR (COALESCE(@PreviousNormalizedPath, '') != '' AND NormalizedPath = @PreviousNormalizedPath)
                               OR NormalizedPath = @NormalizedPath;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
                case CatalogChange.Inactivate:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE ModelFiles
                            SET IsActive = 0
                            WHERE NormalizedPath = @NormalizedPath;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
            }

            if (change.Tags is { Count: > 0 })
            {
                foreach (var tag in change.Tags)
                {
                    var tagId = await FindOrCreateAsync(connection, transaction, "Tags", tag, cancellationToken).ConfigureAwait(false);
                    if (tagId != null)
                    {
                        await connection.ExecuteAsync(
                            new CommandDefinition(
                                "INSERT OR IGNORE INTO ModelTags (ModelId, TagId) VALUES (@ModelId, @TagId)",
                                new { ModelId = change.Id, TagId = tagId },
                                transaction,
                                cancellationToken: cancellationToken)).ConfigureAwait(false);
                    }
                }
            }
        }

        foreach (var error in errors)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO ImportErrors (ScanRoot, Code, Path, Message, RecordedAtUtc)
                    VALUES (@ScanRoot, @Code, @Path, @Message, @RecordedAtUtc);
                    """,
                    new
                    {
                        ScanRoot = scanRoot,
                        error.Code,
                        error.Path,
                        error.Message,
                        error.RecordedAtUtc
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private static async Task<string?> FindOrCreateReleaseAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? name,
        string? artistId,
        string? collectionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        var existing = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                artistId is null
                    ? "SELECT Id FROM Releases WHERE lower(Name) = lower(@Name) AND ArtistId IS NULL LIMIT 1"
                    : "SELECT Id FROM Releases WHERE lower(Name) = lower(@Name) AND ArtistId = @ArtistId LIMIT 1",
                new { Name = normalized, ArtistId = artistId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var id = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Releases (Id, Name, ArtistId, CollectionId, CreatedAt)
                VALUES (@Id, @Name, @ArtistId, @CollectionId, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                ON CONFLICT(Name, ArtistId) DO UPDATE SET CollectionId = COALESCE(Releases.CollectionId, excluded.CollectionId);
                """,
                new { Id = id, Name = normalized, ArtistId = artistId, CollectionId = collectionId },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return id;
    }

    private static async Task<string?> FindOrCreateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        var existing = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                $"SELECT Id FROM {tableName} WHERE lower(Name) = lower(@Name) LIMIT 1",
                new { Name = normalized },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var id = Guid.NewGuid().ToString("D");
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"INSERT INTO {tableName} (Id, Name) VALUES (@Id, @Name)",
                new { Id = id, Name = normalized },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return id;
    }
}

public enum CatalogChange
{
    None,
    Insert,
    Update,
    Move,
    Inactivate
}

public sealed class CatalogFileRecord
{
    public string? FileRecordId { get; set; }

    public string PreviousNormalizedPath { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ScanRoot { get; set; } = string.Empty;

    public string NormalizedPath { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string ModifiedAtUtc { get; set; } = string.Empty;

    public long IsActive { get; set; }

    public CatalogChange Change { get; set; }

    public int FileRole { get; set; }

    public double ConfidenceScore { get; set; } = 1.0;

    public string? HeroPreviewPath { get; set; }

    public string? ArtistName { get; set; }

    public string? CollectionName { get; set; }

    public string? Release { get; set; }

    public IReadOnlyList<string>? Tags { get; set; }

    public string DiscoveryMethod { get; set; } = "Deterministic";

    public IReadOnlyList<string>? DiscoveryEvidence { get; set; }
}

public sealed record CatalogErrorRecord(string Code, string? Path, string Message, string RecordedAtUtc);
