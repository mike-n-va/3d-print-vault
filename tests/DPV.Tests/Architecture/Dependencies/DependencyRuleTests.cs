using DPV.Tests.Support;

namespace DPV.Tests.Architecture.Dependencies;

public class DependencyRuleTests
{
    [Fact]
    public void ProductionProjectsDoNotReferenceTests()
    {
        var offenders = TestRepository.ProductionProjects
            .Select(project => new
            {
                Project = project,
                References = TestRepository.GetProjectReferences(project)
            })
            .Where(item => item.References.Contains("DPV.Tests", StringComparer.OrdinalIgnoreCase))
            .Select(item => $"{item.Project} -> {string.Join(", ", item.References)}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "No production project may reference DPV.Tests. Offending references: "
            + string.Join("; ", offenders));
    }

    [Fact]
    public void InfrastructureReferencesOnlyDomain()
    {
        var references = TestRepository.GetProjectReferences("DPV.Infrastructure");
        var unexpected = references
            .Where(reference => !reference.Equals("DPV.Domain", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "DPV.Infrastructure may reference only DPV.Domain. Found: "
            + (references.Count == 0 ? "(none)" : string.Join(", ", references)));
    }

    [Fact]
    public void ProjectReferencesAreNotCircular()
    {
        var graph = TestRepository.LoadProjectGraph();
        var cycle = TestRepository.FindCycle(graph);

        Assert.True(
            cycle is null,
            "Circular project reference detected: " + cycle);
    }
}
