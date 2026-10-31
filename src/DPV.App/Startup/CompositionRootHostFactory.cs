using DPV.App.DependencyInjection;
using DPV.App.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DPV.App.Startup;

public static class CompositionRootHostFactory
{
    public static HostApplicationBuilder CreateHostBuilder(string[] args)
    {
        BootstrapLogging.Initialize();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Environment.ContentRootPath = AppContext.BaseDirectory;

        builder.Services.Configure<ServiceProviderOptions>(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.Services.AddSerilog((services, loggerConfiguration) =>
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services));

        DependencyInjectionOrchestrator.RegisterApplicationModules(builder.Services, builder.Configuration);

        return builder;
    }
}
