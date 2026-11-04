namespace DPV.Domain.Contracts.Catalog;

public sealed record TopArtistDto(string Name, int ModelCount);

public sealed record TopCollectionDto(string Name, int ModelCount);

public sealed record TopTagDto(string Name, int ModelCount);

public sealed record LibraryStatsDto(
    long TotalModels,
    long TotalFiles,
    long TotalBytesUsed,
    int TotalArtists,
    int TotalCollections,
    int TotalTags,
    IReadOnlyCollection<TopArtistDto> TopArtists,
    IReadOnlyCollection<TopCollectionDto> TopCollections,
    IReadOnlyCollection<TopTagDto> TopTags);

public interface ILibraryStatsRepository
{
    Task<LibraryStatsDto> GetLibraryStatsAsync(CancellationToken cancellationToken = default);
}
