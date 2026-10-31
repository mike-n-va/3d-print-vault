using DPV.App.DependencyInjection;
using DPV.App.Logging;
using DPV.App.Startup;
using DPV.Infrastructure.DependencyInjection;
using DPV.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DPV.App;

public static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var builder = CompositionRootHostFactory.CreateHostBuilder(args);
            using var host = builder.Build();

            ValidateCompositionRoot(host.Services);

            await host.StartAsync().ConfigureAwait(false);

            try
            {
                return AvaloniaApplicationStartup.Start(host, args);
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application terminated unexpectedly during startup.");
            return 1;
        }
        finally
        {
            BootstrapLogging.CloseAndFlush();
        }
    }

    private static void ValidateCompositionRoot(IServiceProvider services)
    {
        _ = services.GetRequiredService<ICompositionRootMarker>();
        _ = services.GetRequiredService<IUiFoundationMarker>();
        _ = services.GetRequiredService<IInfrastructureFoundationMarker>();
        _ = services.GetRequiredService<ILoggerFactory>();
    }
}
