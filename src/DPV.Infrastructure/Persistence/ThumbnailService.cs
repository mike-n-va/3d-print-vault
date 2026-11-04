using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Thumbnails;

namespace DPV.Infrastructure.Persistence;

public sealed partial class ThumbnailService : IThumbnailService
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly string _cacheDirectory;

    public ThumbnailService(
        IDatabaseConnectionFactory connectionFactory,
        MigrationRunner migrationRunner,
        string? cacheDirectory = null)
    {
        _connectionFactory = connectionFactory;
        _migrationRunner = migrationRunner;
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DPV",
                "Cache",
                "Thumbnails")
            : cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string?> GetThumbnailPathAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        var thumbnailPath = ThumbnailPath(contentHash);
        if (File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT CacheStatus FROM ThumbnailCache WHERE ContentHash = @ContentHash",
                new { ContentHash = NormalizeHash(contentHash) },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        var placeholder = Path.Combine(_cacheDirectory, "_placeholder.png");
        return status == "Failed" && File.Exists(placeholder) ? placeholder : null;
    }

    public async Task EnsureThumbnailGeneratedAsync(
        string contentHash,
        string stlFilePath,
        CancellationToken cancellationToken = default)
    {
        var hash = NormalizeHash(contentHash);
        var thumbnailPath = Path.Combine(_cacheDirectory, hash + ".png");
        if (File.Exists(thumbnailPath))
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT CacheStatus FROM ThumbnailCache WHERE ContentHash = @ContentHash",
                new { ContentHash = hash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (existing == "Failed")
        {
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO ThumbnailCache (ContentHash, CacheStatus)
                VALUES (@ContentHash, 'Pending')
                ON CONFLICT(ContentHash) DO UPDATE SET CacheStatus = 'Pending'
                WHERE CacheStatus <> 'Failed'
                """,
                new { ContentHash = hash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var png = StlThumbnailRenderer.Render(stlFilePath);
            await File.WriteAllBytesAsync(thumbnailPath, png, cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE ThumbnailCache
                    SET CacheStatus = 'Cached', GeneratedAt = @GeneratedAt
                    WHERE ContentHash = @ContentHash
                    """,
                    new { ContentHash = hash, GeneratedAt = Timestamp() },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            var placeholder = Path.Combine(_cacheDirectory, "_placeholder.png");
            if (!File.Exists(placeholder))
            {
                await File.WriteAllBytesAsync(placeholder, StlThumbnailRenderer.Placeholder(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE ThumbnailCache
                    SET CacheStatus = 'Failed', GeneratedAt = @GeneratedAt
                    WHERE ContentHash = @ContentHash
                    """,
                    new { ContentHash = hash, GeneratedAt = Timestamp() },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private async Task<Microsoft.Data.Sqlite.SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _migrationRunner.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private string ThumbnailPath(string contentHash) =>
        Path.Combine(_cacheDirectory, NormalizeHash(contentHash) + ".png");

    private static string NormalizeHash(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        var hash = contentHash.Trim();
        if (!HashPattern().IsMatch(hash))
        {
            throw new ArgumentException("Content hash contains characters that cannot be used as a cache file name.", nameof(contentHash));
        }

        return hash;
    }

    private static string Timestamp() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex HashPattern();
}
