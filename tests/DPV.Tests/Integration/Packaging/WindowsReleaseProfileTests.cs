using System.Xml.Linq;
using DPV.Tests.Support;

namespace DPV.Tests.Integration.Packaging;

public class WindowsReleaseProfileTests
{
    [Fact]
    public void WindowsReleaseProfilePackagesTheNet10DesktopExecutable()
    {
        var appProject = XDocument.Load(Path.Combine(TestRepository.Root, "src", "DPV.App", "DPV.App.csproj"));
        var uiProject = XDocument.Load(Path.Combine(TestRepository.Root, "src", "DPV.UI", "DPV.UI.csproj"));
        var profile = XDocument.Load(Path.Combine(
            TestRepository.Root,
            "src",
            "DPV.App",
            "Properties",
            "PublishProfiles",
            "WindowsReleaseProfile.pubxml"));

        Assert.Equal("WinExe", Property(appProject, "OutputType"));
        Assert.Equal("net10.0", Property(appProject, "TargetFramework"));
        Assert.Equal(@"Assets\3DPrintVault.ico", Property(appProject, "ApplicationIcon"));
        Assert.Equal("true", Property(appProject, "BuiltInComInteropSupport"));
        Assert.True(File.Exists(Path.Combine(TestRepository.Root, "src", "DPV.App", "Assets", "3DPrintVault.ico")));

        Assert.Equal("net10.0", Property(uiProject, "TargetFramework"));
        Assert.Null(Property(uiProject, "OutputType"));

        var publish = profile.Descendants().Where(element => !element.HasElements)
            .ToDictionary(element => element.Name.LocalName, element => element.Value);
        Assert.Equal("Release", publish["Configuration"]);
        Assert.Equal("net10.0", publish["TargetFramework"]);
        Assert.Equal("win-x64", publish["RuntimeIdentifier"]);
        Assert.Equal("true", publish["SelfContained"]);
        Assert.Equal("true", publish["PublishSingleFile"]);
        Assert.Equal("true", publish["PublishReadyToRun"]);
        Assert.Equal("false", publish["PublishTrimmed"]);
        Assert.Equal("true", publish["IncludeNativeLibrariesForSelfExtract"]);
        Assert.Equal("false", publish["CopyOutputSymbolsToPublishDirectory"]);
        Assert.Contains("artifacts", publish["PublishDir"], StringComparison.OrdinalIgnoreCase);
    }

    private static string? Property(XDocument project, string name) =>
        project.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
}
