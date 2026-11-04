using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using DPV.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DPV.Infrastructure.DependencyInjection;

public interface IInfrastructureFoundationMarker
{
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDpvInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IInfrastructureFoundationMarker, InfrastructureFoundationMarker>();
        services.AddSingleton<IDatabaseConnectionFactory>(
            _ =>
            {
                var location = configuration["Database:Location"];
                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new InvalidOperationException(
                        "Configuration value 'Database:Location' is required for SQLite persistence.");
                }

                return new SqliteConnectionFactory(location);
            });
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<SqliteCatalogStore>();
        services.AddSingleton<IModelCatalogQuery, SqliteModelCatalogQuery>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IModelCatalogQuery, DapperCatalogQuery>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IModelDetailRepository, DapperModelDetailRepository>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IDuplicateRepository, DapperDuplicateRepository>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IThumbnailService, ThumbnailService>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.ILibraryStatsRepository, DapperLibraryStatsRepository>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.ICatalogExportService, CatalogExportService>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IDbMaintenanceService, SqliteDbMaintenanceService>();
        services.AddSingleton<IModelImportService, ModelImportService>();
        services.AddSingleton<DPV.Domain.Contracts.Catalog.IBackgroundScanQueue>(_ => new BackgroundScanQueue());
        services.AddSingleton(provider => new ScanBackgroundWorker(
            provider.GetRequiredService<DPV.Domain.Contracts.Catalog.IBackgroundScanQueue>(),
            provider.GetRequiredService<IModelImportService>(),
            provider.GetRequiredService<ILogger<ScanBackgroundWorker>>(),
            ReadInt64(configuration, "Scanning:MaxFileSizeBytes", 2L * 1024 * 1024 * 1024),
            ReadInt32(configuration, "Scanning:BatchLimit", 500)));
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ScanBackgroundWorker>());
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IHostedService>(
            provider => provider.GetRequiredService<DatabaseInitializer>());

        return services;
    }

    private static long ReadInt64(IConfiguration configuration, string key, long fallback) =>
        long.TryParse(configuration[key], out var value) ? value : fallback;

    private static int ReadInt32(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) ? value : fallback;
}

internal sealed class InfrastructureFoundationMarker : IInfrastructureFoundationMarker;
