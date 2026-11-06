using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.UI.Converters;
using DPV.UI.ViewModels;
using Xunit;

namespace DPV.Tests.Unit.UI;

public class DashboardCollectionExperienceTests
{
    [Fact]
    public void FavoriteIconConverter_ReturnsExpectedHearts()
    {
        var converter = new FavoriteIconConverter();
        Assert.Equal("♥", converter.Convert(true, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("♡", converter.Convert(false, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("♡", converter.Convert(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FavoriteBrushConverter_ReturnsExpectedBrushes()
    {
        var converter = new FavoriteBrushConverter();
        var trueBrush = Assert.IsAssignableFrom<ISolidColorBrush>(converter.Convert(true, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));
        var falseBrush = Assert.IsAssignableFrom<ISolidColorBrush>(converter.Convert(false, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));

        Assert.NotEqual(trueBrush.Color, falseBrush.Color);
    }

    [Fact]
    public void InitialsConverter_GeneratesCleanInitials()
    {
        var converter = new InitialsConverter();
        Assert.Equal("AG", converter.Convert("Artisan Guild", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("LS", converter.Convert("Loot Studios", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("CP", converter.Convert("Cast-n-Play", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("TI", converter.Convert("Titan", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("?", converter.Convert(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("?", converter.Convert("", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task DashboardViewModel_FavoritesFilterAndToggle_UpdatesStateAndCount()
    {
        var import = new FakeImport();
        var detailRepo = new FakeDetailRepo();
        var catalog = new FakeCatalog(detailRepo);
        var groupingQuery = new FakeGrouping(detailRepo);

        var model1 = new ModelSummaryDto(
            Id: Guid.NewGuid(),
            Name: "Dragon",
            FilePath: @"C:\vault\Dragon",
            FileSizeBytes: 1024,
            LastModified: DateTime.UtcNow.ToString("O"),
            IsActive: true,
            ArtistName: "Artisan",
            CollectionName: "Dragons",
            AssetCount: 2,
            IsFavorite: false);

        var model2 = new ModelSummaryDto(
            Id: Guid.NewGuid(),
            Name: "Knight",
            FilePath: @"C:\vault\Knight",
            FileSizeBytes: 2048,
            LastModified: DateTime.UtcNow.ToString("O"),
            IsActive: true,
            ArtistName: "Artisan",
            CollectionName: "Knights",
            AssetCount: 1,
            IsFavorite: false);

        catalog.AllModels.Add(model1);
        catalog.AllModels.Add(model2);

        var vm = new DashboardViewModel(
            import,
            catalog,
            rootPath: @"C:\vault",
            detailRepository: detailRepo,
            groupingQuery: groupingQuery);

        await vm.LoadCatalogAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, vm.Models.Count);
        Assert.Equal(0, vm.FavoritesCount);

        // Toggle favorite on model1
        await vm.ToggleFavoriteAsync(model1.Id);
        Assert.Contains(model1.Id, detailRepo.Favorites);
        Assert.True(vm.Models.First(m => m.Id == model1.Id).IsFavorite);
        Assert.Equal(1, vm.FavoritesCount);

        // Filter to only favorites
        vm.OnlyFavoritesFilter = true;
        Assert.True(vm.IsFiltered);
        Assert.Single(vm.Models);
        Assert.Equal(model1.Id, vm.Models[0].Id);

        // Untoggle favorite while filtered
        await vm.ToggleFavoriteAsync(model1.Id);
        Assert.DoesNotContain(model1.Id, detailRepo.Favorites);
        Assert.Empty(vm.Models);
        Assert.Equal(0, vm.FavoritesCount);

        // Clear filter
        vm.ClearSearchCommand.Execute(null);
        Assert.False(vm.OnlyFavoritesFilter);
        Assert.Equal(2, vm.Models.Count);
    }

    [Fact]
    public async Task DashboardViewModel_NotesAndDescription_LoadsAndSavesDrafts()
    {
        var import = new FakeImport();
        var detailRepo = new FakeDetailRepo();
        var catalog = new FakeCatalog(detailRepo);

        var modelId = Guid.NewGuid();
        var summary = new ModelSummaryDto(
            Id: modelId,
            Name: "Ancient Castle",
            FilePath: @"C:\vault\Castle",
            FileSizeBytes: 4096,
            LastModified: DateTime.UtcNow.ToString("O"),
            IsActive: true,
            ArtistName: "Builder",
            CollectionName: "Castles",
            AssetCount: 1,
            IsFavorite: false);
        catalog.AllModels.Add(summary);

        detailRepo.Details[modelId] = new ModelDetailDto(
            modelId, "Ancient Castle", "Builder", "Castles", [], [], DateTime.UtcNow,
            1.0, "HighConfidence", "Deterministic", null, null, false,
            "Initial Description", "Initial Print Notes");

        var vm = new DashboardViewModel(
            import,
            catalog,
            rootPath: @"C:\vault",
            detailRepository: detailRepo);

        await vm.LoadCatalogAsync(TestContext.Current.CancellationToken);
        vm.SelectedModel = vm.Models.First();

        // Wait brief delay for async detail load
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Initial Description", vm.DescriptionDraft);
        Assert.Equal("Initial Print Notes", vm.PrintNotesDraft);

        // Edit drafts
        vm.DescriptionDraft = "Updated castle lore.";
        vm.PrintNotesDraft = "Print at 0.12mm layer height.";
        vm.SaveDescriptionCommand.Execute(null);

        // Wait brief delay for save
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Updated castle lore.", detailRepo.Details[modelId].Description);
        Assert.Equal("Print at 0.12mm layer height.", detailRepo.Details[modelId].PrintNotes);
    }

    [Fact]
    public void DashboardViewModel_LibraryDrawer_TogglesState()
    {
        var import = new FakeImport();
        var detailRepo = new FakeDetailRepo();
        var catalog = new FakeCatalog(detailRepo);
        var vm = new DashboardViewModel(import, catalog, rootPath: @"C:\vault\MyCollection");

        Assert.False(vm.IsLibraryDrawerOpen);
        Assert.Equal("MyCollection", vm.LibraryRootDisplay);

        vm.ToggleLibraryDrawerCommand.Execute(null);
        Assert.True(vm.IsLibraryDrawerOpen);

        vm.ToggleLibraryDrawerCommand.Execute(null);
        Assert.False(vm.IsLibraryDrawerOpen);
    }

    [Fact]
    public void ShellViewModel_ShowFavorites_NavigatesToFavoritesOnly()
    {
        var import = new FakeImport();
        var detailRepo = new FakeDetailRepo();
        var catalog = new FakeCatalog(detailRepo);
        var dashboard = new DashboardViewModel(import, catalog);
        var shell = new ShellViewModel(dashboard);

        shell.ShowFavoritesCommand.Execute(null);
        Assert.True(dashboard.OnlyFavoritesFilter);
        Assert.Same(dashboard, shell.CurrentViewModel);

        shell.ShowDashboardCommand.Execute(null);
        Assert.False(dashboard.OnlyFavoritesFilter);
        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    private sealed class FakeImport : IModelImportService
    {
        public Task<ImportLibraryResult> ImportAsync(ImportLibraryRequest request, CancellationToken cancellationToken = default, IProgress<ImportProgress>? progress = null) =>
            Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
    }

    private sealed class FakeCatalog(FakeDetailRepo detailRepo) : DPV.Domain.Contracts.Catalog.IModelCatalogQuery
    {
        public List<ModelSummaryDto> AllModels { get; } = [];

        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var result = AllModels.Select(m => m with { IsFavorite = detailRepo.Favorites.Contains(m.Id) });
            if (parameters.OnlyFavorites)
            {
                result = result.Where(m => m.IsFavorite);
            }
            return Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>(result.ToList());
        }
    }

    private sealed class FakeDetailRepo : IModelDetailRepository
    {
        public HashSet<Guid> Favorites { get; } = [];
        public Dictionary<Guid, ModelDetailDto> Details { get; } = [];

        public Task<ModelDetailDto?> GetModelDetailAsync(Guid modelId, CancellationToken cancellationToken = default)
        {
            Details.TryGetValue(modelId, out var detail);
            return Task.FromResult(detail);
        }

        public Task UpdateModelMetadataAsync(Guid modelId, string? artistName, string? collectionName, IEnumerable<string> tags, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ToggleFavoriteAsync(Guid modelId, CancellationToken cancellationToken = default)
        {
            if (Favorites.Contains(modelId))
            {
                Favorites.Remove(modelId);
            }
            else
            {
                Favorites.Add(modelId);
            }
            return Task.CompletedTask;
        }

        public Task UpdateModelDescriptionAsync(Guid modelId, string? description, string? printNotes, CancellationToken cancellationToken = default)
        {
            if (Details.TryGetValue(modelId, out var detail))
            {
                Details[modelId] = detail with { Description = description, PrintNotes = printNotes };
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGrouping(FakeDetailRepo detailRepo) : IModelGroupingQuery
    {
        public Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtistGroupDto>>([]);

        public Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionGroupDto>>([]);

        public Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TagGroupDto>>([]);

        public Task<long> GetFavoritesCountAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<long>(detailRepo.Favorites.Count);

        public Task ToggleArtistFavoriteAsync(string artistName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
