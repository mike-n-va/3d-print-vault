using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;
using DPV.UI.ViewModels;
using Xunit;

namespace DPV.Tests.Unit.UI;

public class ModelAndReleaseCardViewModelTests
{
    [Fact]
    public void ModelCardViewModel_InitializesFromSummaryDto_AndInfersPreSupported()
    {
        var id = Guid.NewGuid();
        var summary = new ModelSummaryDto(
            Id: id,
            Name: "Basilisk of the Shattered King [presupported]",
            FilePath: @"C:\Vault\Basilisk.stl",
            FileSizeBytes: 1024,
            LastModified: "2026-11-06",
            IsActive: true,
            ThumbnailPath: @"C:\Cache\Thumbnails\basilisk.png",
            ArtistName: "NerdGave",
            CollectionName: "March 2026",
            AssetCount: 4,
            IsFavorite: true);

        var card = new ModelCardViewModel(summary);

        Assert.Equal(id, card.Id);
        Assert.Equal("Basilisk of the Shattered King [presupported]", card.Name);
        Assert.Equal("NerdGave", card.ArtistName);
        Assert.Equal("March 2026", card.CollectionName);
        Assert.Equal(@"C:\Cache\Thumbnails\basilisk.png", card.ThumbnailPath);
        Assert.True(card.IsPreSupported);
        Assert.True(card.IsFavorite);
        Assert.Equal(4, card.AssetCount);
        Assert.Equal(@"C:\Cache\Thumbnails\basilisk.png", card.ActiveImagePath);
    }

    [Fact]
    public void ModelCardViewModel_SwitchesActiveImagePath_WhenHovered()
    {
        var card = new ModelCardViewModel(
            id: Guid.NewGuid(),
            name: "Dragon Miniature",
            thumbnailPath: "thumb_230.png",
            hoverPreviewPath: "beauty_render_720.png");

        Assert.Equal("thumb_230.png", card.ActiveImagePath);

        card.IsHovered = true;
        Assert.Equal("beauty_render_720.png", card.ActiveImagePath);

        card.IsHovered = false;
        Assert.Equal("thumb_230.png", card.ActiveImagePath);
    }

    [Fact]
    public async Task ModelCardViewModel_ToggleFavoriteCommand_InvokesCallback()
    {
        var toggled = false;
        var card = new ModelCardViewModel(
            id: Guid.NewGuid(),
            name: "Warrior",
            isFavorite: false,
            onToggleFavorite: c =>
            {
                toggled = true;
                c.IsFavorite = !c.IsFavorite;
                return Task.CompletedTask;
            });

        card.ToggleFavoriteCommand.Execute(null);

        Assert.True(toggled);
        Assert.True(card.IsFavorite);
    }

    [Fact]
    public void ReleaseCardViewModel_InitializesFromSummaryDto_AndExecutesCommands()
    {
        var relId = Guid.NewGuid();
        var release = new ReleaseSummaryDto(
            ReleaseId: relId,
            Name: "The Crimson Citadel",
            ArtistName: "Artisan Guild",
            Description: "A dark high-fantasy paladin campaign set.",
            ModelCount: 28,
            CoverImagePath: "cover_16_9.png",
            IsFavorite: false,
            ReleaseDate: "2026-03");

        var selected = false;
        var card = new ReleaseCardViewModel(
            release,
            onSelect: r => selected = true);

        Assert.Equal(relId, card.ReleaseId);
        Assert.Equal("The Crimson Citadel", card.Name);
        Assert.Equal("Artisan Guild", card.ArtistName);
        Assert.Equal(28, card.ModelCount);
        Assert.Equal("cover_16_9.png", card.BannerPath);
        Assert.False(card.IsFavorite);

        card.SelectCommand.Execute(null);
        Assert.True(selected);

        card.ToggleFavoriteCommand.Execute(null);
        Assert.True(card.IsFavorite);
    }

    [Fact]
    public async Task MainDashboardViewModel_PopulatesCatalogModels_OnSearch()
    {
        var models = new List<ModelSummaryDto>
        {
            new(Guid.NewGuid(), "Paladin [presupported]", @"C:\v\paladin.stl", 100, "", true, "", "paladin.png", "Artisan", "Citadel", 3),
            new(Guid.NewGuid(), "Sorceress", @"C:\v\sorc.stl", 200, "", true, "", "sorc.png", "Artisan", "Citadel", 1)
        };

        var query = new FakeCatalogQuery(models);
        var vm = new MainDashboardViewModel(
            new FakeImportService(),
            query);

        await vm.ExecuteSearchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.CatalogModels.Count);
        Assert.Equal("Paladin [presupported]", vm.CatalogModels[0].Name);
        Assert.True(vm.CatalogModels[0].IsPreSupported);
        Assert.Equal("Sorceress", vm.CatalogModels[1].Name);
        Assert.False(vm.CatalogModels[1].IsPreSupported);
    }

    private sealed class FakeCatalogQuery : CatalogQuery
    {
        private readonly IReadOnlyCollection<ModelSummaryDto> _models;

        public FakeCatalogQuery(IReadOnlyCollection<ModelSummaryDto> models) => _models = models;

        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(
            ModelQueryParameters parameters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_models);
    }

    private sealed class FakeImportService : IModelImportService
    {
        public Task<ImportLibraryResult> ImportAsync(
            ImportLibraryRequest request,
            CancellationToken cancellationToken = default,
            IProgress<ImportProgress>? progress = null) =>
            Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
    }
}
