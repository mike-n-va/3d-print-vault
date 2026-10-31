using DPV.App.DependencyInjection;
using DPV.Infrastructure.DependencyInjection;
using DPV.UI.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.App.DependencyInjection;

public static class DependencyInjectionOrchestrator
{
    public static void RegisterApplicationModules(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDpvApplication(configuration);
        services.AddDpvInfrastructure(configuration);
        services.AddDpvUi();
    }
}
