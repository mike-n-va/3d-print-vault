using System.IO;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class LeafFolderCollapserTests
{
    [Theory]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\Supported\body.stl", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\Unsupported\body.stl", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\Parts\wings.stl", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\Renders\hero.png", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\Docs\guide.pdf", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    [InlineData(@"C:\models\ArtisanGuild\Dragon\body.stl", @"C:\models", @"C:\models\ArtisanGuild\Dragon")]
    public void ResolveLogicalDirectory_CollapsesVirtualSubfolders(string filePath, string scanRoot, string expectedDirectory)
    {
        var resolved = LeafFolderCollapser.ResolveLogicalDirectory(filePath, scanRoot);

        Assert.Equal(
            Path.GetFullPath(expectedDirectory),
            Path.GetFullPath(resolved));
    }

    [Theory]
    [InlineData("Supported", true)]
    [InlineData("unsupported", true)]
    [InlineData("parts", true)]
    [InlineData("renders", true)]
    [InlineData("Dragon", false)]
    [InlineData("ArtisanGuild", false)]
    public void IsVirtualVariantSubfolder_IdentifiesReservedNames(string folderName, bool expected)
    {
        var actual = LeafFolderCollapser.IsVirtualVariantSubfolder(folderName);
        Assert.Equal(expected, actual);
    }
}
