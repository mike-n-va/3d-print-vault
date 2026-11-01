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
        var builder = CompositionRootHostFactory.CreateHostBuilder([]);
        builder.Environment.ContentRootPath = TestRepository.AppProjectDirectory;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "DPV Host Lifecycle Test"
        });

        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        _ = host.Services.GetRequiredService<ICompositionRootMarker>();
        _ = host.Services.GetRequiredService<ILoggerFactory>();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
