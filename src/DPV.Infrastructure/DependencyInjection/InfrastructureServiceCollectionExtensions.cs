using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.Infrastructure.DependencyInjection;

public interface IInfrastructureFoundationMarker
{
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDpvInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        services.AddSingleton<IInfrastructureFoundationMarker, InfrastructureFoundationMarker>();
        return services;
    }
}

internal sealed class InfrastructureFoundationMarker : IInfrastructureFoundationMarker;
