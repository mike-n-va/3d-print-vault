using DPV.App.Configuration;
using DPV.App.DependencyInjection;
using DPV.App.Startup;
using DPV.Infrastructure.DependencyInjection;
using AvaloniaApplication = DPV.UI.App;
using DPV.UI.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DPV.Tests.Support;

namespace DPV.Tests.Integration.DependencyInjection;

[Collection(nameof(CompositionRootIntegrationCollection))]
public class CompositionRootIntegrationTests
{
    [Fact]
    public void ProductionRegistrationPathBuildsProviderWithAllFoundationMarkers()
    {
        using var host = BuildValidatedHost(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test"
        });

        _ = host.Services.GetRequiredService<ICompositionRootMarker>();
        _ = host.Services.GetRequiredService<IUiFoundationMarker>();
        _ = host.Services.GetRequiredService<IInfrastructureFoundationMarker>();
        _ = host.Services.GetRequiredService<ILoggerFactory>();
    }

    [Fact]
    public void ApplicationOptionsBindAndValidateFromConfiguration()
    {
        using var host = BuildValidatedHost(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test"
        });

        var options = host.Services.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        Assert.Equal("DPV Integration Test", options.ApplicationName);
    }

    [Fact]
    public async Task InvalidApplicationOptionsFailDuringHostStartValidation()
    {
        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = string.Empty
        });

        using var host = builder.Build();
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.False(
            string.IsNullOrWhiteSpace(exception.Message),
            "Invalid options should fail during options validation on host start.");
    }

    [Fact]
    public void AvaloniaShellTypesRegisteredWithoutStartingUiLoop()
    {
        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test"
        });

        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(AvaloniaApplication)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(DPV.UI.MainWindow)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AppSettingsFilesExistInAppProjectForConfigurationIntegration()
    {
        var appSettings = Path.Combine(TestRepository.AppProjectDirectory, "appsettings.json");
        var developmentSettings = Path.Combine(TestRepository.AppProjectDirectory, "appsettings.Development.json");

        Assert.True(File.Exists(appSettings), $"Missing {appSettings}");
        Assert.True(File.Exists(developmentSettings), $"Missing {developmentSettings}");
    }

    private static IHost BuildValidatedHost(IReadOnlyDictionary<string, string?> overrides)
    {
        var builder = CreateIntegrationHostBuilder(overrides);
        return builder.Build();
    }

    private static HostApplicationBuilder CreateIntegrationHostBuilder(IReadOnlyDictionary<string, string?> overrides)
    {
        CompositionRootTestIsolation.ResetLogging();
        var builder = CompositionRootHostFactory.CreateHostBuilder([]);
        builder.Environment.ContentRootPath = TestRepository.AppProjectDirectory;
        builder.Configuration.AddInMemoryCollection(overrides);
        return builder;
    }
}
