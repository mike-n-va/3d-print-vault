using DPV.App.Logging;
using Serilog;

namespace DPV.Tests.Integration.DependencyInjection;

[CollectionDefinition(nameof(CompositionRootIntegrationCollection), DisableParallelization = true)]
public sealed class CompositionRootIntegrationCollection : ICollectionFixture<CompositionRootTestIsolation>;

public sealed class CompositionRootTestIsolation : IDisposable
{
    public CompositionRootTestIsolation() => ResetLogging();

    public void Dispose() => ResetLogging();

    internal static void ResetLogging()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch (ObjectDisposedException)
        {
        }

        BootstrapLogging.Initialize();
    }
}
