using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;

namespace DPV.Tests.Unit.Infrastructure;

public class StlFileInspectorTests
{
    [Theory]
    [InlineData("part.stl")]
    [InlineData("part.STL")]
    [InlineData("part.Stl")]
    public void Classify_AcceptsCaseInsensitiveStlExtension(string fileName)
    {
        var header = new byte[84];

        var disposition = StlFileInspector.Classify(fileName, header.Length, maxFileSizeBytes: 2_147_483_648, header);

        Assert.Equal(StlCandidateDisposition.Eligible, disposition);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("part.stl.bak")]
    [InlineData("part")]
    public void Classify_IgnoresNonStlNames(string fileName)
    {
        var disposition = StlFileInspector.Classify(fileName, 84, maxFileSizeBytes: 2_147_483_648, new byte[84]);

        Assert.Equal(StlCandidateDisposition.Ignore, disposition);
    }

    [Fact]
    public void Classify_IgnoresEmptyFilesWithoutAnError()
    {
        var disposition = StlFileInspector.Classify("empty.stl", 0, maxFileSizeBytes: 2_147_483_648, ReadOnlySpan<byte>.Empty);

        Assert.Equal(StlCandidateDisposition.Ignore, disposition);
    }

    [Fact]
    public void Classify_RejectsFilesBelowMinimumAsInvalidFormat()
    {
        var disposition = StlFileInspector.Classify("tiny.stl", 50, maxFileSizeBytes: 2_147_483_648, new byte[50]);

        Assert.Equal(StlCandidateDisposition.InvalidFormat, disposition);
    }

    [Fact]
    public void Classify_RejectsFilesAboveConfiguredMaximum()
    {
        var disposition = StlFileInspector.Classify("huge.stl", 200, maxFileSizeBytes: 100, new byte[84]);

        Assert.Equal(StlCandidateDisposition.TooLarge, disposition);
    }

    [Fact]
    public void Classify_RejectsReadableStlThatFailsStructuralValidation()
    {
        var header = new byte[84];
        header.AsSpan().Fill(0x01);

        var disposition = StlFileInspector.Classify("bad.stl", header.Length, maxFileSizeBytes: 2_147_483_648, header);

        Assert.Equal(StlCandidateDisposition.InvalidFormat, disposition);
    }

    [Fact]
    public void ErrorCodes_MatchTheImportContract()
    {
        Assert.Equal("ERR_IO_ACCESS", ImportErrorCodes.IoAccess);
        Assert.Equal("ERR_INVALID_FORMAT", ImportErrorCodes.InvalidFormat);
        Assert.Equal("ERR_HASH_FAILURE", ImportErrorCodes.HashFailure);
        Assert.Equal("ERR_FILE_TOO_LARGE", ImportErrorCodes.FileTooLarge);
    }
}
