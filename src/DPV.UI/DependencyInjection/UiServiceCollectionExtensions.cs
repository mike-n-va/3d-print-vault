using Microsoft.Extensions.DependencyInjection;
using DPV.UI.ViewModels;
using DPV.UI.Views;

namespace DPV.UI.DependencyInjection;

public interface IUiFoundationMarker
{
}

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddDpvUi(this IServiceCollection services)
    {
        services.AddSingleton<App>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DuplicatesViewModel>();
        services.AddSingleton<AnalyticsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<DashboardView>();
        services.AddTransient<DuplicatesView>();
        services.AddTransient<AnalyticsView>();
        services.AddTransient<SettingsView>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<IUiFoundationMarker, UiFoundationMarker>();
        return services;
    }
}

internal sealed class UiFoundationMarker : IUiFoundationMarker;
