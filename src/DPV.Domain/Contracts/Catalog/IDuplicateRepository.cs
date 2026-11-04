namespace DPV.Domain.Contracts.Catalog;

public sealed record DuplicateFileItem(
    Guid FileId,
    Guid ModelId,
    string FilePath,
    long FileSizeBytes,
    DateTime LastModified,
    bool IsActive,
    string ModelName);

public sealed record DuplicateGroupDto(
    string ContentHash,
    long FileSizeBytes,
    int DuplicateCount,
    IReadOnlyCollection<DuplicateFileItem> Files);

public interface IDuplicateRepository
{
    Task<IReadOnlyCollection<DuplicateGroupDto>> GetDuplicateGroupsAsync(CancellationToken cancellationToken = default);

    Task ResolveDuplicateMergeAsync(
        Guid primaryFileId,
        IEnumerable<Guid> duplicateFileIdsToDeactivate,
        CancellationToken cancellationToken = default);

    Task ResolveDuplicateIgnoreAsync(string contentHash, CancellationToken cancellationToken = default);

    Task ResolveDuplicateDeleteAsync(
        Guid fileIdToDelete,
        string filePath,
        CancellationToken cancellationToken = default);
}
