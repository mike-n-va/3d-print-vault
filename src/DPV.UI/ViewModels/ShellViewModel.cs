using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DuplicatesViewModel _duplicatesViewModel;
    private readonly AnalyticsViewModel _analyticsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ArtistBrowserViewModel _artistBrowserViewModel;
    private readonly CollectionBrowserViewModel _collectionBrowserViewModel;
    private readonly TagBrowserViewModel _tagBrowserViewModel;
    private readonly ReleaseBrowserViewModel _releaseBrowserViewModel;
    private readonly ReleaseDetailViewModel _releaseDetailViewModel;
    private readonly FavoritesViewModel _favoritesViewModel;

    private object _currentViewModel;
    private string _activeScope = "Models";
    private object? _previousViewModel;

    public ShellViewModel(
        DashboardViewModel dashboardViewModel,
        DuplicatesViewModel? duplicatesViewModel = null,
        AnalyticsViewModel? analyticsViewModel = null,
        SettingsViewModel? settingsViewModel = null,
        ArtistBrowserViewModel? artistBrowserViewModel = null,
        CollectionBrowserViewModel? collectionBrowserViewModel = null,
        TagBrowserViewModel? tagBrowserViewModel = null,
        IModelGroupingQuery? groupingQuery = null,
        IReleaseCatalogQuery? releaseCatalogQuery = null,
        IReleaseDetailRepository? releaseDetailRepository = null,
        ReleaseBrowserViewModel? releaseBrowserViewModel = null,
        ReleaseDetailViewModel? releaseDetailViewModel = null,
        FavoritesViewModel? favoritesViewModel = null)
    {
        _dashboardViewModel = dashboardViewModel;
        _duplicatesViewModel = duplicatesViewModel ?? new DuplicatesViewModel(new EmptyDuplicateRepository());
        _analyticsViewModel = analyticsViewModel ?? new AnalyticsViewModel(new EmptyStatsRepository());
        _settingsViewModel = settingsViewModel ?? new SettingsViewModel(new EmptyExportService(), new EmptyMaintenanceService());

        var query = groupingQuery ?? new EmptyGroupingQuery();
        var relQuery = releaseCatalogQuery ?? new EmptyReleaseCatalogQuery();
        var relRepo = releaseDetailRepository ?? new EmptyReleaseDetailRepository();

        _releaseDetailViewModel = releaseDetailViewModel ?? new ReleaseDetailViewModel(
            relRepo,
            onBack: () =>
            {
                CurrentViewModel = _previousViewModel ?? (object)_dashboardViewModel;
            },
            modelDetailRepository: null,
            onFilterByArtist: artist =>
            {
                _dashboardViewModel.FilterByArtist(artist);
                ActiveScope = "Models";
                CurrentViewModel = _dashboardViewModel;
            },
            onSelectModel: model =>
            {
                _dashboardViewModel.FilterBySearch(model.Name);
                ActiveScope = "Models";
                CurrentViewModel = _dashboardViewModel;
            });

        _releaseBrowserViewModel = releaseBrowserViewModel ?? new ReleaseBrowserViewModel(
            relQuery,
            relRepo,
            onSelectRelease: release =>
            {
                _previousViewModel = _currentViewModel;
                CurrentViewModel = _releaseDetailViewModel;
                _ = _releaseDetailViewModel.LoadReleaseAsync(release.ReleaseId);
            });

        _artistBrowserViewModel = artistBrowserViewModel ?? new ArtistBrowserViewModel(
            query,
            onSelectArtist: artist =>
            {
                _dashboardViewModel.FilterByArtist(artist);
                ActiveScope = "Models";
                CurrentViewModel = _dashboardViewModel;
            },
            releaseCatalogQuery: relQuery,
            onSelectRelease: release =>
            {
                _previousViewModel = _currentViewModel;
                CurrentViewModel = _releaseDetailViewModel;
                _ = _releaseDetailViewModel.LoadReleaseAsync(release.ReleaseId);
            });

        _collectionBrowserViewModel = collectionBrowserViewModel ?? new CollectionBrowserViewModel(query, collection =>
        {
            _dashboardViewModel.FilterByCollection(collection);
            ActiveScope = "Models";
            CurrentViewModel = _dashboardViewModel;
        });

        _tagBrowserViewModel = tagBrowserViewModel ?? new TagBrowserViewModel(query, tag =>
        {
            _dashboardViewModel.FilterByTag(tag);
            ActiveScope = "Models";
            CurrentViewModel = _dashboardViewModel;
        });

        _favoritesViewModel = favoritesViewModel ?? new FavoritesViewModel(
            new EmptyModelCatalogQuery(),
            new EmptyModelDetailRepository(),
            relQuery,
            relRepo,
            query,
            onSelectModel: model =>
            {
                _dashboardViewModel.FilterBySearch(model.Name);
                ActiveScope = "Models";
                CurrentViewModel = _dashboardViewModel;
            },
            onSelectRelease: release =>
            {
                _previousViewModel = _currentViewModel;
                CurrentViewModel = _releaseDetailViewModel;
                _ = _releaseDetailViewModel.LoadReleaseAsync(release.ReleaseId);
            },
            onSelectArtist: artist =>
            {
                _dashboardViewModel.FilterByArtist(artist);
                ActiveScope = "Models";
                CurrentViewModel = _dashboardViewModel;
            });

        _currentViewModel = dashboardViewModel;
        _dashboardViewModel.ShowDuplicates = ShowDuplicates;

        ShowDashboardCommand = new DelegateCommand(ShowDashboard);
        ShowModelsCommand = new DelegateCommand(ShowDashboard);
        ShowReleasesCommand = new DelegateCommand(ShowReleases);
        ShowFavoritesCommand = new DelegateCommand(ShowFavorites);
        ShowDuplicatesCommand = new DelegateCommand(ShowDuplicates);
        ShowAnalyticsCommand = new DelegateCommand(ShowAnalytics);
        ShowSettingsCommand = new DelegateCommand(() => CurrentViewModel = _settingsViewModel);
        ShowArtistsCommand = new DelegateCommand(ShowArtists);
        ShowCollectionsCommand = new DelegateCommand(ShowCollections);
        ShowTagsCommand = new DelegateCommand(ShowTags);
        SelectScopeCommand = new RelayCommand<string>(SelectScope);

        ScheduleInitialLoad(_dashboardViewModel);
    }

    public DashboardViewModel Dashboard => _dashboardViewModel;

    public ReleaseBrowserViewModel ReleaseBrowser => _releaseBrowserViewModel;

    public ReleaseDetailViewModel ReleaseDetail => _releaseDetailViewModel;

    public ArtistBrowserViewModel ArtistBrowser => _artistBrowserViewModel;

    public FavoritesViewModel Favorites => _favoritesViewModel;

    public string ActiveScope
    {
        get => _activeScope;
        set => SetField(ref _activeScope, value);
    }

    public void SelectScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return;

        switch (scope.Trim())
        {
            case "Models":
                ShowDashboard();
                break;
            case "Releases":
                ShowReleases();
                break;
            case "Artists":
                ShowArtists();
                break;
            case "Favorites":
                ShowFavorites();
                break;
        }
    }

    public void ShowDashboard()
    {
        ActiveScope = "Models";
        _dashboardViewModel.OnlyFavoritesFilter = false;
        CurrentViewModel = _dashboardViewModel;
    }

    public void ShowReleases()
    {
        ActiveScope = "Releases";
        _releaseBrowserViewModel.ScanRoot = _dashboardViewModel.RootPath;
        CurrentViewModel = _releaseBrowserViewModel;
        _ = _releaseBrowserViewModel.LoadAsync();
    }

    public void ShowFavorites()
    {
        ActiveScope = "Favorites";
        _dashboardViewModel.OnlyFavoritesFilter = true;
        CurrentViewModel = _dashboardViewModel;
        _ = _dashboardViewModel.ExecuteSearchAsync();
    }

    public void ShowCuratedFavorites()
    {
        ActiveScope = "Favorites";
        _favoritesViewModel.ScanRoot = _dashboardViewModel.RootPath;
        CurrentViewModel = _favoritesViewModel;
        _ = _favoritesViewModel.LoadAsync();
    }

    public void ShowDuplicates()
    {
        CurrentViewModel = _duplicatesViewModel;
        _ = _duplicatesViewModel.LoadAsync();
    }

    public void ShowAnalytics()
    {
        CurrentViewModel = _analyticsViewModel;
        _ = _analyticsViewModel.LoadAsync();
    }

    public void ShowArtists()
    {
        ActiveScope = "Artists";
        _artistBrowserViewModel.ScanRoot = _dashboardViewModel.RootPath;
        CurrentViewModel = _artistBrowserViewModel;
        _ = _artistBrowserViewModel.LoadAsync();
    }

    public void ShowCollections()
    {
        _collectionBrowserViewModel.ScanRoot = _dashboardViewModel.RootPath;
        CurrentViewModel = _collectionBrowserViewModel;
        _ = _collectionBrowserViewModel.LoadAsync();
    }

    public void ShowTags()
    {
        _tagBrowserViewModel.ScanRoot = _dashboardViewModel.RootPath;
        CurrentViewModel = _tagBrowserViewModel;
        _ = _tagBrowserViewModel.LoadAsync();
    }

    private static void ScheduleInitialLoad(DashboardViewModel dashboard)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await dashboard.LoadCatalogAsync().ConfigureAwait(true);
            }
            catch
            {
                // Initial catalog query failures are reported via dashboard status
            }
        }, DispatcherPriority.Loaded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (ReferenceEquals(_currentViewModel, value))
            {
                return;
            }

            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowModelsCommand { get; }

    public ICommand ShowReleasesCommand { get; }

    public ICommand ShowFavoritesCommand { get; }

    public ICommand ShowDuplicatesCommand { get; }

    public ICommand ShowAnalyticsCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand ShowArtistsCommand { get; }

    public ICommand ShowCollectionsCommand { get; }

    public ICommand ShowTagsCommand { get; }

    public ICommand SelectScopeCommand { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;

        public DelegateCommand(Action execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }

    private sealed class EmptyGroupingQuery : IModelGroupingQuery
    {
        public Task<IReadOnlyList<ArtistGroupDto>> GetArtistsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtistGroupDto>>([]);

        public Task<IReadOnlyList<CollectionGroupDto>> GetCollectionsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionGroupDto>>([]);

        public Task<IReadOnlyList<TagGroupDto>> GetTagsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TagGroupDto>>([]);

        public Task<long> GetFavoritesCountAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task ToggleArtistFavoriteAsync(string artistName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyReleaseCatalogQuery : IReleaseCatalogQuery
    {
        public Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesAsync(ReleaseQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReleaseSummaryDto>>([]);

        public Task<IReadOnlyCollection<ReleaseSummaryDto>> GetReleasesByArtistAsync(Guid artistId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReleaseSummaryDto>>([]);

        public Task<ReleaseStatisticsDto> GetReleaseStatisticsAsync(string? scanRoot = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReleaseStatisticsDto(0, 0, 0));
    }

    private sealed class EmptyReleaseDetailRepository : IReleaseDetailRepository
    {
        public Task<ReleaseDetailDto?> GetReleaseDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReleaseDetailDto?>(null);

        public Task ToggleReleaseFavoriteAsync(Guid releaseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateReleaseCoverAsync(Guid releaseId, string coverPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateReleaseDescriptionAsync(Guid releaseId, string? description, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyModelCatalogQuery : IModelCatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>([]);
    }

    private sealed class EmptyModelDetailRepository : IModelDetailRepository
    {
        public Task<ModelDetailDto?> GetModelDetailAsync(Guid modelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelDetailDto?>(null);

        public Task UpdateModelMetadataAsync(Guid modelId, string? artistName, string? collectionName, IEnumerable<string> tags, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ToggleFavoriteAsync(Guid modelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateModelDescriptionAsync(Guid modelId, string? description, string? printNotes, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyDuplicateRepository : IDuplicateRepository
    {
        public Task<IReadOnlyCollection<DuplicateGroupDto>> GetDuplicateGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DuplicateGroupDto>>([]);

        public Task ResolveDuplicateMergeAsync(
            Guid primaryFileId,
            IEnumerable<Guid> duplicateFileIdsToDeactivate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateIgnoreAsync(string contentHash, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateDeleteAsync(Guid fileIdToDelete, string filePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyStatsRepository : ILibraryStatsRepository
    {
        public Task<LibraryStatsDto> GetLibraryStatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryStatsDto(0, 0, 0, 0, 0, 0, [], [], []));
    }

    private sealed class EmptyExportService : ICatalogExportService
    {
        public Task<string> ExportToCsvAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("ModelName,FilePath,FileSizeBytes,Artist,Collection,LastModified" + Environment.NewLine);

        public Task<string> ExportToJsonAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("[]");
    }

    private sealed class EmptyMaintenanceService : IDbMaintenanceService
    {
        public Task VacuumDatabaseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BackupDatabaseAsync(string destinationFilePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RestoreDatabaseAsync(string sourceFilePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
