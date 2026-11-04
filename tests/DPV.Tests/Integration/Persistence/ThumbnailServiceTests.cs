using System.Buffers.Binary;
using System.IO.Compression;
using DPV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DPV.Tests.Integration.Persistence;

public class ThumbnailServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dpv-thumb-" + Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;
    private readonly string _cacheDirectory;

    public ThumbnailServiceTests()
    {
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, "catalog.db");
        _cacheDirectory = Path.Combine(_root, "cache");
    }

    [Fact]
    public async Task EnsureThumbnailGeneratedAsync_CreatesCachedFileAndRecord()
    {
        var hash = "sha256_thumb_test_123";
        var stlPath = WriteTriangle();
        var service = CreateService();

        await service.EnsureThumbnailGeneratedAsync(hash, stlPath);
        var path = await service.GetThumbnailPathAsync(hash);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        AssertPng(path);
        Assert.Equal("Cached", await StatusAsync(hash));

        await service.EnsureThumbnailGeneratedAsync(hash, stlPath);
        Assert.Equal(path, await service.GetThumbnailPathAsync(hash));
    }

    [Fact]
    public async Task EnsureThumbnailGeneratedAsync_MarksCorruptGeometryFailed()
    {
        var hash = "sha256_thumb_failed";
        var stlPath = Path.Combine(_root, "corrupt.stl");
        await File.WriteAllTextAsync(stlPath, "not an stl");
        var service = CreateService();

        await service.EnsureThumbnailGeneratedAsync(hash, stlPath);
        var path = await service.GetThumbnailPathAsync(hash);

        Assert.NotNull(path);
        Assert.EndsWith("_placeholder.png", path, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_cacheDirectory, hash + ".png")));
        Assert.Equal("Failed", await StatusAsync(hash));

        await service.EnsureThumbnailGeneratedAsync(hash, WriteTriangle());
        Assert.Equal("Failed", await StatusAsync(hash));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void AssertPng(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray());
        var offset = 8;
        byte[]? idat = null;
        while (offset + 8 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var data = bytes.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(data[..4]));
                Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)));
            }

            if (type == "IDAT")
            {
                idat = data.ToArray();
            }

            offset += 12 + length;
            if (type == "IEND")
            {
                break;
            }
        }

        Assert.NotNull(idat);
        using var input = new MemoryStream(idat!, 2, idat!.Length - 6);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        var raw = new byte[(128 * 4 + 1) * 128];
        Assert.Equal(raw.Length, deflate.ReadAtLeast(raw, raw.Length, throwOnEndOfStream: true));
    }

    private ThumbnailService CreateService() =>
        new(new SqliteConnectionFactory(_databasePath), new MigrationRunner(), _cacheDirectory);

    private string WriteTriangle()
    {
        var path = Path.Combine(_root, "triangle.stl");
        var bytes = new byte[84 + 50];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80, 4), 1);
        WriteVertex(bytes, 96, 0, 0, 0);
        WriteVertex(bytes, 108, 10, 0, 0);
        WriteVertex(bytes, 120, 0, 8, 4);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void WriteVertex(byte[] bytes, int offset, float x, float y, float z)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), x);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), z);
    }

    private async Task<string> StatusAsync(string hash)
    {
        await using var connection = new SqliteConnectionFactory(_databasePath).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CacheStatus FROM ThumbnailCache WHERE ContentHash = $hash";
        command.Parameters.AddWithValue("$hash", hash);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }
}
