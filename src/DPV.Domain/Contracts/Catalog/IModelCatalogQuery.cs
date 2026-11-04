namespace DPV.Domain.Contracts.Catalog;

public record ModelQueryParameters(
    string? SearchTerm = null,
    string? TagFilter = null,
    bool IncludeInactive = false,
    int Limit = 100,
    int Offset = 0,
    string? ScanRoot = null);

public interface IModelCatalogQuery
{
    Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(
        ModelQueryParameters parameters,
        CancellationToken cancellationToken = default);
}

public sealed record ModelSummaryDto(
    Guid Id,
    string Name,
    string FilePath,
    long FileSizeBytes,
    string LastModified,
    bool IsActive,
    string ContentHash = "",
    string? ThumbnailPath = null);
