using DPV.Tests.Support;

namespace DPV.Tests.Integration.Packaging;

public class ReleaseWorkflowTests
{
    [Fact]
    public void WorkflowsRunTheTestExecutableAndPublishTheDesktopApp()
    {
        var root = TestRepository.Root;
        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        Assert.Contains("DPV.Tests.exe -noColor", release, StringComparison.Ordinal);
        Assert.Contains("src/DPV.App/DPV.App.csproj", release, StringComparison.Ordinal);
        Assert.Contains("PublishProfile=WindowsReleaseProfile", release, StringComparison.Ordinal);
        Assert.Contains("contents: write", release, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet test", release, StringComparison.Ordinal);

        Assert.Contains("DPV.Tests.exe -noColor", ci, StringComparison.Ordinal);
        Assert.Contains("DPV.Tests -noColor", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet test", ci, StringComparison.Ordinal);
    }
}
