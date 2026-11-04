namespace DPV.Domain.Contracts.Catalog;

public interface ICatalogExportService
{
    Task<string> ExportToCsvAsync(CancellationToken cancellationToken = default);

    Task<string> ExportToJsonAsync(CancellationToken cancellationToken = default);
}
