using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.App.Configuration;

public static class OptionsRegistration
{
    public static IServiceCollection AddDpvConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ApplicationOptions>()
            .Bind(configuration.GetSection(ApplicationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ScanningOptions>()
            .Bind(configuration.GetSection(ScanningOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<ViewerOptions>()
            .Bind(configuration.GetSection(ViewerOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
