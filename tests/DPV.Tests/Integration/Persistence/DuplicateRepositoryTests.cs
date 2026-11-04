using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class DuplicateRepositoryTests : IDisposable
{
    private const string SharedHash = "hash-duplicate-001";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dpv-duplicates-" + Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DuplicateRepositoryTests()
    {
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, "catalog.db");
    }

    [Fact]
    public async Task GetDuplicateGroupsAsync_DetectsCollidingHashes()
    {
        var first = await InsertModelAsync("Bracket", "a\\bracket.stl", SharedHash, 2048);
        var second = await InsertModelAsync("Bracket Copy", "b\\bracket.stl", SharedHash, 2048);
        await InsertModelAsync("Tower", "tower.stl", "hash-unique-001", 4096);
        var inactive = await InsertModelAsync("Old Bracket", "c\\bracket.stl", SharedHash, 2048);
        await SetInactiveAsync(inactive);

        var groups = await CreateRepository().GetDuplicateGroupsAsync();

        var group = Assert.Single(groups);
        Assert.Equal(SharedHash, group.ContentHash);
        Assert.Equal(2, group.DuplicateCount);
        Assert.Equal(2048, group.FileSizeBytes);
        Assert.Contains(group.Files, file => file.FileId == first && file.ModelId == first);
        Assert.Contains(group.Files, file => file.ModelId == second);
        Assert.DoesNotContain(group.Files, file => file.ModelId == inactive);
    }

    [Fact]
    public async Task ResolveDuplicateMergeAsync_DeactivatesRedundantRecords()
    {
        var primaryPath = WriteFile("primary.stl");
        var secondaryPath = WriteFile("secondary.stl");
        var primary = await InsertModelAsync("Primary", primaryPath, SharedHash, 100, absolutePath: true);
        var secondary = await InsertModelAsync("Secondary", secondaryPath, SharedHash, 100, absolutePath: true);
        await AssignMetadataAsync(secondary, "Artisan Guild", "hero");

        await CreateRepository().ResolveDuplicateMergeAsync(primary, [secondary]);

        var groups = await CreateRepository().GetDuplicateGroupsAsync();
        Assert.Empty(groups);
        Assert.True(File.Exists(primaryPath));
        Assert.True(File.Exists(secondaryPath));
        Assert.Equal(1, await CountActiveAsync(primary));
        Assert.Equal(0, await CountActiveAsync(secondary));
        Assert.Equal("Artisan Guild", await ScalarAsync(
            "SELECT a.Name FROM Models m LEFT JOIN Artists a ON a.Id = m.ArtistId WHERE m.Id = $id",
            primary));
        Assert.Equal("hero", await ScalarAsync(
            """
            SELECT t.Name
            FROM ModelTags mt
            INNER JOIN Tags t ON t.Id = mt.TagId
            WHERE mt.ModelId = $id
            """,
            primary));
    }

    [Fact]
    public async Task ResolveDuplicateIgnoreAsync_HidesTheGroupWithoutChangingFiles()
    {
        var first = await InsertModelAsync("One", "one.stl", SharedHash, 100);
        var second = await InsertModelAsync("Two", "two.stl", SharedHash, 100);

        await CreateRepository().ResolveDuplicateIgnoreAsync(SharedHash);

        Assert.Empty(await CreateRepository().GetDuplicateGroupsAsync());
        Assert.Equal(1, await CountActiveAsync(first));
        Assert.Equal(1, await CountActiveAsync(second));
    }

    [Fact]
    public async Task ResolveDuplicateDeleteAsync_RemovesOnlyTheSelectedFile()
    {
        var keepPath = WriteFile("keep.stl");
        var dropPath = WriteFile("drop.stl");
        var keep = await InsertModelAsync("Keep", keepPath, SharedHash, 100, absolutePath: true);
        var drop = await InsertModelAsync("Drop", dropPath, SharedHash, 100, absolutePath: true);

        await CreateRepository().ResolveDuplicateDeleteAsync(drop, dropPath);

        Assert.False(File.Exists(dropPath));
        Assert.True(File.Exists(keepPath));
        Assert.Equal(0, await CountActiveAsync(drop));
        Assert.Equal(1, await CountActiveAsync(keep));
        Assert.Empty(await CreateRepository().GetDuplicateGroupsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRepository().ResolveDuplicateDeleteAsync(keep, keepPath));
        Assert.True(File.Exists(keepPath));
    }

    [Fact]
    public async Task ResolveDuplicateDeleteAsync_RejectsAPathThatIsNotTheCatalogRecord()
    {
        var keepPath = WriteFile("keep.stl");
        var dropPath = WriteFile("drop.stl");
        var otherPath = WriteFile("other.stl");
        await InsertModelAsync("Keep", keepPath, SharedHash, 100, absolutePath: true);
        var drop = await InsertModelAsync("Drop", dropPath, SharedHash, 100, absolutePath: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRepository().ResolveDuplicateDeleteAsync(drop, otherPath));

        Assert.True(File.Exists(dropPath));
        Assert.True(File.Exists(otherPath));
        Assert.Equal(1, await CountActiveAsync(drop));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private DapperDuplicateRepository CreateRepository() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner());

    private string WriteFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private async Task<Guid> InsertModelAsync(
        string name,
        string path,
        string hash,
        long size,
        bool absolutePath = false)
    {
        var modelId = Guid.NewGuid();
        var storedPath = absolutePath ? path : Path.Combine(_root, path);
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await new MigrationRunner().MigrateAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Models (Id, Name, ScanRoot, CreatedAt)
            VALUES ($id, $name, $root, '2026-11-04T00:00:00Z');
            INSERT INTO ModelFiles (ModelId, NormalizedPath, ContentHash, FileSizeBytes, ModifiedAtUtc, IsActive)
            VALUES ($id, $path, $hash, $size, '2026-11-04T01:00:00Z', 1);
            """;
        command.Parameters.AddWithValue("$id", modelId.ToString("D"));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$root", _root);
        command.Parameters.AddWithValue("$path", storedPath);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$size", size);
        await command.ExecuteNonQueryAsync();
        return modelId;
    }

    private async Task AssignMetadataAsync(Guid modelId, string artist, string tag)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Artists (Id, Name) VALUES ($artistId, $artist);
            UPDATE Models SET ArtistId = $artistId WHERE Id = $modelId;
            INSERT INTO Tags (Id, Name) VALUES ($tagId, $tag);
            INSERT INTO ModelTags (ModelId, TagId) VALUES ($modelId, $tagId);
            """;
        command.Parameters.AddWithValue("$artistId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$artist", artist);
        command.Parameters.AddWithValue("$tagId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$tag", tag);
        command.Parameters.AddWithValue("$modelId", modelId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetInactiveAsync(Guid modelId)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ModelFiles SET IsActive = 0 WHERE ModelId = $id";
        command.Parameters.AddWithValue("$id", modelId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> CountActiveAsync(Guid modelId)
    {
        var value = await ScalarAsync(
            "SELECT IsActive FROM ModelFiles WHERE ModelId = $id",
            modelId);
        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<string> ScalarAsync(string sql, Guid modelId)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", modelId.ToString("D"));
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value) ?? string.Empty;
    }
}
