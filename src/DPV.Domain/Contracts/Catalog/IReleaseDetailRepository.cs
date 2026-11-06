using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Catalog;

public sealed record ReleaseDetailDto(
    Guid ReleaseId,
    string Name,
    string? ArtistName,
    string? Description,
    int ModelCount,
    string? CoverImagePath,
    bool IsFavorite,
    string? ReleaseDate,
    IReadOnlyList<ModelSummaryDto> Models,
    Guid? ArtistId = null,
    DateTime? CreatedAt = null);

public interface IReleaseDetailRepository
{
    Task<ReleaseDetailDto?> GetReleaseDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task ToggleReleaseFavoriteAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default);

    Task UpdateReleaseCoverAsync(
        Guid releaseId,
        string coverPath,
        CancellationToken cancellationToken = default);

    Task UpdateReleaseDescriptionAsync(
        Guid releaseId,
        string? description,
        CancellationToken cancellationToken = default);
}
