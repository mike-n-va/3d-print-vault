using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class ModelDetailRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"dpv-detail-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetModelDetailAsync_ReturnsExpectedAggregate()
    {
        var modelId = await InsertModelAsync("Bracket", "models/bracket.stl", "abc123hash", 1024);
        var repository = CreateRepository();

        var detail = await repository.GetModelDetailAsync(modelId);

        Assert.NotNull(detail);
        Assert.Equal(modelId, detail.Id);
        Assert.Equal("Bracket", detail.Name);
        var file = Assert.Single(detail.Files);
        Assert.EndsWith("bracket.stl", file.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("abc123hash", file.FileHash);
        Assert.Equal(1024, file.FileSizeBytes);
        Assert.True(file.IsActive);
        Assert.Empty(detail.Tags);
    }

    [Fact]
    public async Task UpdateModelMetadataAsync_PersistsArtistCollectionAndTags()
    {
        var modelId = await InsertModelAsync("Orc", "orcs/warrior.stl", "hash-orc", 2048);
        var repository = CreateRepository();

        await repository.UpdateModelMetadataAsync(
            modelId,
            "  Artisan Guild  ",
            "Orc Horde",
            [" minis ", "supported", "minis", "orc"]);

        var detail = await repository.GetModelDetailAsync(modelId);
        Assert.NotNull(detail);
        Assert.Equal("Artisan Guild", detail.ArtistName);
        Assert.Equal("Orc Horde", detail.CollectionName);
        Assert.Equal(["minis", "orc", "supported"], detail.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray());

        var byTag = await new DapperCatalogQuery(new SqliteConnectionFactory(_databasePath), new MigrationRunner())
            .SearchModelsAsync(new ModelQueryParameters(TagFilter: "supported"));
        Assert.Equal(modelId, Assert.Single(byTag).Id);
    }

    [Fact]
    public async Task UpdateModelMetadataAsync_ClearsArtistAndReusesExistingNames()
    {
        var first = await InsertModelAsync("One", "one.stl", "hash-one", 100);
        var second = await InsertModelAsync("Two", "two.stl", "hash-two", 200);
        var repository = CreateRepository();

        await repository.UpdateModelMetadataAsync(first, "Shared Artist", "Shared Set", ["hero"]);
        await repository.UpdateModelMetadataAsync(second, "Shared Artist", null, ["hero"]);
        await repository.UpdateModelMetadataAsync(first, null, "Shared Set", []);

        var cleared = await repository.GetModelDetailAsync(first);
        var reused = await repository.GetModelDetailAsync(second);
        Assert.NotNull(cleared);
        Assert.NotNull(reused);
        Assert.Null(cleared.ArtistName);
        Assert.Equal("Shared Set", cleared.CollectionName);
        Assert.Empty(cleared.Tags);
        Assert.Equal("Shared Artist", reused.ArtistName);
        Assert.Null(reused.CollectionName);

        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Artists;";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task GetModelDetailAsync_ReturnsNullWhenTheModelDoesNotExist()
    {
        var detail = await CreateRepository().GetModelDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
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

    private DapperModelDetailRepository CreateRepository() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner());

    private async Task<Guid> InsertModelAsync(string name, string relativePath, string hash, long size)
    {
        var modelId = Guid.NewGuid();
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
        command.Parameters.AddWithValue("$root", Path.GetTempPath());
        command.Parameters.AddWithValue("$path", Path.Combine(Path.GetTempPath(), relativePath));
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$size", size);
        await command.ExecuteNonQueryAsync();
        return modelId;
    }
}
