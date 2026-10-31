using Microsoft.Extensions.DependencyInjection;

namespace DPV.UI.DependencyInjection;

public interface IUiFoundationMarker
{
}

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddDpvUi(this IServiceCollection services)
    {
        services.AddSingleton<App>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<IUiFoundationMarker, UiFoundationMarker>();
        return services;
    }
}

internal sealed class UiFoundationMarker : IUiFoundationMarker;
