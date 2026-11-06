using System.Text.Json;
using DPV.App.Configuration;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using DPV.Infrastructure.Workers;
using DPV.App.DependencyInjection;
using DPV.App.Startup;
using DPV.Infrastructure.DependencyInjection;
using AvaloniaApplication = DPV.UI.App;
using DPV.UI.DependencyInjection;
using DPV.UI.ViewModels;
using DPV.UI.Views;
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
            ["Application:ApplicationName"] = "DPV Integration Test",
            ["Database:Location"] = "integration-test.db"
        });

        var applicationOptions = host.Services.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        var databaseOptions = host.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var scanningOptions = host.Services.GetRequiredService<IOptions<ScanningOptions>>().Value;
        var viewerOptions = host.Services.GetRequiredService<IOptions<ViewerOptions>>().Value;

        Assert.Equal("DPV Integration Test", applicationOptions.ApplicationName);
        Assert.Equal("integration-test.db", databaseOptions.Location);
        Assert.NotNull(scanningOptions);
        Assert.NotNull(viewerOptions);
    }

    [Fact]
    public void AppSettingsFilesDeclareRequiredConfigurationSections()
    {
        var appSettings = Path.Combine(TestRepository.AppProjectDirectory, "appsettings.json");
        var developmentSettings = Path.Combine(TestRepository.AppProjectDirectory, "appsettings.Development.json");

        using var productionDocument = JsonDocument.Parse(File.ReadAllText(appSettings));
        using var developmentDocument = JsonDocument.Parse(File.ReadAllText(developmentSettings));

        foreach (var sectionName in new[] { "Database", "Scanning", "Viewer", "Logging" })
        {
            Assert.Equal(JsonValueKind.Object, productionDocument.RootElement.GetProperty(sectionName).ValueKind);
            Assert.Equal(JsonValueKind.Object, developmentDocument.RootElement.GetProperty(sectionName).ValueKind);
        }

        Assert.Equal(
            "%AppData%/3DPrintVault/",
            productionDocument.RootElement.GetProperty("Database").GetProperty("Location").GetString());
        Assert.Equal(
            "/data/dev.db",
            developmentDocument.RootElement.GetProperty("Database").GetProperty("Location").GetString());
    }

    [Theory]
    [InlineData("appsettings.json", "%AppData%/3DPrintVault/")]
    [InlineData("appsettings.Development.json", "/data/dev.db")]
    public void AppSettingsBindAllDeclaredOptions(string fileName, string expectedDatabaseLocation)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestRepository.AppProjectDirectory)
            .AddJsonFile(fileName)
            .Build();
        var services = new ServiceCollection();
        services.AddDpvConfigurationOptions(configuration);

        using var provider = services.BuildServiceProvider();
        var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var scanningOptions = provider.GetRequiredService<IOptions<ScanningOptions>>().Value;
        var viewerOptions = provider.GetRequiredService<IOptions<ViewerOptions>>().Value;

        Assert.Equal(expectedDatabaseLocation, databaseOptions.Location);
        Assert.NotNull(scanningOptions);
        Assert.NotNull(viewerOptions);
    }

    [Fact]
    public async Task EmptyDatabaseLocationUsesTheRoamingCatalog()
    {
        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test",
            ["Database:Location"] = string.Empty
        });

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var factory = host.Services.GetRequiredService<IDatabaseConnectionFactory>();
        var expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "3DPrintVault",
            "dpv.db"));

        Assert.Equal(expected, factory.DatabasePath);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidScanningMaxFileSizeFailsDuringHostStartValidation()
    {
        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test",
            ["Scanning:MaxFileSizeBytes"] = "50"
        });

        using var host = builder.Build();
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("MaxFileSizeBytes", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhitespaceScanningRootPathFailsDuringHostStartValidation()
    {
        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test",
            ["Scanning:RootPath"] = " "
        });

        using var host = builder.Build();
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("RootPath", exception.ToString(), StringComparison.Ordinal);
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
    public void ImportPipelineAndDashboardResolveFromScanningOptions()
    {
        using var host = BuildValidatedHost(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test",
            ["Scanning:RootPath"] = @"C:\library",
            ["Scanning:BatchLimit"] = "25",
            ["Scanning:MaxFileSizeBytes"] = "4096"
        });

        Assert.IsType<ModelImportService>(host.Services.GetRequiredService<IModelImportService>());
        Assert.IsType<SqliteModelCatalogQuery>(host.Services.GetRequiredService<IModelCatalogQuery>());
        Assert.IsType<DapperCatalogQuery>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IModelCatalogQuery>());
        Assert.IsType<DapperModelDetailRepository>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IModelDetailRepository>());
        Assert.IsType<DapperDuplicateRepository>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IDuplicateRepository>());
        Assert.IsType<ThumbnailService>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IThumbnailService>());
        Assert.IsType<DapperLibraryStatsRepository>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.ILibraryStatsRepository>());
        Assert.IsType<CatalogExportService>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.ICatalogExportService>());
        Assert.IsType<SqliteDbMaintenanceService>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IDbMaintenanceService>());
        Assert.IsType<BackgroundScanQueue>(host.Services.GetRequiredService<DPV.Domain.Contracts.Catalog.IBackgroundScanQueue>());
        var worker = host.Services.GetRequiredService<ScanBackgroundWorker>();
        Assert.Contains(host.Services.GetServices<IHostedService>(), service => ReferenceEquals(service, worker));
        Assert.IsType<SqliteConnectionFactory>(host.Services.GetRequiredService<IDatabaseConnectionFactory>());
        Assert.NotNull(host.Services.GetRequiredService<MigrationRunner>());
        Assert.NotNull(host.Services.GetRequiredService<SqliteCatalogStore>());

        var dashboard = host.Services.GetRequiredService<DashboardViewModel>();
        Assert.Equal(@"C:\library", dashboard.RootPath);
        Assert.Equal(25, dashboard.BatchLimit);
        Assert.Equal(4096, dashboard.MaxFileSizeBytes);
        Assert.True(dashboard.ImportCommand.CanExecute(null));
        dashboard.RootPath = " ";
        Assert.False(dashboard.ImportCommand.CanExecute(null));
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
    public void DashboardIsRegisteredAndSelectedOnStartup()
    {
        typeof(Avalonia.Threading.Dispatcher)
            .GetField("_thread", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(Avalonia.Threading.Dispatcher.UIThread, System.Threading.Thread.CurrentThread);

        var builder = CreateIntegrationHostBuilder(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Integration Test"
        });

        using var host = builder.Build();
        var dashboard = host.Services.GetRequiredService<DashboardViewModel>();
        var shell = host.Services.GetRequiredService<ShellViewModel>();
        var dashboardView = host.Services.GetRequiredService<DashboardView>();

        Assert.Same(dashboard, shell.CurrentViewModel);
        Assert.IsType<DashboardView>(dashboardView);
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

        var configurationValues = new Dictionary<string, string?>
        {
            ["Database:Location"] = "integration-test.db"
        };
        foreach (var (key, value) in overrides)
        {
            configurationValues[key] = value;
        }

        builder.Configuration.AddInMemoryCollection(configurationValues);
        return builder;
    }
}
