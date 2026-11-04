using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class LibraryStatsRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"dpv-stats-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetLibraryStatsAsync_ReturnsAccurateAggregates()
    {
        var token = TestContext.Current.CancellationToken;
        await InsertLibraryAsync(token);
        var stats = await CreateRepository().GetLibraryStatsAsync(token);

        Assert.Equal(2, stats.TotalModels);
        Assert.Equal(1, stats.TotalFiles);
        Assert.Equal(5000, stats.TotalBytesUsed);
        Assert.Equal(1, stats.TotalArtists);
        Assert.Equal(1, stats.TotalCollections);
        Assert.Equal(2, stats.TotalTags);
        var artist = Assert.Single(stats.TopArtists);
        Assert.Equal("Test Artist", artist.Name);
        Assert.Equal(2, artist.ModelCount);
        Assert.Equal("Test Collection", Assert.Single(stats.TopCollections).Name);
        Assert.Equal(["hero", "orc"], stats.TopTags.Select(tag => tag.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public async Task GetLibraryStatsAsync_ReturnsZerosForAnEmptyCatalog()
    {
        var stats = await CreateRepository().GetLibraryStatsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, stats.TotalModels);
        Assert.Equal(0, stats.TotalBytesUsed);
        Assert.Empty(stats.TopArtists);
        Assert.Empty(stats.TopCollections);
        Assert.Empty(stats.TopTags);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private DapperLibraryStatsRepository CreateRepository() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner());

    private async Task InsertLibraryAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await new MigrationRunner().MigrateAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Artists (Id, Name) VALUES ('art_1', 'Test Artist');
            INSERT INTO Collections (Id, Name) VALUES ('col_1', 'Test Collection');
            INSERT INTO Tags (Id, Name) VALUES ('tag_1', 'hero'), ('tag_2', 'orc');
            INSERT INTO Models (Id, Name, ScanRoot, ArtistId, CollectionId, CreatedAt)
            VALUES ('mod_1', 'Warrior', 'C:/library', 'art_1', 'col_1', '2026-11-04T00:00:00Z');
            INSERT INTO Models (Id, Name, ScanRoot, ArtistId, CollectionId, CreatedAt)
            VALUES ('mod_2', 'Inactive', 'C:/library', 'art_1', 'col_1', '2026-11-04T00:00:00Z');
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ('mod_1', 'C:/library/test.stl', 'hash123', 5000, '2026-11-04T01:00:00Z', 1);
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ('mod_2', 'C:/library/old.stl', 'hash456', 9000, '2026-11-04T01:00:00Z', 0);
            INSERT INTO ModelTags (ModelId, TagId) VALUES ('mod_1', 'tag_1'), ('mod_1', 'tag_2');
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
