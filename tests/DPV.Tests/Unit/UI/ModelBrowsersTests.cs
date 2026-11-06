using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.UI.ViewModels;

namespace DPV.Tests.Unit.UI;

public class ModelBrowsersTests
{
    [Fact]
    public async Task ArtistBrowser_LoadsAndFiltersArtists()
    {
        var mockQuery = new MockGroupingQuery
        {
            Artists =
            [
                new ArtistGroupDto("TitanForge", 15),
                new ArtistGroupDto("ArtisanGuild", 42),
                new ArtistGroupDto("ArchvillainGames", 30)
            ]
        };

        string? selectedArtist = null;
        var vm = new ArtistBrowserViewModel(mockQuery, artist => selectedArtist = artist);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, vm.Artists.Count);

        vm.SearchFilter = "art";
        Assert.Single(vm.Artists);
        Assert.Equal("ArtisanGuild", vm.Artists[0].Name);

        vm.SelectArtistCommand.Execute("TitanForge");
        Assert.Equal("TitanForge", selectedArtist);
    }

    [Fact]
    public async Task CollectionBrowser_LoadsAndNavigates()
    {
        var mockQuery = new MockGroupingQuery
        {
            Collections =
            [
                new CollectionGroupDto("Dragons", 8),
                new CollectionGroupDto("SciFiVehicles", 12)
            ]
        };

        string? selectedCollection = null;
        var vm = new CollectionBrowserViewModel(mockQuery, col => selectedCollection = col);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, vm.Collections.Count);

        vm.SelectCollectionCommand.Execute("Dragons");
        Assert.Equal("Dragons", selectedCollection);
    }

    [Fact]
    public async Task TagBrowser_LoadsAndFiltersTags()
    {
        var mockQuery = new MockGroupingQuery
        {
            Tags =
            [
                new TagGroupDto("supported", 50),
                new TagGroupDto("unsupported", 50),
                new TagGroupDto("scenery", 10)
            ]
        };

        string? selectedTag = null;
        var vm = new TagBrowserViewModel(mockQuery, tag => selectedTag = tag);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, vm.Tags.Count);

        vm.SearchFilter = "scen";
        Assert.Single(vm.Tags);
        Assert.Equal("scenery", vm.Tags[0].Name);

        vm.SelectTagCommand.Execute("supported");
        Assert.Equal("supported", selectedTag);
    }

    [Fact]
    public void DashboardViewModel_ViewModeAndDensityControlsWork()
    {
        var import = new FakeImportService();
        var catalog = new FakeCatalogQuery();
        var vm = new DashboardViewModel(import, catalog);

        Assert.Equal("Grid", vm.ViewMode);
        Assert.True(vm.IsGridView);
        Assert.False(vm.IsListView);

        vm.SetViewModeCommand.Execute("List");
        Assert.Equal("List", vm.ViewMode);
        Assert.False(vm.IsGridView);
        Assert.True(vm.IsListView);

        vm.SetViewModeCommand.Execute("Details");
        Assert.Equal("Details", vm.ViewMode);
        Assert.True(vm.IsDetailsView);

        // Density checks (S6-002: Small ~140px, Medium ~200px, Large ~280px)
        Assert.Equal("Medium", vm.GridDensity);
        Assert.Equal(200, vm.CardWidth);

        vm.SetGridDensityCommand.Execute("Small");
        Assert.Equal("Small", vm.GridDensity);
        Assert.Equal(140, vm.CardWidth);

        vm.SetGridDensityCommand.Execute("Large");
        Assert.Equal("Large", vm.GridDensity);
        Assert.Equal(280, vm.CardWidth);
    }

    [Fact]
    public async Task DashboardViewModel_MetadataPivotNavigationFiltersCatalog()
    {
        var import = new FakeImportService();
        var catalog = new FakeCatalogQuery();
        var vm = new DashboardViewModel(import, catalog, rootPath: @"C:\vault");

        vm.FilterByArtistCommand.Execute("TitanForge");
        Assert.Equal("TitanForge", vm.ArtistFilter);
        Assert.True(vm.IsFiltered);

        vm.FilterByCollectionCommand.Execute("Dragons");
        Assert.Equal("Dragons", vm.CollectionFilter);

        vm.ClearSearchCommand.Execute(null);
        Assert.Null(vm.ArtistFilter);
        Assert.Null(vm.CollectionFilter);
        Assert.False(vm.IsFiltered);
    }

    private sealed class MockGroupingQuery : IModelGroupingQuery
    {
        public List<ArtistGroupDto> Artists { get; set; } = [];
        public List<CollectionGroupDto> Collections { get; set; } = [];
        public List<TagGroupDto> Tags { get; set; } = [];

        public Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtistGroupDto>>(Artists);

        public Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionGroupDto>>(Collections);

        public Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TagGroupDto>>(Tags);

        public Task<long> GetFavoritesCountAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task ToggleArtistFavoriteAsync(string artistName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeImportService : IModelImportService
    {
        public Task<ImportLibraryResult> ImportAsync(ImportLibraryRequest request, CancellationToken cancellationToken = default, IProgress<ImportProgress>? progress = null) =>
            Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
    }

    private sealed class FakeCatalogQuery : DPV.Domain.Contracts.Catalog.IModelCatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>([]);
    }
}
