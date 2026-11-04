using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Import;
using DPV.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<IModelImportService, ModelImportService>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IHostedService>(
            provider => provider.GetRequiredService<DatabaseInitializer>());

        return services;
    }
}

internal sealed class InfrastructureFoundationMarker : IInfrastructureFoundationMarker;
