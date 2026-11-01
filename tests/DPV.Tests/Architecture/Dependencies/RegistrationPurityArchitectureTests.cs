using DPV.Tests.Support;

namespace DPV.Tests.Architecture.Dependencies;

public class RegistrationPurityArchitectureTests
{
    private static readonly string[] RegistrationFiles =
    [
        Path.Combine("src", "DPV.App", "DependencyInjection", "ApplicationServiceCollectionExtensions.cs"),
        Path.Combine("src", "DPV.App", "DependencyInjection", "DependencyInjectionOrchestrator.cs"),
        Path.Combine("src", "DPV.UI", "DependencyInjection", "UiServiceCollectionExtensions.cs"),
        Path.Combine("src", "DPV.Infrastructure", "DependencyInjection", "InfrastructureServiceCollectionExtensions.cs"),
    ];

    private static readonly string[] ProhibitedRegistrationTokens =
    [
        "BuildServiceProvider(",
        "Migrate(",
        "File.Read",
        "File.Write",
        "Directory.Get",
        "SQLiteConnection",
        "host.Run(",
        "StartAsync("
    ];

    [Fact]
    public void RegistrationMethodsDoNotContainProhibitedSideEffects()
    {
        var offenders = new List<string>();
        foreach (var relativePath in RegistrationFiles)
        {
            var fullPath = Path.Combine(TestRepository.Root, relativePath);
            Assert.True(File.Exists(fullPath), $"Expected registration file was not found: {relativePath}");

            var content = File.ReadAllText(fullPath);
            foreach (var token in ProhibitedRegistrationTokens)
            {
                if (content.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add($"{relativePath} contains '{token}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Registration methods must remain pure. Offending tokens: "
            + string.Join("; ", offenders));
    }
}
