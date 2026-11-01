using DPV.Domain.Entities;

namespace DPV.Tests.Unit.Domain;

public class DomainEntityTests
{
    [Theory]
    [InlineData("  Creator  ", "Creator")]
    [InlineData("   A Creator \t", "A Creator")]
    public void ArtistCreationGeneratesIdentityAndTrimsName(string name, string expectedName)
    {
        var artist = new Artist(name);

        Assert.NotEqual(Guid.Empty, artist.Id);
        Assert.Equal(expectedName, artist.Name);
    }

    [Theory]
    [InlineData("  Release pack ", "Release pack")]
    [InlineData("Collection", "Collection")]
    public void CollectionCreationGeneratesIdentityAndTrimsName(string name, string expectedName)
    {
        var collection = new Collection(name);

        Assert.NotEqual(Guid.Empty, collection.Id);
        Assert.Equal(expectedName, collection.Name);
        Assert.Null(collection.Artist);
    }

    [Theory]
    [InlineData("  Printable design ", "Printable design")]
    [InlineData("Model", "Model")]
    public void ModelCreationGeneratesIdentityAndStartsWithoutAssociations(
        string name,
        string expectedName)
    {
        var model = new Model(name);

        Assert.NotEqual(Guid.Empty, model.Id);
        Assert.Equal(expectedName, model.Name);
        Assert.Null(model.Collection);
        Assert.Empty(model.Tags);
    }

    [Fact]
    public void TagCreationGeneratesIdentityAndTrimsName()
    {
        var tag = new Tag("  Decorative  ");

        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.Equal("Decorative", tag.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void EveryEntityRejectsInvalidNames(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Artist(name!));
        Assert.ThrowsAny<ArgumentException>(() => new Collection(name!));
        Assert.ThrowsAny<ArgumentException>(() => new Model(name!));
        Assert.ThrowsAny<ArgumentException>(() => new Tag(name!));
    }

    [Fact]
    public void NamesMayRepeatWithoutMakingEntitiesEqual()
    {
        var firstArtist = new Artist("Same");
        var secondArtist = new Artist("Same");
        var firstCollection = new Collection("Same");
        var secondCollection = new Collection("Same");
        var firstModel = new Model("Same");
        var secondModel = new Model("Same");
        var firstTag = new Tag("Same");
        var secondTag = new Tag("Same");

        Assert.NotEqual(firstArtist.Id, secondArtist.Id);
        Assert.NotEqual(firstCollection.Id, secondCollection.Id);
        Assert.NotEqual(firstModel.Id, secondModel.Id);
        Assert.NotEqual(firstTag.Id, secondTag.Id);
        Assert.NotEqual(firstArtist, secondArtist);
        Assert.NotEqual(firstCollection, secondCollection);
        Assert.NotEqual(firstModel, secondModel);
        Assert.NotEqual(firstTag, secondTag);
        var sameArtist = firstArtist;
        var sameCollection = firstCollection;
        var sameModel = firstModel;
        var sameTag = firstTag;
        Assert.True(firstArtist == sameArtist);
        Assert.True(firstCollection == sameCollection);
        Assert.True(firstModel == sameModel);
        Assert.True(firstTag == sameTag);
        Assert.True(firstArtist != secondArtist);
        Assert.True(firstCollection != secondCollection);
        Assert.True(firstModel != secondModel);
        Assert.True(firstTag != secondTag);
    }

    [Fact]
    public void RenameAppliesTheSameNameValidation()
    {
        var artist = new Artist("Before");
        var collection = new Collection("Before");
        var model = new Model("Before");
        var tag = new Tag("Before");

        artist.Rename("  Artist after ");
        collection.Rename("  Collection after ");
        model.Rename("  Model after ");
        tag.Rename("  Tag after ");

        Assert.Equal("Artist after", artist.Name);
        Assert.Equal("Collection after", collection.Name);
        Assert.Equal("Model after", model.Name);
        Assert.Equal("Tag after", tag.Name);
        Assert.Throws<ArgumentException>(() => artist.Rename(" "));
        Assert.Throws<ArgumentException>(() => collection.Rename(""));
        Assert.ThrowsAny<ArgumentException>(() => model.Rename(null!));
        Assert.Throws<ArgumentException>(() => tag.Rename("\t"));
    }

    [Fact]
    public void CollectionMayBeAssociatedWithAtMostOneArtist()
    {
        var artist = new Artist("Creator");
        var collection = new Collection("Release", artist);

        Assert.Same(artist, collection.Artist);
    }

    [Fact]
    public void ModelMayBeAssociatedWithAtMostOneCollection()
    {
        var collection = new Collection("Release");
        var model = new Model("Printable design", collection);

        Assert.Same(collection, model.Collection);
    }

    [Fact]
    public void ModelAcceptsDistinctTagsAndDoesNotExposeMutableTagCollection()
    {
        var firstTag = new Tag("Functional");
        var secondTag = new Tag("Decorative");
        var model = new Model("Printable design", null, [firstTag, secondTag]);

        Assert.Collection(
            model.Tags,
            tag => Assert.Same(firstTag, tag),
            tag => Assert.Same(secondTag, tag));
        Assert.IsAssignableFrom<IList<Tag>>(model.Tags);
        Assert.Throws<NotSupportedException>(() => ((IList<Tag>)model.Tags).Add(new Tag("Extra")));
    }

    [Fact]
    public void ModelRejectsNullTagCollection()
    {
        Assert.Throws<ArgumentNullException>(() => new Model("Printable design", null, null!));
    }

    [Fact]
    public void ModelRejectsNullAndDuplicateTags()
    {
        var tag = new Tag("Functional");

        Assert.Throws<ArgumentException>(
            () => new Model("Printable design", null, new Tag[] { null! }));
        Assert.Throws<ArgumentException>(
            () => new Model("Printable design", null, [tag, tag]));
    }
}
