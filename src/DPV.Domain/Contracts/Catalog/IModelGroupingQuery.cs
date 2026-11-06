using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Catalog;

public sealed record ArtistGroupDto(string Name, long ModelCount, bool IsFavorite = false, Guid? Id = null);

public sealed record CollectionGroupDto(string Name, long ModelCount, string? Description = null, bool IsFavorite = false, Guid? Id = null);

public sealed record TagGroupDto(string Name, long ModelCount);

public interface IModelGroupingQuery
{
    Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(string? scanRoot = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(string? scanRoot = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(string? scanRoot = null, CancellationToken cancellationToken = default);

    Task<long> GetFavoritesCountAsync(string? scanRoot = null, CancellationToken cancellationToken = default);

    Task ToggleArtistFavoriteAsync(string artistName, CancellationToken cancellationToken = default);
}
