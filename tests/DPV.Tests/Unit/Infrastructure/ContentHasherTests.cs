using System.Security.Cryptography;
using DPV.Infrastructure.Import;

namespace DPV.Tests.Unit.Infrastructure;

public class ContentHasherTests
{
    [Fact]
    public void Hash_UnderSixteenMegabytes_UsesEveryByte()
    {
        var payload = "solid vault\nendsolid vault\n"u8.ToArray();

        var actual = ContentHasher.Hash(new MemoryStream(payload), payload.Length);

        Assert.Equal(Hex(SHA256.HashData(payload)), actual);
    }

    [Fact]
    public void Hash_AtSixteenMegabytes_UsesEdgesAndLittleEndianLength()
    {
        var payload = new byte[ContentHasher.FullFileLimitBytes];
        payload[0] = 0x11;
        payload[^1] = 0x22;
        var first = payload.AsSpan(0, ContentHasher.EdgeLengthBytes).ToArray();
        var last = payload.AsSpan(payload.Length - ContentHasher.EdgeLengthBytes, ContentHasher.EdgeLengthBytes).ToArray();
        var length = BitConverter.GetBytes((ulong)payload.LongLength);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(length);
        }

        var actual = ContentHasher.Hash(new MemoryStream(payload), payload.Length);

        Assert.Equal(Hex(SHA256.HashData([..first, ..last, ..length])), actual);
        Assert.NotEqual(Hex(SHA256.HashData(payload)), actual);
    }

    [Fact]
    public void Hash_ShortRead_Throws()
    {
        var payload = new byte[32];

        Assert.Throws<InvalidDataException>(() => ContentHasher.Hash(new MemoryStream(payload), 64));
    }

    private static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
