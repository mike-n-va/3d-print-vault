using System.Text.Json;
using DPV.App.Logging;
using DPV.App.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DPV.Tests.Integration.DependencyInjection;
using DPV.Tests.Support;

namespace DPV.Tests.Integration.Logging;

[Collection(nameof(CompositionRootIntegrationCollection))]
public class FileLoggingIntegrationTests
{
    [Fact]
    public void AppSettingsEnableConsoleAndRollingFileSinks()
    {
        var settingsPath = Path.Combine(
            TestRepository.AppProjectDirectory,
            "appsettings.json");

        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var sinks = settings.RootElement
            .GetProperty("Serilog")
            .GetProperty("WriteTo")
            .EnumerateArray()
            .ToArray();

        Assert.Contains(
            sinks,
            sink => sink.GetProperty("Name").GetString() == "Console");
        var fileSink = Assert.Single(
            sinks,
            sink => sink.GetProperty("Name").GetString() == "File");
        Assert.Equal(
            "logs/dpv-.log",
            fileSink.GetProperty("Args").GetProperty("path").GetString());
        Assert.Equal(
            "Day",
            fileSink.GetProperty("Args").GetProperty("rollingInterval").GetString());
    }

    [Fact]
    public async Task HostStartupWritesConfiguredLogEventsToFile()
    {
        CompositionRootTestIsolation.ResetLogging();

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"dpv-issue-7-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        var logDirectory = Path.Combine(temporaryDirectory, "logs");
        var logPath = Path.Combine(logDirectory, "dpv-.log");
        var verificationId = Guid.NewGuid().ToString("N");

        try
        {
            var builder = CompositionRootHostFactory.CreateHostBuilder([]);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ApplicationName"] = "Issue 7 logging integration test",
                ["Database:Location"] = Path.Combine(temporaryDirectory, "test.db"),
                ["Serilog:WriteTo:1:Args:path"] = logPath
            });

            using (var host = builder.Build())
            {
                await host.StartAsync(TestContext.Current.CancellationToken);

                var logger = host.Services
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Issue7LoggingIntegration");
                logger.LogInformation(
                    "Issue 7 file sink verification {VerificationId}",
                    verificationId);

                await host.StopAsync(TestContext.Current.CancellationToken);
            }

            BootstrapLogging.CloseAndFlush();

            Assert.True(Directory.Exists(logDirectory));
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            Assert.NotEmpty(logFiles);

            var logContent = string.Join(
                Environment.NewLine,
                logFiles.Select(File.ReadAllText));
            Assert.Contains(verificationId, logContent, StringComparison.Ordinal);
        }
        finally
        {
            CompositionRootTestIsolation.ResetLogging();
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}
