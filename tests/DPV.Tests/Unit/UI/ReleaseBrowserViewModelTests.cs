using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.UI.ViewModels;
using Xunit;

namespace DPV.Tests.Unit.UI;

public class ReleaseBrowserViewModelTests
{
    [Fact]
    public async Task ReleaseBrowserViewModel_LoadsAndFiltersReleases()
    {
        var rel1 = new ReleaseSummaryDto(Guid.NewGuid(), "Crimson Paladins", "Artisan Guild", "Paladins set", 18, null, false);
        var rel2 = new ReleaseSummaryDto(Guid.NewGuid(), "Blood Reapers", "TitanForge", "Orc set", 12, null, true);
        var fakeQuery = new FakeReleaseCatalogQuery([rel1, rel2]);
        var fakeRepo = new FakeReleaseDetailRepository();

        ReleaseSummaryDto? selected = null;
        var vm = new ReleaseBrowserViewModel(fakeQuery, fakeRepo, r => selected = r);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, vm.Releases.Count);

        // Filter by text
        vm.SearchFilter = "Crimson";
        Assert.Single(vm.Releases);
        Assert.Equal("Crimson Paladins", vm.Releases[0].Name);

        // Clear filter
        vm.SearchFilter = "";
        Assert.Equal(2, vm.Releases.Count);

        // Select release command
        vm.SelectReleaseCommand.Execute(rel1);
        Assert.Same(rel1, selected);
    }

    [Fact]
    public async Task ReleaseBrowserViewModel_TogglesFavorite()
    {
        var rel = new ReleaseSummaryDto(Guid.NewGuid(), "Crimson Paladins", "Artisan Guild", "Paladins", 18, null, false);
        var fakeQuery = new FakeReleaseCatalogQuery([rel]);
        var fakeRepo = new FakeReleaseDetailRepository();
        var vm = new ReleaseBrowserViewModel(fakeQuery, fakeRepo, _ => { });

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(vm.Releases[0].IsFavorite);

        await vm.ToggleFavoriteAsync(vm.Releases[0]);
        Assert.True(vm.Releases[0].IsFavorite);
        Assert.Contains(rel.ReleaseId, fakeRepo.ToggledFavorites);
    }

    [Fact]
    public async Task ReleaseDetailViewModel_LoadsDetailAndAdjustsDensity()
    {
        var relId = Guid.NewGuid();
        var model1 = new ModelSummaryDto(Guid.NewGuid(), "Paladin Commander", "path1.stl", 100, "now", true);
        var model2 = new ModelSummaryDto(Guid.NewGuid(), "Paladin Knight", "path2.stl", 100, "now", true);
        var detail = new ReleaseDetailDto(relId, "Crimson Paladins", "Artisan Guild", "Epic paladins", 2, "cover.png", false, "March 2026", [model1, model2]);

        var fakeRepo = new FakeReleaseDetailRepository { DetailToReturn = detail };
        var backed = false;
        var vm = new ReleaseDetailViewModel(fakeRepo, () => backed = true);

        await vm.LoadReleaseAsync(relId, TestContext.Current.CancellationToken);

        Assert.Equal("Crimson Paladins", vm.Name);
        Assert.Equal("Artisan Guild", vm.ArtistName);
        Assert.Equal(2, vm.Models.Count);

        // Density controls
        vm.SetDensitySmallCommand.Execute(null);
        Assert.Equal("Small", vm.CardDensity);
        Assert.Equal(140, vm.CardWidth);

        vm.SetDensityLargeCommand.Execute(null);
        Assert.Equal("Large", vm.CardDensity);
        Assert.Equal(280, vm.CardWidth);

        vm.SetDensityMediumCommand.Execute(null);
        Assert.Equal("Medium", vm.CardDensity);
        Assert.Equal(200, vm.CardWidth);

        // Back command
        vm.BackCommand.Execute(null);
        Assert.True(backed);
    }

    [Fact]
    public async Task FavoritesViewModel_LoadsMultiCategoryAndSwitchesView()
    {
        var model = new ModelSummaryDto(Guid.NewGuid(), "Paladin Commander", "path1.stl", 100, "now", true, IsFavorite: true);
        var release = new ReleaseSummaryDto(Guid.NewGuid(), "Crimson Paladins", "Artisan Guild", "Paladins", 18, null, true);
        var artist = new ArtistGroupDto("Artisan Guild", 18, IsFavorite: true);

        var modelQuery = new FakeModelCatalogQuery([model]);
        var modelRepo = new FakeModelDetailRepo();
        var relQuery = new FakeReleaseCatalogQuery([release]);
        var relRepo = new FakeReleaseDetailRepository();
        var groupQuery = new FakeGroupingQuery([artist]);

        var vm = new FavoritesViewModel(modelQuery, modelRepo, relQuery, relRepo, groupQuery);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, vm.ModelsCount);
        Assert.Equal(1, vm.ReleasesCount);
        Assert.Equal(1, vm.ArtistsCount);

        // Default category is Models
        Assert.True(vm.IsModelsSelected);
        Assert.Single(vm.FavoriteModels);

        // Switch to Releases
        vm.SelectCategoryReleasesCommand.Execute(null);
        Assert.True(vm.IsReleasesSelected);
        Assert.Single(vm.FavoriteReleases);

        // Switch to Artists
        vm.SelectCategoryArtistsCommand.Execute(null);
        Assert.True(vm.IsArtistsSelected);
        Assert.Single(vm.FavoriteArtists);
    }

    [Fact]
    public async Task ArtistBrowserViewModel_OpensArtistPortfolio_AndDrillsDownToRelease()
    {
        var artist = new ArtistGroupDto("Artisan Guild", 42);
        var release = new ReleaseSummaryDto(Guid.NewGuid(), "Crimson Paladins", "Artisan Guild", "Paladins", 18, null, false);

        var groupQuery = new FakeGroupingQuery([artist]);
        var relQuery = new FakeReleaseCatalogQuery([release]);

        ReleaseSummaryDto? drilledRelease = null;
        var vm = new ArtistBrowserViewModel(
            groupQuery,
            _ => { },
            relQuery,
            r => drilledRelease = r);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(vm.Artists);
        Assert.False(vm.IsPortfolioMode);

        // Click artist -> enters portfolio mode
        vm.SelectArtistCommand.Execute(artist);
        Assert.True(vm.IsPortfolioMode);
        Assert.Equal("Artisan Guild", vm.SelectedArtist!.Name);

        // Simulate release click in portfolio
        vm.SelectReleaseCommand.Execute(release);
        Assert.Same(release, drilledRelease);

        // Back button returns to overview
        vm.BackToArtistsCommand.Execute(null);
        Assert.False(vm.IsPortfolioMode);
    }

    private sealed class FakeReleaseCatalogQuery(IEnumerable<ReleaseSummaryDto> releases) : IReleaseCatalogQuery
    {
        private readonly List<ReleaseSummaryDto> _releases = releases.ToList();

        public Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesAsync(ReleaseQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var result = _releases.AsEnumerable();
            if (parameters.OnlyFavorites)
            {
                result = result.Where(r => r.IsFavorite);
            }
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                result = result.Where(r => r.Name.Contains(parameters.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(parameters.ArtistName))
            {
                result = result.Where(r => r.ArtistName == parameters.ArtistName);
            }
            return Task.FromResult<IReadOnlyCollection<ReleaseSummaryDto>>(result.ToList());
        }

        public Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesByArtistAsync(Guid artistId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReleaseSummaryDto>>(_releases);

        public Task<ReleaseStatisticsDto> GetReleaseStatisticsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReleaseStatisticsDto(_releases.Count, _releases.Sum(r => r.ModelCount), _releases.Count(r => r.IsFavorite)));
    }

    private sealed class FakeReleaseDetailRepository : IReleaseDetailRepository
    {
        public List<Guid> ToggledFavorites { get; } = [];
        public ReleaseDetailDto? DetailToReturn { get; set; }

        public Task<ReleaseDetailDto?> GetReleaseDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DetailToReturn);

        public Task ToggleReleaseFavoriteAsync(Guid releaseId, CancellationToken cancellationToken = default)
        {
            ToggledFavorites.Add(releaseId);
            return Task.CompletedTask;
        }

        public Task UpdateReleaseCoverAsync(Guid releaseId, string coverPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateReleaseDescriptionAsync(Guid releaseId, string? description, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeModelCatalogQuery(IEnumerable<ModelSummaryDto> models) : IModelCatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>(models.ToList());
    }

    private sealed class FakeModelDetailRepo : IModelDetailRepository
    {
        public Task<ModelDetailDto?> GetModelDetailAsync(Guid modelId, CancellationToken cancellationToken = default) => Task.FromResult<ModelDetailDto?>(null);
        public Task UpdateModelMetadataAsync(Guid modelId, string? artistName, string? collectionName, IEnumerable<string> tags, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ToggleFavoriteAsync(Guid modelId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateModelDescriptionAsync(Guid modelId, string? description, string? printNotes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGroupingQuery(IEnumerable<ArtistGroupDto> artists) : IModelGroupingQuery
    {
        public Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtistGroupDto>>(artists.ToList());
        public Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionGroupDto>>([]);
        public Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TagGroupDto>>([]);
        public Task<long> GetFavoritesCountAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<long>(artists.Count(a => a.IsFavorite));
        public Task ToggleArtistFavoriteAsync(string artistName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
