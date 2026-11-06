using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;
using DPV.UI.ViewModels;

namespace DPV.Tests.Unit.UI;

public class DashboardNavigationTests
{
    [Fact]
    public void DashboardIsTheInitialShellViewModel()
    {
        var dashboard = CreateDashboard();
        var shell = new ShellViewModel(dashboard);

        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    [Fact]
    public void DashboardNavigationCommandCanSelectStartupRoute()
    {
        var dashboard = CreateDashboard();
        var shell = new ShellViewModel(dashboard);

        Assert.True(shell.ShowDashboardCommand.CanExecute(null));
        shell.ShowDashboardCommand.Execute(null);

        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    [Fact]
    public void DuplicatesCommandOpensTheDuplicateWorkspace()
    {
        var dashboard = CreateDashboard();
        var shell = new ShellViewModel(dashboard);

        dashboard.ShowDuplicatesCommand.Execute(null);

        Assert.IsType<DuplicatesViewModel>(shell.CurrentViewModel);
        shell.ShowAnalyticsCommand.Execute(null);
        Assert.IsType<AnalyticsViewModel>(shell.CurrentViewModel);
        shell.ShowSettingsCommand.Execute(null);
        Assert.IsType<SettingsViewModel>(shell.CurrentViewModel);
        shell.ShowDashboardCommand.Execute(null);
        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    [Fact]
    public void DashboardViewModelProvidesOnlyPlaceholderContent()
    {
        var viewModel = CreateDashboard();

        Assert.Equal("Welcome to 3D Print Vault", viewModel.Title);
        Assert.Equal("Your library overview will appear here.", viewModel.Description);
        Assert.False(viewModel.ImportCommand.CanExecute(null));
        Assert.False(viewModel.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task ImportCommandLoadsModelsFromTheCatalogAndReportsProgress()
    {
        var import = new RecordingImportService();
        var catalog = new StaticCatalogQuery();
        var viewModel = new DashboardViewModel(import, catalog, maxFileSizeBytes: 4096, batchLimit: 25);
        viewModel.RootPath = @"C:\library";

        Assert.True(viewModel.ImportCommand.CanExecute(null));
        await viewModel.ImportAsync();

        Assert.False(viewModel.IsScanning);
        Assert.Equal(100, viewModel.ProgressPercentage);
        Assert.Equal(4096, import.Request!.MaxFileSizeBytes);
        Assert.Equal(25, import.Request.BatchLimit);
        Assert.Equal(@"C:\library", import.Request.RootPath);
        var model = Assert.Single(viewModel.Models);
        Assert.Equal("bracket", model.Name);
        Assert.EndsWith("bracket.stl", model.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchQueryDebouncesBeforeQuerying()
    {
        var catalog = new RecordingCatalogQuery();
        var viewModel = new DashboardViewModel(new RecordingImportService(), catalog, rootPath: @"C:\library");

        viewModel.SearchQuery = "bra";
        viewModel.SearchQuery = "bracket";

        Assert.Equal(0, catalog.CallCount);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Equal(1, catalog.CallCount);
        Assert.Equal("bracket", catalog.LastParameters!.SearchTerm);
        Assert.Equal(@"C:\library", catalog.LastParameters.ScanRoot);
    }

    [Fact]
    public async Task CancelCommandCancelsTheActiveImport()
    {
        var import = new RecordingImportService { WaitForCancellation = true };
        var viewModel = new DashboardViewModel(import, new StaticCatalogQuery());
        viewModel.RootPath = @"C:\library";

        var importTask = viewModel.ImportAsync();
        await import.Started;
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await importTask;

        Assert.False(viewModel.IsScanning);
        Assert.True(import.ObservedToken.IsCancellationRequested);
        Assert.Contains("cancelled", viewModel.CurrentStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellViewModelInvokesDashboardLoadCatalogOnStartup()
    {
        var catalog = new RecordingCatalogQuery();
        var dashboard = new DashboardViewModel(new RecordingImportService(), catalog);
        _ = new ShellViewModel(dashboard);

        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        if (!dispatcher.CheckAccess())
        {
            typeof(Avalonia.Threading.Dispatcher)
                .GetField("_thread", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                .SetValue(dispatcher, System.Threading.Thread.CurrentThread);
        }

        dispatcher.RunJobs(Avalonia.Threading.DispatcherPriority.Loaded);
        Assert.Equal(1, catalog.CallCount);
    }

    [Fact]
    public void DuplicatesViewModelSelectsFileAndRaisesSelectedFilesFirst()
    {
        var fileId = Guid.NewGuid();
        var file = new DuplicateFileItem(fileId, fileId, @"C:\models\cube.stl", 100, DateTime.UtcNow, true, "cube");
        var group = new DuplicateGroupDto("hash123", 100, 1, [file]);
        var events = new List<string>();

        var duplicates = new DuplicatesViewModel(new FakeDuplicateRepository());
        duplicates.PropertyChanged += (_, e) => events.Add(e.PropertyName!);

        duplicates.SelectedGroup = group;

        Assert.Equal(file, duplicates.SelectedFile);
        var filesIndex = events.IndexOf(nameof(DuplicatesViewModel.SelectedFiles));
        var fileIndex = events.IndexOf(nameof(DuplicatesViewModel.SelectedFile));
        Assert.True(filesIndex >= 0);
        Assert.True(fileIndex >= 0);
        Assert.True(filesIndex < fileIndex, "SelectedFiles notification must precede SelectedFile to prevent ListBox selection coercion.");
    }

    [Fact]
    public void DuplicatesViewModelGuardsCommandsDuringBusyState()
    {
        var fileId = Guid.NewGuid();
        var file = new DuplicateFileItem(fileId, fileId, @"C:\models\cube.stl", 100, DateTime.UtcNow, true, "cube");
        var group = new DuplicateGroupDto("hash123", 100, 1, [file]);
        var duplicates = new DuplicatesViewModel(new FakeDuplicateRepository());

        duplicates.SelectedGroup = group;
        Assert.True(duplicates.DeleteCommand.CanExecute(null));
        Assert.True(duplicates.MergeCommand.CanExecute(null));
        Assert.True(duplicates.IgnoreCommand.CanExecute(null));
        Assert.True(duplicates.RefreshCommand.CanExecute(null));
    }

    private sealed class FakeDuplicateRepository : IDuplicateRepository
    {
        public Task<IReadOnlyCollection<DuplicateGroupDto>> GetDuplicateGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DuplicateGroupDto>>([]);

        public Task ResolveDuplicateMergeAsync(Guid primaryFileId, IEnumerable<Guid> duplicateFileIdsToDeactivate, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateIgnoreAsync(string contentHash, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateDeleteAsync(Guid fileIdToDelete, string filePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task ClearSearchCommandResetsQueryAndRestoresCatalog()
    {
        var catalog = new RecordingCatalogQuery();
        var dashboard = new DashboardViewModel(new RecordingImportService(), catalog, rootPath: @"C:\library");
        dashboard.SearchQuery = "test-query";
        dashboard.TagFilter = "custom-tag";

        Assert.True(dashboard.IsFiltered);
        Assert.True(dashboard.ClearSearchCommand.CanExecute(null));

        dashboard.ClearSearchCommand.Execute(null);

        Assert.Empty(dashboard.SearchQuery);
        Assert.Equal("All", dashboard.TagFilter);
        Assert.False(dashboard.IsFiltered);
        Assert.Equal(1, catalog.CallCount);
    }

    [Fact]
    public void EmptyStateFlagsDistinguishEmptyCatalogFromEmptySearch()
    {
        var dashboard = CreateDashboard();

        Assert.Empty(dashboard.Models);
        Assert.False(dashboard.IsFiltered);
        Assert.True(dashboard.IsEmptyCatalog);
        Assert.False(dashboard.IsEmptySearch);

        dashboard.SearchQuery = "test";

        Assert.True(dashboard.IsFiltered);
        Assert.False(dashboard.IsEmptyCatalog);
        Assert.True(dashboard.IsEmptySearch);
    }

    private static DashboardViewModel CreateDashboard() =>
        new(new RecordingImportService(), new StaticCatalogQuery());

    private sealed class RecordingImportService : IModelImportService
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WaitForCancellation { get; init; }

        public ImportLibraryRequest? Request { get; private set; }

        public CancellationToken ObservedToken { get; private set; }

        public Task Started => _started.Task;

        public async Task<ImportLibraryResult> ImportAsync(
            ImportLibraryRequest request,
            CancellationToken cancellationToken = default,
            IProgress<ImportProgress>? progress = null)
        {
            Request = request;
            ObservedToken = cancellationToken;
            _started.TrySetResult();
            progress?.Report(new ImportProgress(1, 2, "Scanning"));
            if (WaitForCancellation)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new ImportLibraryResult(0, 0, 0, 0, 0, false, []);
                }
            }

            progress?.Report(new ImportProgress(2, 2, "Complete"));
            return new ImportLibraryResult(0, 1, 0, 0, 0, true, []);
        }
    }

    private sealed class StaticCatalogQuery : CatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(
            ModelQueryParameters parameters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>(
            [
                new ModelSummaryDto(Guid.NewGuid(), "bracket", parameters.ScanRoot + @"\bracket.stl", 84, "stamp", true)
            ]);
    }

    private sealed class RecordingCatalogQuery : CatalogQuery
    {
        public int CallCount { get; private set; }

        public ModelQueryParameters? LastParameters { get; private set; }

        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(
            ModelQueryParameters parameters,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastParameters = parameters;
            return Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>([]);
        }
    }
}
