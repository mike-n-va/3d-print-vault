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
                    m.Id,
                    m.Name,
                    m.ScanRoot,
                    f.NormalizedPath,
                    f.ContentHash,
                    f.FileSizeBytes,
                    f.ModifiedAtUtc,
                    f.IsActive
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
            switch (change.Change)
            {
                case CatalogChange.Insert:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            INSERT INTO Models (Id, Name, ScanRoot, CreatedAt)
                            VALUES (@Id, @Name, @ScanRoot, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            INSERT INTO ModelFiles (
                                ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
                            VALUES (
                                @Id, @NormalizedPath, @ContentHash, @FileSizeBytes, @ModifiedAtUtc, @IsActive);
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
                case CatalogChange.Update:
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE ModelFiles
                            SET ContentHash = @ContentHash,
                                FileSizeBytes = @FileSizeBytes,
                                ModifiedAtUtc = @ModifiedAtUtc,
                                IsActive = @IsActive
                            WHERE ModelId = @Id;
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
                            SET Name = @Name
                            WHERE Id = @Id;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE ModelFiles
                            SET NormalizedPath = @NormalizedPath,
                                FileSizeBytes = @FileSizeBytes,
                                ModifiedAtUtc = @ModifiedAtUtc,
                                IsActive = @IsActive
                            WHERE ModelId = @Id;
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
                            WHERE ModelId = @Id;
                            """,
                            change,
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    break;
            }
        }

        if (errors.Count > 0)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO ImportErrors (ScanRoot, Code, Path, Message, RecordedAtUtc)
                    VALUES (@ScanRoot, @Code, @Path, @Message, @RecordedAtUtc);
                    """,
                    errors.Select(error => new
                    {
                        ScanRoot = scanRoot,
                        error.Code,
                        error.Path,
                        error.Message,
                        error.RecordedAtUtc
                    }),
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
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
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ScanRoot { get; set; } = string.Empty;

    public string NormalizedPath { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string ModifiedAtUtc { get; set; } = string.Empty;

    public long IsActive { get; set; }

    public CatalogChange Change { get; set; }
}

public sealed record CatalogErrorRecord(string Code, string? Path, string Message, string RecordedAtUtc);
