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

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IModelImportService _importService;
    private readonly CatalogQuery _catalogQuery;
    private readonly IModelDetailRepository _detailRepository;
    private readonly IThumbnailService? _thumbnails;
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
    private ModelSummaryDto? _selectedModel;
    private ModelDetailDto? _detailedModel;
    private string _artistDraft = string.Empty;
    private string _collectionDraft = string.Empty;
    private string _tagsDraft = string.Empty;

    public DashboardViewModel(
        IModelImportService importService,
        CatalogQuery catalogQuery,
        long maxFileSizeBytes = 2L * 1024 * 1024 * 1024,
        int batchLimit = 500,
        string? rootPath = null,
        IModelDetailRepository? detailRepository = null,
        IThumbnailService? thumbnails = null)
    {
        _importService = importService;
        _catalogQuery = catalogQuery;
        _detailRepository = detailRepository ?? new EmptyDetailRepository();
        _thumbnails = thumbnails;
        MaxFileSizeBytes = maxFileSizeBytes;
        BatchLimit = batchLimit;
        _rootPath = rootPath ?? string.Empty;
        Models = new ObservableCollection<ModelSummaryDto>();
        TagFilterChoices = new ObservableCollection<string> { "All" };
        ImportCommand = new RelayCommand(() => _ = ImportAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(RootPath));
        CancelCommand = new RelayCommand(CancelScan, () => IsScanning);
        ExecuteSearchCommand = new RelayCommand(() => _ = ExecuteSearchAsync());
        ClearSearchCommand = new RelayCommand(ClearSearch);
        SaveMetadataCommand = new RelayCommand(() => _ = SaveMetadataAsync(), () => SelectedModel is not null && !IsScanning);
        ShowDuplicatesCommand = new RelayCommand(() => ShowDuplicates?.Invoke());
    }

    public Action? ShowDuplicates { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Welcome to 3D Print Vault";

    public string Description => "Your library overview will appear here.";

    public long MaxFileSizeBytes { get; }

    public int BatchLimit { get; }

    public ObservableCollection<ModelSummaryDto> Models { get; }

    public ObservableCollection<string> TagFilterChoices { get; }

    public ICommand ImportCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ExecuteSearchCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchQuery) || (TagFilter != "All" && !string.IsNullOrWhiteSpace(TagFilter));

    public bool IsEmptySearch => Models.Count == 0 && IsFiltered;

    public bool IsEmptyCatalog => Models.Count == 0 && !IsFiltered;

    public void ClearSearch()
    {
        SearchQuery = string.Empty;
        TagFilter = "All";
        _ = ExecuteSearchAsync();
    }

    private void RaiseFilterProperties()
    {
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(IsEmptySearch));
        OnPropertyChanged(nameof(IsEmptyCatalog));
    }

    public ICommand SaveMetadataCommand { get; }

    public ICommand ShowDuplicatesCommand { get; }

    public bool HasSelectedModel => SelectedModel is not null;

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
            RaiseCommands();
            if (value is null)
            {
                DetailedModel = null;
                ArtistDraft = string.Empty;
                CollectionDraft = string.Empty;
                TagsDraft = string.Empty;
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
                RaiseCommands();
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
                ScanRoot: string.IsNullOrWhiteSpace(RootPath) ? null : RootPath),
            cancellationToken).ConfigureAwait(true);
        var selectedId = SelectedModel?.Id;
        Models.Clear();
        foreach (var item in items)
        {
            Models.Add(item);
        }

        if (selectedId is Guid id)
        {
            SelectedModel = Models.FirstOrDefault(model => model.Id == id);
        }

        QueueThumbnails(Models.ToArray());
        RaiseFilterProperties();

        if (!string.IsNullOrWhiteSpace(SearchQuery) || TagFilter != "All")
        {
            CurrentStatusText = string.IsNullOrWhiteSpace(SearchQuery)
                ? $"{Models.Count} result{(Models.Count == 1 ? "" : "s")} for tag \"{TagFilter}\"."
                : $"{Models.Count} result{(Models.Count == 1 ? "" : "s")} for \"{SearchQuery.Trim()}\".";
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
            if (!acceptProgress)
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
        }
        catch (OperationCanceledException)
        {
            acceptProgress = false;
            CurrentStatusText = "Import cancelled.";
        }
        finally
        {
            IsScanning = false;
            _scanCancellation.Dispose();
            _scanCancellation = null;
            RaiseCommands();
        }
    }

    public void CancelScan() => _scanCancellation?.Cancel();

    public async Task LoadModelDetailsAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        var detail = await _detailRepository.GetModelDetailAsync(modelId, cancellationToken).ConfigureAwait(true);
        if (SelectedModel?.Id != modelId)
        {
            return;
        }

        DetailedModel = detail;
        ArtistDraft = detail?.ArtistName ?? string.Empty;
        CollectionDraft = detail?.CollectionName ?? string.Empty;
        TagsDraft = detail is null ? string.Empty : string.Join(", ", detail.Tags);
        if (detail is not null)
        {
            var previewFile = detail.Files.FirstOrDefault(file => file.IsActive) ?? detail.Files.FirstOrDefault();
            if (previewFile is not null)
            {
                PreviewPath = await EnsureThumbnailPathAsync(previewFile.FileHash, previewFile.FilePath, cancellationToken)
                    .ConfigureAwait(true);
            }

            foreach (var tag in detail.Tags)
            {
                if (!TagFilterChoices.Contains(tag))
                {
                    TagFilterChoices.Add(tag);
                }
            }
        }
    }

    public async Task SaveMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedModel is null)
        {
            return;
        }

        var tags = TagsDraft.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        await _detailRepository.UpdateModelMetadataAsync(
            SelectedModel.Id,
            ArtistDraft,
            CollectionDraft,
            tags,
            cancellationToken).ConfigureAwait(true);
        await LoadModelDetailsAsync(SelectedModel.Id, cancellationToken).ConfigureAwait(true);
        CurrentStatusText = "Metadata saved.";
    }

    private void QueueThumbnails(IReadOnlyList<ModelSummaryDto> models)
    {
        if (_thumbnails is null || models.Count == 0)
        {
            return;
        }

        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = new CancellationTokenSource();
        var token = _thumbnailCancellation.Token;
        _ = LoadThumbnailsAsync(models, token);
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<ModelSummaryDto> models, CancellationToken cancellationToken)
    {
        foreach (var model in models)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var path = await EnsureThumbnailPathAsync(model.ContentHash, model.FilePath, cancellationToken)
                .ConfigureAwait(true);
            if (path is null)
            {
                continue;
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
                continue;
            }

            var updated = Models[index] with { ThumbnailPath = path };
            Models[index] = updated;
            if (SelectedModel?.Id == updated.Id)
            {
                SelectedModel = updated;
            }
        }
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
