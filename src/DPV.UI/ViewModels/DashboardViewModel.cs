using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;

namespace DPV.UI.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IModelImportService _importService;
    private readonly CatalogQuery _catalogQuery;
    private readonly IModelDetailRepository _detailRepository;
    private readonly IThumbnailService? _thumbnails;
    private readonly IUserPreferencesService? _preferencesService;
    private readonly IModelGroupingQuery? _groupingQuery;
    private readonly IReleaseCatalogQuery? _releaseCatalogQuery;
    private readonly IReleaseDetailRepository? _releaseDetailRepository;
    private CancellationTokenSource? _thumbnailCancellation;
    private string? _previewPath;
    private const int SearchDebounceMilliseconds = 250;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _searchDebounce;
    private bool _isScanning;
    private double _progressPercentage;
    private string _currentStatusText = "Ready";
    private string _rootPath;
    private string _searchQuery = string.Empty;
    private string _tagFilter = "All";
    private string? _artistFilter;
    private string? _collectionFilter;
    private bool _onlyFavoritesFilter;
    private long _favoritesCount;
    private bool _isLibraryDrawerOpen;
    private string _viewMode = "Grid";
    private string _gridDensity = "Medium";
    private ModelSummaryDto? _selectedModel;
    private ModelDetailDto? _detailedModel;
    private string _artistDraft = string.Empty;
    private string _collectionDraft = string.Empty;
    private string _tagsDraft = string.Empty;
    private string _descriptionDraft = string.Empty;
    private string _printNotesDraft = string.Empty;

    public DashboardViewModel(
        IModelImportService importService,
        CatalogQuery catalogQuery,
        long maxFileSizeBytes = 2L * 1024 * 1024 * 1024,
        int batchLimit = 500,
        string? rootPath = null,
        IModelDetailRepository? detailRepository = null,
        IThumbnailService? thumbnails = null,
        IUserPreferencesService? preferencesService = null,
        IModelGroupingQuery? groupingQuery = null,
        IReleaseCatalogQuery? releaseCatalogQuery = null,
        IReleaseDetailRepository? releaseDetailRepository = null)
    {
        _importService = importService;
        _catalogQuery = catalogQuery;
        _detailRepository = detailRepository ?? new EmptyDetailRepository();
        _thumbnails = thumbnails;
        _preferencesService = preferencesService;
        _groupingQuery = groupingQuery;
        _releaseCatalogQuery = releaseCatalogQuery;
        _releaseDetailRepository = releaseDetailRepository;
        MaxFileSizeBytes = maxFileSizeBytes;
        BatchLimit = batchLimit;
        _rootPath = rootPath ?? string.Empty;

        if (_preferencesService is not null)
        {
            var prefs = _preferencesService.LoadPreferences();
            if (string.IsNullOrWhiteSpace(_rootPath) && !string.IsNullOrWhiteSpace(prefs.LastScanRoot))
            {
                _rootPath = prefs.LastScanRoot;
            }

            _viewMode = prefs.ViewMode ?? "Grid";
            _gridDensity = prefs.GridDensity ?? "Medium";
        }

        Models = new ObservableCollection<ModelSummaryDto>();
        TagFilterChoices = new ObservableCollection<string> { "All" };
        ImportCommand = new RelayCommand(() => _ = ImportAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(RootPath));
        CancelCommand = new RelayCommand(CancelScan, () => IsScanning);
        ExecuteSearchCommand = new RelayCommand(() => _ = ExecuteSearchAsync());
        ClearSearchCommand = new RelayCommand(ClearSearch);
        SaveMetadataCommand = new RelayCommand(() => _ = SaveMetadataAsync(), () => SelectedModel is not null && !IsScanning);
        SaveDescriptionCommand = new RelayCommand(() => _ = SaveDescriptionAsync(), () => SelectedModel is not null && !IsScanning);
        ToggleFavoriteCommand = new RelayCommand<object>(param => _ = ToggleFavoriteAsync(param));
        ToggleOnlyFavoritesCommand = new RelayCommand(ToggleOnlyFavorites);
        ToggleLibraryDrawerCommand = new RelayCommand(() => IsLibraryDrawerOpen = !IsLibraryDrawerOpen);
        ShowDuplicatesCommand = new RelayCommand(() => ShowDuplicates?.Invoke());

        FilterByArtistCommand = new RelayCommand<string>(FilterByArtist);
        FilterByCollectionCommand = new RelayCommand<string>(FilterByCollection);
        FilterByTagCommand = new RelayCommand<string>(FilterByTag);
        SetViewModeCommand = new RelayCommand<string>(mode => ViewMode = mode ?? "Grid");
        SetGridDensityCommand = new RelayCommand<string>(density => GridDensity = density ?? "Medium");
    }

    public Action? ShowDuplicates { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Welcome to 3D Print Vault";

    public string Description => "Your library overview will appear here.";

    public long MaxFileSizeBytes { get; }

    public int BatchLimit { get; }

    public ObservableCollection<ModelSummaryDto> Models { get; }

    public ObservableCollection<ModelCardViewModel> CatalogModels { get; } = new();

    public ObservableCollection<ReleaseCardViewModel> FeaturedReleases { get; } = new();

    public Action<ReleaseCardViewModel>? OnSelectRelease { get; set; }

    public Action<ModelCardViewModel>? OnSelectModel { get; set; }

    public ObservableCollection<string> TagFilterChoices { get; }

    public ICommand ImportCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ExecuteSearchCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public ICommand SaveMetadataCommand { get; }

    public ICommand SaveDescriptionCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleOnlyFavoritesCommand { get; }

    public ICommand ToggleLibraryDrawerCommand { get; }

    public ICommand ShowDuplicatesCommand { get; }

    public ICommand FilterByArtistCommand { get; }

    public ICommand FilterByCollectionCommand { get; }

    public ICommand FilterByTagCommand { get; }

    public ICommand SetViewModeCommand { get; }

    public ICommand SetGridDensityCommand { get; }

    public string? ArtistFilter
    {
        get => _artistFilter;
        set
        {
            if (SetField(ref _artistFilter, value))
            {
                RaiseFilterProperties();
            }
        }
    }

    public string? CollectionFilter
    {
        get => _collectionFilter;
        set
        {
            if (SetField(ref _collectionFilter, value))
            {
                RaiseFilterProperties();
            }
        }
    }

    public string ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetField(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(IsDetailsView));
                SavePreferences();
            }
        }
    }

    public bool IsGridView => ViewMode == "Grid";

    public bool IsListView => ViewMode == "List";

    public bool IsDetailsView => ViewMode == "Details";

    public string GridDensity
    {
        get => _gridDensity;
        set
        {
            if (SetField(ref _gridDensity, value))
            {
                OnPropertyChanged(nameof(CardWidth));
                OnPropertyChanged(nameof(CardHeight));
                OnPropertyChanged(nameof(ThumbnailHeight));
                SavePreferences();
            }
        }
    }

    public double CardWidth => GridDensity switch
    {
        "Small" => 140,
        "Large" => 280,
        _ => 200
    };

    public double CardHeight => GridDensity switch
    {
        "Small" => 190,
        "Large" => 355,
        _ => 260
    };

    public double ThumbnailHeight => GridDensity switch
    {
        "Small" => 115,
        "Large" => 230,
        _ => 165
    };

    public bool OnlyFavoritesFilter
    {
        get => _onlyFavoritesFilter;
        set
        {
            if (SetField(ref _onlyFavoritesFilter, value))
            {
                RaiseFilterProperties();
                _ = ExecuteSearchAsync();
            }
        }
    }

    public long FavoritesCount
    {
        get => _favoritesCount;
        private set => SetField(ref _favoritesCount, value);
    }

    public bool IsLibraryDrawerOpen
    {
        get => _isLibraryDrawerOpen;
        set => SetField(ref _isLibraryDrawerOpen, value);
    }

    public string LibraryRootDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RootPath)) return "No library folder selected";
            var trimmed = RootPath.TrimEnd('/', '\\');
            var name = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? RootPath : name;
        }
    }

    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchQuery)
        || (TagFilter != "All" && !string.IsNullOrWhiteSpace(TagFilter))
        || !string.IsNullOrWhiteSpace(ArtistFilter)
        || !string.IsNullOrWhiteSpace(CollectionFilter)
        || OnlyFavoritesFilter;

    public bool IsEmptySearch => Models.Count == 0 && IsFiltered;

    public bool IsEmptyCatalog => Models.Count == 0 && !IsFiltered;

    public void FilterByArtist(string? artist)
    {
        ArtistFilter = artist;
        _ = ExecuteSearchAsync();
    }

    public void FilterByCollection(string? collection)
    {
        CollectionFilter = collection;
        _ = ExecuteSearchAsync();
    }

    public void FilterByTag(string? tag)
    {
        TagFilter = tag ?? "All";
        _ = ExecuteSearchAsync();
    }

    public void FilterBySearch(string? query)
    {
        SearchQuery = query ?? string.Empty;
        _ = ExecuteSearchAsync();
    }

    public void ClearSearch()
    {
        _searchDebounce?.Cancel();
        _searchQuery = string.Empty;
        _tagFilter = "All";
        _artistFilter = null;
        _collectionFilter = null;
        _onlyFavoritesFilter = false;
        OnPropertyChanged(nameof(SearchQuery));
        OnPropertyChanged(nameof(TagFilter));
        OnPropertyChanged(nameof(ArtistFilter));
        OnPropertyChanged(nameof(CollectionFilter));
        OnPropertyChanged(nameof(OnlyFavoritesFilter));
        RaiseFilterProperties();
        _ = ExecuteSearchAsync();
    }

    private void RaiseFilterProperties()
    {
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(IsEmptySearch));
        OnPropertyChanged(nameof(IsEmptyCatalog));
    }

    public bool HasSelectedModel => SelectedModel is not null;

    public string SelectedModelConfidenceText => SelectedModel != null
        ? $"{SelectedModel.ConfidenceScore:P0} ({SelectedModel.ConfidenceTier})"
        : string.Empty;

    public string SelectedModelMethodText => SelectedModel?.DiscoveryMethod ?? string.Empty;

    public int SelectedModelAssetCount => SelectedModel?.AssetCount ?? (DetailedModel?.Files?.Count ?? 0);

    public ModelSummaryDto? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (!SetField(ref _selectedModel, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedModel));
            OnPropertyChanged(nameof(SelectedModelConfidenceText));
            OnPropertyChanged(nameof(SelectedModelMethodText));
            OnPropertyChanged(nameof(SelectedModelAssetCount));
            RaiseCommands();
            if (value is null)
            {
                DetailedModel = null;
                ArtistDraft = string.Empty;
                CollectionDraft = string.Empty;
                TagsDraft = string.Empty;
                DescriptionDraft = string.Empty;
                PrintNotesDraft = string.Empty;
                PreviewPath = null;
                return;
            }

            if (DetailedModel?.Id == value.Id)
            {
                return;
            }

            _ = LoadModelDetailsAsync(value.Id);
        }
    }

    public string? PreviewPath
    {
        get => _previewPath;
        private set => SetField(ref _previewPath, value);
    }

    public ModelDetailDto? DetailedModel
    {
        get => _detailedModel;
        private set => SetField(ref _detailedModel, value);
    }

    public string ArtistDraft
    {
        get => _artistDraft;
        set => SetField(ref _artistDraft, value);
    }

    public string CollectionDraft
    {
        get => _collectionDraft;
        set => SetField(ref _collectionDraft, value);
    }

    public string TagsDraft
    {
        get => _tagsDraft;
        set => SetField(ref _tagsDraft, value);
    }

    public string DescriptionDraft
    {
        get => _descriptionDraft;
        set => SetField(ref _descriptionDraft, value);
    }

    public string PrintNotesDraft
    {
        get => _printNotesDraft;
        set => SetField(ref _printNotesDraft, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetField(ref _isScanning, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetField(ref _progressPercentage, value);
    }

    public string CurrentStatusText
    {
        get => _currentStatusText;
        private set => SetField(ref _currentStatusText, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                RaiseFilterProperties();
                _ = DebouncedSearchAsync();
            }
        }
    }

    public string TagFilter
    {
        get => _tagFilter;
        set
        {
            if (SetField(ref _tagFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
            {
                RaiseFilterProperties();
                _ = DebouncedSearchAsync();
            }
        }
    }

    public string RootPath
    {
        get => _rootPath;
        set
        {
            if (SetField(ref _rootPath, value))
            {
                OnPropertyChanged(nameof(LibraryRootDisplay));
                RaiseCommands();
                SavePreferences();
            }
        }
    }

    public Task LoadCatalogAsync(CancellationToken cancellationToken = default) =>
        ExecuteSearchAsync(cancellationToken);

    public async Task ExecuteSearchAsync(CancellationToken cancellationToken = default)
    {
        var items = await _catalogQuery.SearchModelsAsync(
            new ModelQueryParameters(
                SearchTerm: string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                TagFilter: TagFilter == "All" ? null : TagFilter,
                ArtistFilter: ArtistFilter,
                CollectionFilter: CollectionFilter,
                ScanRoot: string.IsNullOrWhiteSpace(RootPath) ? null : RootPath,
                OnlyFavorites: OnlyFavoritesFilter),
            cancellationToken).ConfigureAwait(true);
        var selectedId = SelectedModel?.Id;
        Models.Clear();
        CatalogModels.Clear();

        var query = SearchQuery?.Trim();
        var sortedItems = !string.IsNullOrWhiteSpace(query)
            ? items.OrderByDescending(m => !string.IsNullOrEmpty(m.CollectionName) && m.CollectionName.Equals(query, StringComparison.OrdinalIgnoreCase) ? 3 : 0)
                   .ThenByDescending(m => m.Name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 2 : 0)
                   .ThenByDescending(m => (!string.IsNullOrEmpty(m.CollectionName) && m.CollectionName.Contains(query, StringComparison.OrdinalIgnoreCase)) || m.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                   .ThenBy(m => m.Name)
                   .ToList()
            : items.ToList();

        foreach (var item in sortedItems)
        {
            Models.Add(item);
            CatalogModels.Add(CreateModelCard(item));
        }

        if (_releaseCatalogQuery is not null)
        {
            _ = LoadFeaturedReleasesAsync(cancellationToken);
        }

        if (selectedId is Guid id)
        {
            SelectedModel = Models.FirstOrDefault(model => model.Id == id);
        }

        QueueThumbnails(Models.ToArray());
        await RefreshFavoritesCountAsync(cancellationToken).ConfigureAwait(true);
        RaiseFilterProperties();

        if (IsFiltered)
        {
            var filterParts = new List<string>();
            if (OnlyFavoritesFilter) filterParts.Add("favorites");
            if (!string.IsNullOrWhiteSpace(SearchQuery)) filterParts.Add($"query \"{SearchQuery.Trim()}\"");
            if (TagFilter != "All" && !string.IsNullOrWhiteSpace(TagFilter)) filterParts.Add($"tag \"{TagFilter}\"");
            if (!string.IsNullOrWhiteSpace(ArtistFilter)) filterParts.Add($"artist \"{ArtistFilter}\"");
            if (!string.IsNullOrWhiteSpace(CollectionFilter)) filterParts.Add($"collection \"{CollectionFilter}\"");
            CurrentStatusText = $"{Models.Count} model{(Models.Count == 1 ? "" : "s")} found for {string.Join(", ", filterParts)}.";
        }
    }

    public async Task RefreshFavoritesCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_groupingQuery is not null)
            {
                FavoritesCount = await _groupingQuery.GetFavoritesCountAsync(
                    string.IsNullOrWhiteSpace(RootPath) ? null : RootPath,
                    cancellationToken).ConfigureAwait(true);
            }
        }
        catch
        {
            // Non-critical background count update
        }
    }

    public void ToggleOnlyFavorites()
    {
        OnlyFavoritesFilter = !OnlyFavoritesFilter;
    }

    public async Task ToggleFavoriteAsync(object? parameter)
    {
        Guid? targetId = parameter switch
        {
            Guid g => g,
            ModelSummaryDto summary => summary.Id,
            ModelCardViewModel card => card.Id,
            _ => SelectedModel?.Id
        };

        if (targetId is not Guid id || id == Guid.Empty)
        {
            return;
        }

        try
        {
            var currentFav = SelectedModel?.Id == id
                ? SelectedModel.IsFavorite
                : (Models.FirstOrDefault(m => m.Id == id)?.IsFavorite ?? false);
            var newFav = !currentFav;

            await _detailRepository.ToggleFavoriteAsync(id).ConfigureAwait(true);
            for (var i = 0; i < Models.Count; i++)
            {
                if (Models[i].Id == id)
                {
                    Models[i] = Models[i] with { IsFavorite = newFav };
                    break;
                }
            }

            for (var i = 0; i < CatalogModels.Count; i++)
            {
                if (CatalogModels[i].Id == id)
                {
                    CatalogModels[i].IsFavorite = newFav;
                    break;
                }
            }

            if (SelectedModel?.Id == id)
            {
                SelectedModel = SelectedModel with { IsFavorite = newFav };
            }

            if (DetailedModel?.Id == id)
            {
                DetailedModel = DetailedModel with { IsFavorite = newFav };
            }

            await RefreshFavoritesCountAsync().ConfigureAwait(true);

            if (OnlyFavoritesFilter && !newFav)
            {
                await ExecuteSearchAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            CurrentStatusText = $"Failed to toggle favorite: {ex.Message}";
        }
    }

    private async Task DebouncedSearchAsync()
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, token).ConfigureAwait(true);
            await ExecuteSearchAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task ImportAsync()
    {
        if (IsScanning || string.IsNullOrWhiteSpace(RootPath))
        {
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        ProgressPercentage = 0;
        CurrentStatusText = "Scanning";
        RaiseCommands();
        var acceptProgress = true;
        var progress = new Progress<ImportProgress>(report =>
        {
            if (!acceptProgress || _scanCancellation?.IsCancellationRequested == true)
            {
                return;
            }

            ProgressPercentage = report.Percentage;
            CurrentStatusText = report.StatusText;
        });

        try
        {
            var result = await _importService.ImportAsync(
                new ImportLibraryRequest(RootPath, MaxFileSizeBytes, BatchLimit),
                _scanCancellation.Token,
                progress).ConfigureAwait(true);
            acceptProgress = false;
            CurrentStatusText = result.Completed
                ? $"Import complete. Added {result.TotalAdded}, skipped {result.TotalSkipped}, errors {result.Errors.Count}."
                : "Import cancelled.";
            if (result.Completed)
            {
                ProgressPercentage = 100;
            }

            await LoadCatalogAsync().ConfigureAwait(true);
            if (_scanCancellation?.IsCancellationRequested == true)
            {
                CurrentStatusText = "Import cancelled.";
            }
        }
        catch (OperationCanceledException)
        {
            acceptProgress = false;
            CurrentStatusText = "Import cancelled.";
            await LoadCatalogAsync().ConfigureAwait(true);
            CurrentStatusText = "Import cancelled.";
        }
        catch (Exception exception)
        {
            acceptProgress = false;
            CurrentStatusText = $"Import failed: {exception.Message}";
        }
        finally
        {
            IsScanning = false;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
            RaiseCommands();
        }
    }

    public void CancelScan()
    {
        if (_scanCancellation is { IsCancellationRequested: false })
        {
            _scanCancellation.Cancel();
            CurrentStatusText = "Cancelling import...";
        }
    }

    private async Task LoadModelDetailsAsync(Guid modelId)
    {
        try
        {
            var detail = await _detailRepository.GetModelDetailAsync(modelId).ConfigureAwait(true);
            if (detail is null || SelectedModel?.Id != modelId)
            {
                return;
            }

            DetailedModel = detail;
            ArtistDraft = detail.ArtistName ?? string.Empty;
            CollectionDraft = detail.CollectionName ?? string.Empty;
            TagsDraft = detail.Tags is { Count: > 0 } ? string.Join(", ", detail.Tags) : string.Empty;
            DescriptionDraft = detail.Description ?? string.Empty;
            PrintNotesDraft = detail.PrintNotes ?? string.Empty;

            var chosenPreview = detail.Files.FirstOrDefault(file =>
                file.FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                file.FilePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.FilePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                file.FilePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))?.FilePath;

            PreviewPath = chosenPreview ?? SelectedModel?.ThumbnailPath ?? detail.Files.FirstOrDefault()?.FilePath;
            UpdateFilterChoices(detail.Tags);
        }
        catch
        {
            // Transient inspection failures are non-critical
        }
    }

    private void UpdateFilterChoices(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !TagFilterChoices.Contains(tag))
            {
                TagFilterChoices.Add(tag);
            }
        }
    }

    private void QueueThumbnails(IReadOnlyCollection<ModelSummaryDto> models)
    {
        if (_thumbnails is null)
        {
            return;
        }

        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = new CancellationTokenSource();
        var token = _thumbnailCancellation.Token;

        _ = Task.Run(
            async () =>
            {
                foreach (var model in models)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var path = await EnsureThumbnailPathAsync(model.ContentHash, model.FilePath, token)
                        .ConfigureAwait(false);
                    if (path is null)
                    {
                        continue;
                    }

                    Action updateAction = () =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var index = -1;
                        for (var itemIndex = 0; itemIndex < Models.Count; itemIndex++)
                        {
                            if (Models[itemIndex].Id == model.Id)
                            {
                                index = itemIndex;
                                break;
                            }
                        }

                        if (index < 0 || Models[index].ThumbnailPath == path)
                        {
                            return;
                        }

                        var updated = Models[index] with { ThumbnailPath = path };
                        Models[index] = updated;
                        if (SelectedModel?.Id == updated.Id)
                        {
                            SelectedModel = updated;
                        }
                    };

                    if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    {
                        updateAction();
                    }
                    else
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(updateAction, Avalonia.Threading.DispatcherPriority.Background);
                    }
                }
            },
            token);
    }

    private async Task<string?> EnsureThumbnailPathAsync(string contentHash, string filePath, CancellationToken cancellationToken)
    {
        if (_thumbnails is null || string.IsNullOrWhiteSpace(contentHash) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var existing = await _thumbnails.GetThumbnailPathAsync(contentHash, cancellationToken).ConfigureAwait(true);
        if (existing is not null)
        {
            return existing;
        }

        await _thumbnails.EnsureThumbnailGeneratedAsync(contentHash, filePath, cancellationToken).ConfigureAwait(true);
        return await _thumbnails.GetThumbnailPathAsync(contentHash, cancellationToken).ConfigureAwait(true);
    }

    private async Task SaveMetadataAsync()
    {
        if (SelectedModel is null)
        {
            return;
        }

        var tags = TagsDraft
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        await _detailRepository.UpdateModelMetadataAsync(
            SelectedModel.Id,
            string.IsNullOrWhiteSpace(ArtistDraft) ? null : ArtistDraft.Trim(),
            string.IsNullOrWhiteSpace(CollectionDraft) ? null : CollectionDraft.Trim(),
            tags).ConfigureAwait(true);

        await LoadModelDetailsAsync(SelectedModel.Id).ConfigureAwait(true);
        CurrentStatusText = $"Metadata saved for \"{SelectedModel.Name}\".";
    }

    private async Task SaveDescriptionAsync()
    {
        if (SelectedModel is null)
        {
            return;
        }

        await _detailRepository.UpdateModelDescriptionAsync(
            SelectedModel.Id,
            string.IsNullOrWhiteSpace(DescriptionDraft) ? null : DescriptionDraft.Trim(),
            string.IsNullOrWhiteSpace(PrintNotesDraft) ? null : PrintNotesDraft.Trim()).ConfigureAwait(true);

        if (DetailedModel is not null && DetailedModel.Id == SelectedModel.Id)
        {
            DetailedModel = DetailedModel with
            {
                Description = string.IsNullOrWhiteSpace(DescriptionDraft) ? null : DescriptionDraft.Trim(),
                PrintNotes = string.IsNullOrWhiteSpace(PrintNotesDraft) ? null : PrintNotesDraft.Trim()
            };
        }

        CurrentStatusText = $"Notes and description saved for \"{SelectedModel.Name}\".";
    }

    private void SavePreferences()
    {
        if (_preferencesService is null)
        {
            return;
        }

        var current = _preferencesService.LoadPreferences();
        _preferencesService.SavePreferences(current with
        {
            ViewMode = ViewMode,
            GridDensity = GridDensity,
            LastScanRoot = string.IsNullOrWhiteSpace(RootPath) ? current.LastScanRoot : RootPath
        });
    }

    private void RaiseCommands()
    {
        if (ImportCommand is RelayCommand import)
        {
            import.RaiseCanExecuteChanged();
        }

        if (CancelCommand is RelayCommand cancel)
        {
            cancel.RaiseCanExecuteChanged();
        }

        if (SaveMetadataCommand is RelayCommand save)
        {
            save.RaiseCanExecuteChanged();
        }

        if (SaveDescriptionCommand is RelayCommand saveDesc)
        {
            saveDesc.RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class EmptyDetailRepository : IModelDetailRepository
    {
        public Task<ModelDetailDto?> GetModelDetailAsync(Guid modelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelDetailDto?>(null);

        public Task UpdateModelMetadataAsync(
            Guid modelId,
            string? artistName,
            string? collectionName,
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ToggleFavoriteAsync(Guid modelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateModelDescriptionAsync(
            Guid modelId,
            string? description,
            string? printNotes,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }


    public async Task LoadFeaturedReleasesAsync(CancellationToken cancellationToken = default)
    {
        if (_releaseCatalogQuery is null)
        {
            return;
        }

        try
        {
            var releases = await _releaseCatalogQuery.GetReleasesAsync(
                new ReleaseQueryParameters(
                    ScanRoot: string.IsNullOrWhiteSpace(RootPath) ? null : RootPath,
                    Limit: 8),
                cancellationToken).ConfigureAwait(true);

            FeaturedReleases.Clear();
            foreach (var rel in releases)
            {
                FeaturedReleases.Add(new ReleaseCardViewModel(
                    rel,
                    onSelect: r => OnSelectRelease?.Invoke(r),
                    onToggleFavorite: async r =>
                    {
                        if (_releaseDetailRepository is not null)
                        {
                            await _releaseDetailRepository.ToggleReleaseFavoriteAsync(r.ReleaseId, cancellationToken).ConfigureAwait(true);
                            r.IsFavorite = !r.IsFavorite;
                        }
                    }));
            }
        }
        catch
        {
            // Non-critical background load
        }
    }

    private ModelCardViewModel CreateModelCard(ModelSummaryDto item)
    {
        return new ModelCardViewModel(
            item,
            onSelect: card =>
            {
                SelectedModel = item;
                OnSelectModel?.Invoke(card);
            },
            onToggleFavorite: async card =>
            {
                await ToggleFavoriteAsync(item).ConfigureAwait(true);
                card.IsFavorite = item.IsFavorite;
            });
    }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool> _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute ?? (_ => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        parameter is T typed ? _canExecute(typed) : (parameter is null ? _canExecute(default) : false);

    public void Execute(object? parameter)
    {
        if (parameter is T typed)
        {
            _execute(typed);
        }
        else if (parameter is null)
        {
            _execute(default);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
