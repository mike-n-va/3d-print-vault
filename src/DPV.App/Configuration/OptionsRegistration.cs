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

        return services;
    }
}
