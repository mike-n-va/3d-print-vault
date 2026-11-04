using System.Runtime.InteropServices;
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
    public static int Main(string[] args)
    {
        try
        {
            var builder = CompositionRootHostFactory.CreateHostBuilder(args);
            using var host = builder.Build();

            ValidateCompositionRoot(host.Services);

            host.StartAsync().GetAwaiter().GetResult();

            try
            {
                return AvaloniaApplicationStartup.Start(host, args);
            }
            finally
            {
                host.StopAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application terminated unexpectedly during startup.");
            WriteCriticalStartupException(exception);
            return 1;
        }
        finally
        {
            BootstrapLogging.CloseAndFlush();
        }
    }

    private static void WriteCriticalStartupException(Exception exception)
    {
        try
        {
            if (OperatingSystem.IsWindows() && GetConsoleWindow() == IntPtr.Zero)
            {
                AllocConsole();
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("[CRITICAL STARTUP EXCEPTION]");
            Console.WriteLine(exception);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(intercept: true);
        }
        catch (Exception)
        {
            // A redirected or missing console cannot accept the exit prompt.
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private static void ValidateCompositionRoot(IServiceProvider services)
    {
        _ = services.GetRequiredService<ICompositionRootMarker>();
        _ = services.GetRequiredService<IUiFoundationMarker>();
        _ = services.GetRequiredService<IInfrastructureFoundationMarker>();
        _ = services.GetRequiredService<ILoggerFactory>();
    }
}

