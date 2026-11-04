using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DPV.Infrastructure.Thumbnails;

public static class StlThumbnailRenderer
{
    public const int Size = 128;
    private const int MaxTriangles = 2000;

    public static byte[] Render(string stlFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stlFilePath);
        using var stream = new FileStream(stlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var segments = ReadSegments(stream);
        if (segments.Count == 0)
        {
            throw new InvalidDataException("The STL file does not contain drawable geometry.");
        }

        return EncodePng(Rasterize(segments));
    }

    public static byte[] Placeholder()
    {
        var pixels = NewCanvas();
        for (var y = 40; y < 88; y++)
        {
            for (var x = 40; x < 88; x++)
            {
                SetPixel(pixels, x, y, 70, 130, 180, 255);
            }
        }

        return EncodePng(pixels);
    }

    private static List<(float X1, float Y1, float X2, float Y2)> ReadSegments(Stream stream)
    {
        if (stream.Length >= 84)
        {
            var header = new byte[84];
            if (stream.Read(header, 0, header.Length) == header.Length)
            {
                var count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80, 4));
                if (count > 0 && stream.Length == 84L + (count * 50L))
                {
                    return ReadBinarySegments(stream, count);
                }
            }

            stream.Position = 0;
        }

        return ReadAsciiSegments(stream);
    }

    private static List<(float X1, float Y1, float X2, float Y2)> ReadBinarySegments(Stream stream, uint count)
    {
        var samples = (int)Math.Min(count, MaxTriangles);
        var buffer = new byte[50];
        var segments = new List<(float, float, float, float)>(samples * 3);
        for (var sample = 0; sample < samples; sample++)
        {
            var index = (long)sample * count / samples;
            stream.Position = 84 + (index * 50);
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                break;
            }

            AddTriangle(segments, buffer.AsSpan(12), buffer.AsSpan(24), buffer.AsSpan(36));
        }

        return segments;
    }

    private static List<(float X1, float Y1, float X2, float Y2)> ReadAsciiSegments(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var vertices = new List<(float X, float Y)>();
        var segments = new List<(float, float, float, float)>();
        while (reader.ReadLine() is { } line && segments.Count < MaxTriangles * 3)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                continue;
            }

            var projected = Project(x, y, z);
            vertices.Add(projected);
            if (vertices.Count == 3)
            {
                segments.Add((vertices[0].X, vertices[0].Y, vertices[1].X, vertices[1].Y));
                segments.Add((vertices[1].X, vertices[1].Y, vertices[2].X, vertices[2].Y));
                segments.Add((vertices[2].X, vertices[2].Y, vertices[0].X, vertices[0].Y));
                vertices.Clear();
            }
        }

        return segments;
    }

    private static void AddTriangle(
        List<(float, float, float, float)> segments,
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third)
    {
        var a = Project(ReadSingle(first), ReadSingle(first[4..]), ReadSingle(first[8..]));
        var b = Project(ReadSingle(second), ReadSingle(second[4..]), ReadSingle(second[8..]));
        var c = Project(ReadSingle(third), ReadSingle(third[4..]), ReadSingle(third[8..]));
        segments.Add((a.X, a.Y, b.X, b.Y));
        segments.Add((b.X, b.Y, c.X, c.Y));
        segments.Add((c.X, c.Y, a.X, a.Y));
    }

    private static float ReadSingle(ReadOnlySpan<byte> bytes) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes);

    private static (float X, float Y) Project(float x, float y, float z) =>
        ((x - z) * 0.8660254f, -(y + ((x + z) * 0.5f)));

    private static byte[] Rasterize(List<(float X1, float Y1, float X2, float Y2)> segments)
    {
        var minX = segments.Min(segment => Math.Min(segment.X1, segment.X2));
        var maxX = segments.Max(segment => Math.Max(segment.X1, segment.X2));
        var minY = segments.Min(segment => Math.Min(segment.Y1, segment.Y2));
        var maxY = segments.Max(segment => Math.Max(segment.Y1, segment.Y2));
        var spanX = Math.Max(maxX - minX, 0.001f);
        var spanY = Math.Max(maxY - minY, 0.001f);
        var scale = 112f / Math.Max(spanX, spanY);
        var pixels = NewCanvas();
        foreach (var segment in segments)
        {
            DrawLine(
                pixels,
                Scale(segment.X1, minX, scale),
                Scale(segment.Y1, minY, scale),
                Scale(segment.X2, minX, scale),
                Scale(segment.Y2, minY, scale));
        }

        return pixels;
    }

    private static int Scale(float value, float min, float scale) =>
        Math.Clamp((int)Math.Round(8 + ((value - min) * scale)), 0, Size - 1);

    private static byte[] NewCanvas()
    {
        var pixels = new byte[Size * Size * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 30;
            pixels[index + 1] = 30;
            pixels[index + 2] = 30;
            pixels[index + 3] = 255;
        }

        return pixels;
    }

    private static void DrawLine(byte[] pixels, int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx - dy;
        while (true)
        {
            SetPixel(pixels, x0, y0, 143, 184, 214, 255);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var doubled = error * 2;
            if (doubled > -dy)
            {
                error -= dy;
                x0 += sx;
            }

            if (doubled < dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(byte[] pixels, int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        var index = ((y * Size) + x) * 4;
        pixels[index] = red;
        pixels[index + 1] = green;
        pixels[index + 2] = blue;
        pixels[index + 3] = alpha;
    }

    private static byte[] EncodePng(byte[] rgba)
    {
        var raw = new byte[(Size * 4 + 1) * Size];
        for (var y = 0; y < Size; y++)
        {
            var rawIndex = y * (Size * 4 + 1);
            raw[rawIndex] = 0;
            Buffer.BlockCopy(rgba, y * Size * 4, raw, rawIndex + 1, Size * 4);
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
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), Size);
        header[8] = 8;
        header[9] = 6;
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
