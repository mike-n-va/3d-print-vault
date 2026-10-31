using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using DPV.Tests.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DPV.Tests.Architecture.Boundaries;

public class DomainPurityTests
{
    private static readonly ArchitectureModel Model = new ArchLoader()
        .LoadAssemblies(
            Assembly.Load("DPV.Domain"),
            Assembly.Load("DPV.UI"),
            Assembly.Load("DPV.Infrastructure"))
        .Build();

    [Fact]
    public void DomainHasNoProjectReferences()
    {
        var references = TestRepository.GetProjectReferences("DPV.Domain");
        var forbidden = references
            .Where(reference =>
                reference.Equals("DPV.UI", StringComparison.OrdinalIgnoreCase)
                || reference.Equals("DPV.Infrastructure", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            references.Count == 0,
            "DPV.Domain must not reference DPV.UI or DPV.Infrastructure, and must have no project references. "
            + "Found: "
            + (references.Count == 0 ? "(none)" : string.Join(", ", references))
            + (forbidden.Count == 0 ? string.Empty : ". Forbidden: " + string.Join(", ", forbidden)));
    }

    [Fact]
    public void DomainTypesDoNotDependOnUi()
    {
        IArchRule rule = Types()
            .That()
            .ResideInAssembly("DPV.Domain")
            .Should()
            .NotDependOnAny(Types().That().ResideInAssembly("DPV.UI"))
            .Because("DPV.Domain must not reference DPV.UI")
            .WithoutRequiringPositiveResults();

        rule.Check(Model);
    }

    [Fact]
    public void DomainTypesDoNotDependOnInfrastructure()
    {
        IArchRule rule = Types()
            .That()
            .ResideInAssembly("DPV.Domain")
            .Should()
            .NotDependOnAny(Types().That().ResideInAssembly("DPV.Infrastructure"))
            .Because("DPV.Domain must not reference DPV.Infrastructure")
            .WithoutRequiringPositiveResults();

        rule.Check(Model);
    }

    [Fact]
    public void ArchitectureRulesAreExecutable()
    {
        var domainAssembly = Model.Assemblies
            .Select(assembly => assembly.Name)
            .FirstOrDefault(name => name.Equals("DPV.Domain", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            domainAssembly is not null,
            "Architecture rules could not be evaluated because DPV.Domain was not loaded. Loaded assemblies: "
            + string.Join(", ", Model.Assemblies.Select(assembly => assembly.Name)));

        IArchRule rule = Types()
            .That()
            .ResideInAssembly("DPV.Domain")
            .Should()
            .NotDependOnAny(Types().That().ResideInAssembly("DPV.UI"))
            .Because("architecture tests must be able to evaluate the approved dependency rules")
            .WithoutRequiringPositiveResults();

        rule.Check(Model);
    }
}
