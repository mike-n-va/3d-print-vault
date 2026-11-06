using DPV.App.Configuration;
using DPV.App.DependencyInjection;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.DependencyInjection;
using DPV.UI.DependencyInjection;
using DPV.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DPV.App.DependencyInjection;

public static class DependencyInjectionOrchestrator
{
    public static void RegisterApplicationModules(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDpvApplication(configuration);
        services.AddDpvInfrastructure(configuration);
        services.AddDpvUi();
        services.AddSingleton(serviceProvider =>
        {
            var scanning = serviceProvider.GetRequiredService<IOptions<ScanningOptions>>().Value;
            return new MainDashboardViewModel(
                serviceProvider.GetRequiredService<IModelImportService>(),
                serviceProvider.GetRequiredService<CatalogQuery>(),
                scanning.MaxFileSizeBytes,
                scanning.BatchLimit,
                scanning.RootPath,
                serviceProvider.GetRequiredService<IModelDetailRepository>(),
                serviceProvider.GetRequiredService<IThumbnailService>(),
                serviceProvider.GetService<IUserPreferencesService>(),
                serviceProvider.GetService<IModelGroupingQuery>(),
                serviceProvider.GetService<IReleaseCatalogQuery>(),
                serviceProvider.GetService<IReleaseDetailRepository>());
        });
        services.AddSingleton<DashboardViewModel>(serviceProvider =>
            serviceProvider.GetRequiredService<MainDashboardViewModel>());
        services.AddSingleton(serviceProvider =>
        {
            var dashboard = serviceProvider.GetRequiredService<MainDashboardViewModel>();
            return new ShellViewModel(
                dashboard,
                serviceProvider.GetService<DuplicatesViewModel>(),
                serviceProvider.GetService<AnalyticsViewModel>(),
                serviceProvider.GetService<SettingsViewModel>(),
                null,
                null,
                null,
                serviceProvider.GetService<IModelGroupingQuery>(),
                serviceProvider.GetService<IReleaseCatalogQuery>(),
                serviceProvider.GetService<IReleaseDetailRepository>());
        });
    }
}
