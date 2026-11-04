namespace DPV.Domain.Contracts.Catalog;

public interface IThumbnailService
{
    Task<string?> GetThumbnailPathAsync(string contentHash, CancellationToken cancellationToken = default);

    Task EnsureThumbnailGeneratedAsync(string contentHash, string stlFilePath, CancellationToken cancellationToken = default);
}
