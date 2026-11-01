using DPV.Tests.Support;

namespace DPV.Tests.Architecture.Dependencies;

public class CompositionRootArchitectureTests
{
    [Fact]
    public void AppProjectIsDesktopExecutableEntryPoint()
    {
        var outputType = TestRepository.GetProjectProperty("DPV.App", "OutputType");
        Assert.True(
            string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase),
            "DPV.App must be the desktop executable entry point (WinExe). Found: "
            + (outputType ?? "(missing)"));
    }

    [Fact]
    public void UiProjectDoesNotDefineProcessEntryPoint()
    {
        var uiProgram = Path.Combine(TestRepository.Root, "src", "DPV.UI", "Program.cs");
        Assert.False(
            File.Exists(uiProgram),
            "DPV.UI must not own Program.Main. Remove process entry from the UI project.");

        var outputType = TestRepository.GetProjectProperty("DPV.UI", "OutputType");
        Assert.True(
            string.IsNullOrWhiteSpace(outputType)
                || string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase),
            "DPV.UI must remain a presentation library. OutputType must not be WinExe or Exe. Found: "
            + (outputType ?? "(library default)"));
    }

    [Fact]
    public void ProductionSourceDoesNotUseCompetingHostRunLoop()
    {
        var offenders = FindSourceMatches(
            ["IHost.Run(", "host.Run("],
            "DPV.App",
            "DPV.UI",
            "DPV.Infrastructure",
            "DPV.Domain");

        Assert.True(
            offenders.Count == 0,
            "IHost.Run() is prohibited as a competing desktop loop. Matches: "
            + string.Join("; ", offenders));
    }

    [Fact]
    public void BuildServiceProviderIsNotUsedOutsideCompositionRoot()
    {
        var offenders = FindSourceMatches(
            ["BuildServiceProvider("],
            "DPV.UI",
            "DPV.Infrastructure",
            "DPV.Domain");

        Assert.True(
            offenders.Count == 0,
            "BuildServiceProvider must not be used outside DPV.App. Matches: "
            + string.Join("; ", offenders));
    }

    private static List<string> FindSourceMatches(
        IReadOnlyList<string> patterns,
        params string[] projectNames)
    {
        var offenders = new List<string>();
        foreach (var projectName in projectNames)
        {
            foreach (var file in TestRepository.EnumerateSourceFiles(projectName))
            {
                var content = File.ReadAllText(file);
                foreach (var pattern in patterns)
                {
                    if (content.Contains(pattern, StringComparison.Ordinal))
                    {
                        offenders.Add($"{projectName}:{Path.GetRelativePath(TestRepository.Root, file)} contains '{pattern}'");
                    }
                }
            }
        }

        return offenders;
    }
}
