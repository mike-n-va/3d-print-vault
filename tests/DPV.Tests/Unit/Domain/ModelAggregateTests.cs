using DPV.Domain.Models;

namespace DPV.Tests.Unit.Domain;

public class ModelAggregateTests
{
    [Fact]
    public void NormalizeOptionalName_TrimsArtistAndCollectionNames()
    {
        Assert.Equal("Artisan Guild", ModelMetadataAssignment.NormalizeOptionalName("  Artisan Guild  ", "artist"));
        Assert.Null(ModelMetadataAssignment.NormalizeOptionalName(null, "artist"));
        Assert.Null(ModelMetadataAssignment.NormalizeOptionalName("", "collection"));
    }

    [Fact]
    public void NormalizeOptionalName_RejectsWhitespace()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ModelMetadataAssignment.NormalizeOptionalName("   ", "artist"));

        Assert.Equal("artist", exception.ParamName);
    }

    [Fact]
    public void NormalizeTags_TrimsDistinctTagsAndSkipsEmptyEntries()
    {
        var tags = ModelMetadataAssignment.NormalizeTags([" minis ", "", "minis", "supported"]);

        Assert.Equal(["minis", "supported"], tags);
    }

    [Fact]
    public void NormalizeTags_RejectsWhitespaceOnlyTags()
    {
        Assert.Throws<ArgumentException>(() => ModelMetadataAssignment.NormalizeTags(["orc", "  "]));
    }
}
