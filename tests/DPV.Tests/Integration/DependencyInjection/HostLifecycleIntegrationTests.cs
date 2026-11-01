using DPV.App.DependencyInjection;
using DPV.App.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DPV.Tests.Support;

namespace DPV.Tests.Integration.DependencyInjection;

[Collection(nameof(CompositionRootIntegrationCollection))]
public class HostLifecycleIntegrationTests
{
    [Fact]
    public async Task HostStartsAndStopsWithoutLifecycleErrors()
    {
        CompositionRootTestIsolation.ResetLogging();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"dpv-host-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        var builder = CompositionRootHostFactory.CreateHostBuilder([]);
        builder.Environment.ContentRootPath = TestRepository.AppProjectDirectory;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Host Lifecycle Test",
            ["Database:Location"] = Path.Combine(temporaryDirectory, "test.db"),
            ["Serilog:WriteTo:1:Args:path"] = Path.Combine(temporaryDirectory, "logs", "dpv-.log")
        });

        try
        {
            using var host = builder.Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            _ = host.Services.GetRequiredService<ICompositionRootMarker>();
            _ = host.Services.GetRequiredService<ILoggerFactory>();
            await host.StopAsync(TestContext.Current.CancellationToken);
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
