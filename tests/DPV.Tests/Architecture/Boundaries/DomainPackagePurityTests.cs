using DPV.Tests.Support;

namespace DPV.Tests.Architecture.Boundaries;

public class DomainPackagePurityTests
{
    private static readonly string[] ProhibitedDomainPackages =
    [
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Options",
        "Avalonia",
        "Serilog"
    ];

    [Fact]
    public void DomainProjectHasNoProhibitedPackageReferences()
    {
        var packages = TestRepository.GetPackageReferences("DPV.Domain");
        var offenders = packages
            .Where(package => ProhibitedDomainPackages.Contains(package, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "DPV.Domain must remain free of DI, hosting, configuration, options, Avalonia, and Serilog packages. Found: "
            + (offenders.Count == 0 ? "(none)" : string.Join(", ", offenders)));
    }

    [Fact]
    public void DomainProjectReferencesNoOtherProductionProjects()
    {
        var references = TestRepository.GetProjectReferences("DPV.Domain");
        Assert.True(
            references.Count == 0,
            "DPV.Domain must not reference other production projects. Found: "
            + (references.Count == 0 ? "(none)" : string.Join(", ", references)));
    }
}
