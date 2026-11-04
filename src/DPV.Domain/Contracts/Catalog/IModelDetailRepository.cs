namespace DPV.Domain.Contracts.Catalog;

public record ModelFileDto(
    string FilePath,
    long FileSizeBytes,
    string FileHash,
    DateTime LastModified,
    bool IsActive);

public record ModelDetailDto(
    Guid Id,
    string Name,
    string? ArtistName,
    string? CollectionName,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<ModelFileDto> Files,
    DateTime CreatedAt);

public interface IModelDetailRepository
{
    Task<ModelDetailDto?> GetModelDetailAsync(Guid modelId, CancellationToken cancellationToken = default);

    Task UpdateModelMetadataAsync(
        Guid modelId,
        string? artistName,
        string? collectionName,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);
}
