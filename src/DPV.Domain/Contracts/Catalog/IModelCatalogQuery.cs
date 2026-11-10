namespace DPV.Domain.Contracts.Catalog;

public record ModelQueryParameters(
    string? SearchTerm = null,
    string? TagFilter = null,
    string? ArtistFilter = null,
    string? CollectionFilter = null,
    bool OnlyFavorites = false,
    bool IncludeInactive = false,
    int Limit = 100,
    int Offset = 0,
    string? ScanRoot = null,
    Guid? ReleaseId = null);

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
    string? ThumbnailPath = null,
    string? ArtistName = null,
    string? CollectionName = null,
    int AssetCount = 1,
    double ConfidenceScore = 1.0,
    string ConfidenceTier = "HighConfidence",
    string DiscoveryMethod = "Deterministic",
    IReadOnlyList<string>? Evidence = null,
    bool IsFavorite = false,
    string? Scale = null,
    string? FolderPath = null,
    string? ActiveImagePath = null);
