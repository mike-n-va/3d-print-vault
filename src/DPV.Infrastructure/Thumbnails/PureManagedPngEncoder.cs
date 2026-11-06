using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace DPV.Infrastructure.Thumbnails;

public static class PureManagedPngEncoder
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        }

        var rowLength = width * 4;
        if (rgba.Length < rowLength * height)
        {
            throw new ArgumentException("RGBA buffer is smaller than width * height * 4.", nameof(rgba));
        }

        var raw = new byte[(rowLength + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var rawIndex = y * (rowLength + 1);
            raw[rawIndex] = 0; // Filter: None
            Buffer.BlockCopy(rgba, y * rowLength, raw, rawIndex + 1, rowLength);
        }

        using var compressed = new MemoryStream();
        compressed.WriteByte(0x78);
        compressed.WriteByte(0x01);
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(raw, 0, raw.Length);
        }

        var adler = Adler32(raw);
        compressed.WriteByte((byte)(adler >> 24));
        compressed.WriteByte((byte)(adler >> 16));
        compressed.WriteByte((byte)(adler >> 8));
        compressed.WriteByte((byte)adler);

        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6; // RGBA
        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (uint)-(crc & 1);
                crc = (crc >> 1) ^ (0xEDB88320u & mask);
            }
        }

        return crc;
    }
}
