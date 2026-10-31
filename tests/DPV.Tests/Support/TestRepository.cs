using System.Xml.Linq;

namespace DPV.Tests.Support;

internal static class TestRepository
{
    public static readonly string[] ProductionProjects =
    [
        "DPV.App",
        "DPV.UI",
        "DPV.Domain",
        "DPV.Infrastructure"
    ];

    public static readonly string[] SolutionProjects =
    [
        ..ProductionProjects,
        "DPV.Tests"
    ];

    public static string Root { get; } = FindRoot();

    public static string ProjectPath(string projectName)
    {
        var sourcePath = Path.Combine(Root, "src", projectName, projectName + ".csproj");
        if (File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var testPath = Path.Combine(Root, "tests", projectName, projectName + ".csproj");
        if (File.Exists(testPath))
        {
            return testPath;
        }

        throw new InvalidOperationException(
            $"Project file for '{projectName}' was not found under '{Root}'.");
    }

    public static IReadOnlyList<string> GetProjectReferences(string projectName)
    {
        var document = XDocument.Load(ProjectPath(projectName));
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadProjectGraph()
    {
        return SolutionProjects.ToDictionary(
            project => project,
            GetProjectReferences,
            StringComparer.OrdinalIgnoreCase);
    }

    public static string? FindCycle(IReadOnlyDictionary<string, IReadOnlyList<string>> graph)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();

        foreach (var project in graph.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var cycle = Visit(project);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;

        string? Visit(string project)
        {
            if (!graph.ContainsKey(project) || visited.Contains(project))
            {
                return null;
            }

            if (!visiting.Add(project))
            {
                var start = stack.FindIndex(name =>
                    name.Equals(project, StringComparison.OrdinalIgnoreCase));
                return string.Join(" -> ", stack.Skip(start).Append(project));
            }

            stack.Add(project);
            foreach (var reference in graph[project])
            {
                var cycle = Visit(reference);
                if (cycle is not null)
                {
                    return cycle;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            visiting.Remove(project);
            visited.Add(project);
            return null;
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DPV.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find DPV.sln by walking up from '{AppContext.BaseDirectory}'.");
    }
}
