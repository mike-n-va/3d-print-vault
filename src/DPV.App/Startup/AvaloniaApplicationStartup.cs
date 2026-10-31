using Avalonia;
using AvaloniaApplication = DPV.UI.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DPV.App.Startup;

public static class AvaloniaApplicationStartup
{
    public static int Start(IHost host, string[] args)
    {
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DPV.App.Startup");
        logger.LogInformation("Starting Avalonia desktop lifetime.");

        var exitCode = BuildAvaloniaApp(host.Services)
            .StartWithClassicDesktopLifetime(args);

        logger.LogInformation("Avalonia desktop lifetime exited with code {ExitCode}.", exitCode);
        return exitCode;
    }

    internal static AppBuilder BuildAvaloniaApp(IServiceProvider services)
        => AppBuilder.Configure(() => services.GetRequiredService<AvaloniaApplication>())
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
