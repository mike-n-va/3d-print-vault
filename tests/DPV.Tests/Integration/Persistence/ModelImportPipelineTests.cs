using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class ModelImportPipelineTests
{
    private const long TwoGigabytes = 2L * 1024 * 1024 * 1024;

    [Fact]
    public async Task ImportPersistsEligibleStlAndSkipsItOnRescan()
    {
        using var library = new TemporaryLibrary();
        var stlPath = library.WriteBinaryStl("bracket.stl");
        library.WriteBytes("readme.txt", "not a model"u8.ToArray());
        var service = library.CreateService();

        var first = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var second = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var rows = await library.ReadFilesAsync();

        Assert.True(first.Completed);
        Assert.Equal(1, first.TotalAdded);
        Assert.Empty(first.Errors);
        Assert.Equal(1, second.TotalSkipped);
        Assert.Equal(0, second.TotalAdded);
        Assert.Single(rows);
        Assert.Equal(1L, rows[0].IsActive);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}bracket.stl", rows[0].NormalizedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(84, new FileInfo(stlPath).Length);
    }

    [Fact]
    public async Task ModifiedFileKeepsModelIdAndUpdatesHash()
    {
        using var library = new TemporaryLibrary();
        library.WriteBinaryStl("bracket.stl");
        var service = library.CreateService();
        await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var before = await library.ReadFilesAsync();

        library.WriteBytes("bracket.stl", library.BinaryStl(triangleCount: 1));
        var result = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var after = await library.ReadFilesAsync();

        Assert.Equal(1, result.TotalUpdated);
        Assert.Equal(before[0].Id, after[0].Id);
        Assert.NotEqual(before[0].ContentHash, after[0].ContentHash);
        Assert.Equal(1L, after[0].IsActive);
    }

    [Fact]
    public async Task MovedFileKeepsModelIdAndHash()
    {
        using var library = new TemporaryLibrary();
        var source = library.WriteBinaryStl("bracket.stl");
        var service = library.CreateService();
        await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var before = await library.ReadFilesAsync();

        var destination = Path.Combine(library.Root, "moved.stl");
        File.Move(source, destination);
        var result = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var after = await library.ReadFilesAsync();

        Assert.Equal(1, result.TotalMoved);
        Assert.Equal(0, result.TotalInactivated);
        Assert.Equal(before[0].Id, after[0].Id);
        Assert.Equal(before[0].ContentHash, after[0].ContentHash);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}moved.stl", after[0].NormalizedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Single(after);
    }

    [Fact]
    public async Task MissingFileIsMarkedInactiveOnlyAfterACompletedScan()
    {
        using var library = new TemporaryLibrary();
        var stlPath = library.WriteBinaryStl("bracket.stl");
        var service = library.CreateService();
        await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        File.Delete(stlPath);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var cancelledResult = await service.ImportAsync(library.Request(), cancelled.Token);
        var duringCancellation = await library.ReadFilesAsync();

        var completed = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var afterCompletion = await library.ReadFilesAsync();

        Assert.False(cancelledResult.Completed);
        Assert.Equal(0, cancelledResult.TotalInactivated);
        Assert.Equal(1L, duringCancellation[0].IsActive);
        Assert.True(completed.Completed);
        Assert.Equal(1, completed.TotalInactivated);
        Assert.Equal(0L, afterCompletion[0].IsActive);
    }

    [Fact]
    public async Task InvalidAndOversizedFilesAreReportedWithoutStoppingTheScan()
    {
        using var library = new TemporaryLibrary();
        library.WriteBinaryStl("good.stl");
        library.WriteBytes("tiny.stl", new byte[50]);
        library.WriteBytes("bad.stl", Enumerable.Repeat((byte)0x01, 84).ToArray());
        library.WriteBytes("huge.stl", new byte[200]);
        var service = library.CreateService();

        var result = await service.ImportAsync(library.Request(maxFileSizeBytes: 100), TestContext.Current.CancellationToken);
        var rows = await library.ReadFilesAsync();

        Assert.True(result.Completed);
        Assert.Equal(1, result.TotalAdded);
        Assert.Single(rows);
        Assert.Contains(result.Errors, error => error.Code == ImportErrorCodes.InvalidFormat && error.Path!.EndsWith("tiny.stl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Code == ImportErrorCodes.InvalidFormat && error.Path!.EndsWith("bad.stl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Code == ImportErrorCodes.FileTooLarge && error.Path!.EndsWith("huge.stl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LockedFileIsReportedAndDoesNotAbortTheScan()
    {
        using var library = new TemporaryLibrary();
        library.WriteBinaryStl("good.stl");
        var lockedPath = library.WriteBinaryStl("locked.stl");
        await using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var service = library.CreateService();

        var result = await service.ImportAsync(library.Request(), TestContext.Current.CancellationToken);
        var rows = await library.ReadFilesAsync();

        Assert.Equal(1, result.TotalAdded);
        Assert.Contains(rows, row => row.NormalizedPath.EndsWith("good.stl", StringComparison.OrdinalIgnoreCase));
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(result.Errors, error => error.Code == ImportErrorCodes.IoAccess);
            Assert.DoesNotContain(rows, row => row.NormalizedPath.EndsWith("locked.stl", StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TemporaryLibrary : IDisposable
    {
        public TemporaryLibrary()
        {
            Root = Path.Combine(Path.GetTempPath(), "dpv-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "catalog.db");
        }

        public string Root { get; }

        public string DatabasePath { get; }

        public ImportLibraryRequest Request(long maxFileSizeBytes = TwoGigabytes) =>
            new(Root, maxFileSizeBytes);

        public ModelImportService CreateService()
        {
            var factory = new SqliteConnectionFactory(DatabasePath);
            return new ModelImportService(factory, new MigrationRunner(), new SqliteCatalogStore());
        }

        public string WriteBinaryStl(string name, uint triangleCount = 0)
        {
            var path = Path.Combine(Root, name);
            File.WriteAllBytes(path, BinaryStl(triangleCount));
            return path;
        }

        public byte[] BinaryStl(uint triangleCount)
        {
            var bytes = new byte[84 + (triangleCount * 50)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80, 4), triangleCount);
            return bytes;
        }

        public void WriteBytes(string name, byte[] content) =>
            File.WriteAllBytes(Path.Combine(Root, name), content);

        public async Task<List<StoredFile>> ReadFilesAsync()
        {
            var factory = new SqliteConnectionFactory(DatabasePath);
            await using var connection = factory.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT m.Id, f.NormalizedPath, f.ContentHash, f.IsActive
                FROM Models m
                INNER JOIN ModelFiles f ON f.ModelId = m.Id
                ORDER BY f.NormalizedPath;
                """;
            var rows = new List<StoredFile>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new StoredFile(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3)));
            }

            return rows;
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

    private sealed record StoredFile(string Id, string NormalizedPath, string ContentHash, long IsActive);
}
