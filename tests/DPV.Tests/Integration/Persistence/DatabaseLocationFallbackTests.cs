using DPV.App.Configuration;
using DPV.App.Startup;
using DPV.Infrastructure.DependencyInjection;
using DPV.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.Tests.Integration.Persistence;

public class DatabaseLocationFallbackTests
{
    [Fact]
    public void MissingDatabaseLocation_UsesRoamingCatalogFile()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var provider = new ServiceCollection().AddDpvInfrastructure(configuration).BuildServiceProvider();

        var factory = provider.GetRequiredService<IDatabaseConnectionFactory>();
        var expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "3DPrintVault",
            "dpv.db"));

        Assert.Equal(DatabaseOptions.DefaultLocation, "%AppData%/3DPrintVault/dpv.db");
        Assert.Equal(expected, factory.DatabasePath);
    }

    [Fact]
    public void ExplicitDatabaseLocation_OverridesTheRoamingFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Location"] = @"C:\catalog\custom.db"
            })
            .Build();
        using var provider = new ServiceCollection().AddDpvInfrastructure(configuration).BuildServiceProvider();

        var factory = provider.GetRequiredService<IDatabaseConnectionFactory>();

        Assert.Equal(Path.GetFullPath(@"C:\catalog\custom.db"), factory.DatabasePath);
    }

    [Fact]
    public void HostLoadsSettingsFromTheApplicationBaseDirectory()
    {
        var builder = CompositionRootHostFactory.CreateHostBuilder([]);

        Assert.Equal(AppContext.BaseDirectory, builder.Environment.ContentRootPath);
    }
}
