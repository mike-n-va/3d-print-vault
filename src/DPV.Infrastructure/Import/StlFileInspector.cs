namespace DPV.Infrastructure.Import;

public enum StlCandidateDisposition
{
    Ignore,
    Eligible,
    InvalidFormat,
    TooLarge
}

public static class StlFileInspector
{
    public const long MinimumAcceptedBytes = 84;

    public static StlCandidateDisposition Classify(
        string fileName,
        long length,
        long maxFileSizeBytes,
        ReadOnlySpan<byte> header)
    {
        if (!IsStlExtension(fileName))
        {
            return StlCandidateDisposition.Ignore;
        }

        if (length <= 0)
        {
            return StlCandidateDisposition.Ignore;
        }

        if (length > maxFileSizeBytes)
        {
            return StlCandidateDisposition.TooLarge;
        }

        if (length < MinimumAcceptedBytes || !HasValidSignature(header, length))
        {
            return StlCandidateDisposition.InvalidFormat;
        }

        return StlCandidateDisposition.Eligible;
    }

    public static bool IsStlExtension(string fileName) =>
        Path.GetExtension(fileName).Equals(".stl", StringComparison.OrdinalIgnoreCase);

    public static bool HasValidSignature(ReadOnlySpan<byte> header, long length)
    {
        if (header.Length >= MinimumAcceptedBytes && IsBinaryStl(header, length))
        {
            return true;
        }

        return IsAsciiStl(header);
    }

    private static bool IsBinaryStl(ReadOnlySpan<byte> header, long length)
    {
        var triangleCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(80, 4));
        return length == MinimumAcceptedBytes + (triangleCount * 50L);
    }

    private static bool IsAsciiStl(ReadOnlySpan<byte> header)
    {
        var span = header;
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        if (span.StartsWith(bom))
        {
            span = span[bom.Length..];
        }

        var index = 0;
        while (index < span.Length && IsAsciiWhitespace(span[index]))
        {
            index++;
        }

        ReadOnlySpan<byte> solid = "solid"u8;
        return span.Length - index >= solid.Length
            && span.Slice(index, solid.Length).SequenceEqual(solid);
    }

    private static bool IsAsciiWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
