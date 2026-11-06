using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.UI.ViewModels;
using Xunit;

namespace DPV.Tests.Unit.UI;

public class ReleaseAwareSearchTests
{
    [Fact]
    public async Task DashboardSearch_PrioritizesExactReleaseMatchesFirst_ThenExactModelMatches()
    {
        // Setup:
        // Model 1: Exact Release match: CollectionName = "Crimson Paladins", ModelName = "Paladin Commander"
        // Model 2: Exact Model match: ModelName = "Crimson Paladins", CollectionName = "Other"
        // Model 3: Related match: ModelName = "Crimson Knight", CollectionName = "Paladins"
        var m1 = new ModelSummaryDto(Guid.NewGuid(), "Paladin Commander", "p1.stl", 100, "now", true, CollectionName: "Crimson Paladins");
        var m2 = new ModelSummaryDto(Guid.NewGuid(), "Crimson Paladins", "p2.stl", 100, "now", true, CollectionName: "Other");
        var m3 = new ModelSummaryDto(Guid.NewGuid(), "Crimson Knight", "p3.stl", 100, "now", true, CollectionName: "Paladins");

        var catalog = new SearchRecordingCatalog([m3, m2, m1]); // Unordered inputs
        var dashboard = new DashboardViewModel(new FakeImport(), catalog);

        dashboard.SearchQuery = "Crimson Paladins";
        await dashboard.ExecuteSearchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, dashboard.Models.Count);
        // Priority 1: Exact Release match (m1)
        Assert.Equal("Paladin Commander", dashboard.Models[0].Name);
        Assert.Equal("Crimson Paladins", dashboard.Models[0].CollectionName);

        // Priority 2: Exact Model match (m2)
        Assert.Equal("Crimson Paladins", dashboard.Models[1].Name);

        // Priority 3: Related match (m3)
        Assert.Equal("Crimson Knight", dashboard.Models[2].Name);
    }

    [Fact]
    public void ShellViewModel_ScopeSwitcher_SwitchesPrimaryPerspectives()
    {
        var dashboard = new DashboardViewModel(new FakeImport(), new SearchRecordingCatalog([]));
        var shell = new ShellViewModel(dashboard);

        // Initial scope
        Assert.Equal("Models", shell.ActiveScope);
        Assert.Same(dashboard, shell.CurrentViewModel);

        // Switch to Releases
        shell.ShowReleasesCommand.Execute(null);
        Assert.Equal("Releases", shell.ActiveScope);
        Assert.IsType<ReleaseBrowserViewModel>(shell.CurrentViewModel);

        // Switch to Artists
        shell.ShowArtistsCommand.Execute(null);
        Assert.Equal("Artists", shell.ActiveScope);
        Assert.IsType<ArtistBrowserViewModel>(shell.CurrentViewModel);

        // Switch to Favorites
        shell.ShowFavoritesCommand.Execute(null);
        Assert.Equal("Favorites", shell.ActiveScope);
        Assert.True(dashboard.OnlyFavoritesFilter);

        // SelectScopeCommand
        shell.SelectScopeCommand.Execute("Models");
        Assert.Equal("Models", shell.ActiveScope);
        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    private sealed class SearchRecordingCatalog(IEnumerable<ModelSummaryDto> models) : CatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>(models.ToList());
    }

    private sealed class FakeImport : IModelImportService
    {
        public Task<ImportLibraryResult> ImportAsync(
            ImportLibraryRequest request,
            CancellationToken cancellationToken = default,
            IProgress<ImportProgress>? progress = null) =>
            Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
    }
}
