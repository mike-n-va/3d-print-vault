using System.Xml.Linq;
using DPV.Tests.Support;

namespace DPV.Tests.Unit.Foundation;

public class SolutionStructureTests
{
    [Fact]
    public void SolutionContainsApprovedProjects()
    {
        var solutionPath = Path.Combine(TestRepository.Root, "DPV.sln");
        Assert.True(File.Exists(solutionPath), $"Solution file was not found at '{solutionPath}'.");

        var missing = TestRepository.SolutionProjects
            .Where(project => !File.Exists(SafeProjectPath(project)))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Approved projects are missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void ApprovedTestFoldersExist()
    {
        string[] folders =
        [
            Path.Combine("tests", "DPV.Tests", "Unit"),
            Path.Combine("tests", "DPV.Tests", "Integration"),
            Path.Combine("tests", "DPV.Tests", "Architecture"),
            Path.Combine("tests", "DPV.Tests", "Unit", "Foundation"),
            Path.Combine("tests", "DPV.Tests", "Architecture", "Dependencies"),
            Path.Combine("tests", "DPV.Tests", "Architecture", "Boundaries")
        ];

        var missing = folders
            .Where(folder => !Directory.Exists(Path.Combine(TestRepository.Root, folder)))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Approved test folders are missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void TemplateTestArtifactIsAbsent()
    {
        var templateTest = Path.Combine(TestRepository.Root, "tests", "DPV.Tests", "UnitTest1.cs");
        Assert.False(
            File.Exists(templateTest),
            $"Template artifact must not exist: '{templateTest}'.");
    }

    [Fact]
    public void TestProjectReferencesApprovedPackages()
    {
        var document = XDocument.Load(TestRepository.ProjectPath("DPV.Tests"));
        var packages = document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(package => !string.IsNullOrWhiteSpace(package))
            .Select(package => package!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] required =
        [
            "xunit.v3",
            "NSubstitute",
            "TngTech.ArchUnitNET",
            "TngTech.ArchUnitNET.xUnitV3"
        ];

        var missing = required.Where(package => !packages.Contains(package)).ToList();
        Assert.True(
            missing.Count == 0,
            "Approved testing packages are not referenced: " + string.Join(", ", missing));

        string[] prohibited =
        [
            "xunit",
            "xunit.runner.visualstudio",
            "FluentAssertions",
            "Shouldly",
            "Moq"
        ];

        var present = prohibited.Where(packages.Contains).ToList();
        Assert.True(
            present.Count == 0,
            "Prohibited or obsolete testing packages are referenced: " + string.Join(", ", present));
    }

    private static string SafeProjectPath(string projectName)
    {
        var sourcePath = Path.Combine(TestRepository.Root, "src", projectName, projectName + ".csproj");
        if (File.Exists(sourcePath))
        {
            return sourcePath;
        }

        return Path.Combine(TestRepository.Root, "tests", projectName, projectName + ".csproj");
    }
}
