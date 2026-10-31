using Serilog;

namespace DPV.App.Logging;

public static class BootstrapLogging
{
    public static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
