using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Catalog;

public sealed record ReleaseQueryParameters(
    string? SearchTerm = null,
    Guid? ArtistId = null,
    string? ArtistName = null,
    bool OnlyFavorites = false,
    int Limit = 100,
    int Offset = 0,
    string? ScanRoot = null);

public sealed record ReleaseSummaryDto(
    Guid ReleaseId,
    string Name,
    string? ArtistName,
    string? Description,
    int ModelCount,
    string? CoverImagePath,
    bool IsFavorite,
    string? ReleaseDate = null,
    Guid? ArtistId = null,
    string? FolderPath = null);

public sealed record ReleaseStatisticsDto(
    int TotalReleases,
    int TotalModelsInReleases,
    int FavoritedReleases);

public interface IReleaseCatalogQuery
{
    Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesAsync(
        ReleaseQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesByArtistAsync(
        Guid artistId,
        CancellationToken cancellationToken = default);

    Task<ReleaseStatisticsDto> GetReleaseStatisticsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default);
}
