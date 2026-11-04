using System.Buffers.Binary;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Unit.Infrastructure;

public class FtsSearchTests
{
    [Fact]
    public async Task SearchMatchesNameAndPathWithoutRegardToCase()
    {
        using var library = new SearchLibrary();
        library.WriteBinaryStl(Path.Combine("creatures", "bracket.stl"));
        library.WriteBinaryStl("tower.stl");
        await library.ImportAsync();
        var query = library.CreateQuery();

        var byName = await query.SearchModelsAsync(new ModelQueryParameters(SearchTerm: "BRACKET", ScanRoot: library.Root), TestContext.Current.CancellationToken);
        var byPath = await query.SearchModelsAsync(new ModelQueryParameters(SearchTerm: "creatures", ScanRoot: library.Root), TestContext.Current.CancellationToken);
        var bySubstring = await query.SearchModelsAsync(new ModelQueryParameters(SearchTerm: "rack", ScanRoot: library.Root), TestContext.Current.CancellationToken);
        var missing = await query.SearchModelsAsync(new ModelQueryParameters(SearchTerm: "dragon", ScanRoot: library.Root), TestContext.Current.CancellationToken);

        Assert.Equal("bracket", Assert.Single(byName).Name);
        Assert.Contains("creatures", Assert.Single(byPath).FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("bracket", Assert.Single(bySubstring).Name);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task ImportPopulatesTheFtsIndex()
    {
        using var library = new SearchLibrary();
        library.WriteBinaryStl("bracket.stl");
        await library.ImportAsync();

        var factory = new SqliteConnectionFactory(library.DatabasePath);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Models_Fts WHERE Models_Fts MATCH '\"bracket\"';";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task TagFilterRestrictsIndexedTags()
    {
        using var library = new SearchLibrary();
        library.WriteBinaryStl("bracket.stl");
        await library.ImportAsync();
        await library.SetTagsAsync("miniature");
        var query = library.CreateQuery();

        var matched = await query.SearchModelsAsync(new ModelQueryParameters(TagFilter: "miniature", ScanRoot: library.Root), TestContext.Current.CancellationToken);
        var unmatched = await query.SearchModelsAsync(new ModelQueryParameters(TagFilter: "vehicle", ScanRoot: library.Root), TestContext.Current.CancellationToken);

        Assert.Equal("bracket", Assert.Single(matched).Name);
        Assert.Empty(unmatched);
    }

    private sealed class SearchLibrary : IDisposable
    {
        public SearchLibrary()
        {
            Root = Path.Combine(Path.GetTempPath(), "dpv-fts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "catalog.db");
        }

        public string Root { get; }

        public string DatabasePath { get; }

        public DapperCatalogQuery CreateQuery() =>
            new(new SqliteConnectionFactory(DatabasePath), new MigrationRunner());

        public async Task ImportAsync()
        {
            var service = new ModelImportService(
                new SqliteConnectionFactory(DatabasePath),
                new MigrationRunner(),
                new SqliteCatalogStore());
            await service.ImportAsync(new ImportLibraryRequest(Root, 2L * 1024 * 1024 * 1024), TestContext.Current.CancellationToken);
        }

        public void WriteBinaryStl(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = new byte[84];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80, 4), 0);
            File.WriteAllBytes(path, bytes);
        }

        public async Task SetTagsAsync(string tags)
        {
            var factory = new SqliteConnectionFactory(DatabasePath);
            await using var connection = factory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM Models_Fts;
                INSERT INTO Models_Fts(rowid, model_id, name, file_path, tags)
                SELECT f.rowid, f.ModelId, m.Name, f.NormalizedPath, $tags
                FROM ModelFiles f
                INNER JOIN Models m ON m.Id = f.ModelId;
                """;
            command.Parameters.AddWithValue("$tags", tags);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
