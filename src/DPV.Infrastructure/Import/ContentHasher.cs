using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DPV.Infrastructure.Import;

public static class ContentHasher
{
    public const long FullFileLimitBytes = 16L * 1024 * 1024;
    public const int EdgeLengthBytes = 8192;

    public static string Hash(Stream stream, long length)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (length < FullFileLimitBytes)
        {
            AppendExactly(stream, hash, length);
        }
        else
        {
            AppendExactly(stream, hash, EdgeLengthBytes);
            if (!stream.CanSeek)
            {
                throw new InvalidDataException("Partial hashing requires a seekable stream.");
            }

            stream.Position = length - EdgeLengthBytes;
            AppendExactly(stream, hash, EdgeLengthBytes);
            Span<byte> lengthBytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(lengthBytes, (ulong)length);
            hash.AppendData(lengthBytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendExactly(Stream stream, IncrementalHash hash, long count)
    {
        var buffer = new byte[81920];
        var remaining = count;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new InvalidDataException("The file ended before the hash input was complete.");
            }

            hash.AppendData(buffer.AsSpan(0, read));
            remaining -= read;
        }
    }
}
