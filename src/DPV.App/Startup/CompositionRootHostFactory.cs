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

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = ResolveContentRoot()
        });

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

    internal static string ResolveContentRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDirectory, "appsettings.json")))
        {
            return baseDirectory;
        }

        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(processDirectory)
            && File.Exists(Path.Combine(processDirectory, "appsettings.json")))
        {
            return processDirectory;
        }

        return baseDirectory;
    }
}
