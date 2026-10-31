using DPV.App.Configuration;
using DPV.Infrastructure.DependencyInjection;
using DPV.UI.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.App.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddDpvApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ICompositionRootMarker, CompositionRootMarker>();
        services.AddDpvConfigurationOptions(configuration);
        return services;
    }
}

internal sealed class CompositionRootMarker : ICompositionRootMarker;
