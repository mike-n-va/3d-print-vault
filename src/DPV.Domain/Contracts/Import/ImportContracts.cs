namespace DPV.Domain.Contracts.Import;

public interface IModelImportService
{
    Task<ImportLibraryResult> ImportAsync(
        ImportLibraryRequest request,
        CancellationToken cancellationToken = default,
        IProgress<ImportProgress>? progress = null);
}

public interface IModelCatalogQuery
{
    Task<IReadOnlyList<CatalogModelSummary>> ListAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}

public sealed record ImportProgress(int ProcessedCount, int TotalCount, string StatusText)
{
    public double Percentage =>
        TotalCount <= 0 ? 0 : Math.Clamp(ProcessedCount * 100d / TotalCount, 0, 100);
}

public sealed record CatalogModelSummary(
    Guid Id,
    string Name,
    string NormalizedPath,
    string ContentHash,
    long FileSizeBytes,
    bool IsActive);

public sealed record ImportLibraryRequest(string RootPath, long MaxFileSizeBytes, int BatchLimit = 500);

public sealed record ImportLibraryResult(
    int TotalSkipped,
    int TotalAdded,
    int TotalUpdated,
    int TotalMoved,
    int TotalInactivated,
    bool Completed,
    IReadOnlyList<ImportError> Errors);

public sealed record ImportError(string Code, string? Path, string Message);

public static class ImportErrorCodes
{
    public const string IoAccess = "ERR_IO_ACCESS";
    public const string InvalidFormat = "ERR_INVALID_FORMAT";
    public const string HashFailure = "ERR_HASH_FAILURE";
    public const string FileTooLarge = "ERR_FILE_TOO_LARGE";
}
