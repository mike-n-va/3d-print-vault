using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class FavoritesViewModel : INotifyPropertyChanged
{
    private readonly IModelCatalogQuery _modelCatalogQuery;
    private readonly IModelDetailRepository _modelDetailRepository;
    private readonly IReleaseCatalogQuery _releaseCatalogQuery;
    private readonly IReleaseDetailRepository _releaseDetailRepository;
    private readonly IModelGroupingQuery _groupingQuery;
    private readonly Action<ModelSummaryDto>? _onSelectModel;
    private readonly Action<ReleaseSummaryDto>? _onSelectRelease;
    private readonly Action<string>? _onSelectArtist;

    private string _selectedCategory = "Models";
    private bool _isLoading;
    private string _scanRoot = string.Empty;
    private int _modelsCount;
    private int _releasesCount;
    private int _artistsCount;

    public FavoritesViewModel(
        IModelCatalogQuery modelCatalogQuery,
        IModelDetailRepository modelDetailRepository,
        IReleaseCatalogQuery releaseCatalogQuery,
        IReleaseDetailRepository releaseDetailRepository,
        IModelGroupingQuery groupingQuery,
        Action<ModelSummaryDto>? onSelectModel = null,
        Action<ReleaseSummaryDto>? onSelectRelease = null,
        Action<string>? onSelectArtist = null)
    {
        _modelCatalogQuery = modelCatalogQuery;
        _modelDetailRepository = modelDetailRepository;
        _releaseCatalogQuery = releaseCatalogQuery;
        _releaseDetailRepository = releaseDetailRepository;
        _groupingQuery = groupingQuery;
        _onSelectModel = onSelectModel;
        _onSelectRelease = onSelectRelease;
        _onSelectArtist = onSelectArtist;

        FavoriteModels = new ObservableCollection<ModelSummaryDto>();
        FavoriteReleases = new ObservableCollection<ReleaseSummaryDto>();
        FavoriteArtists = new ObservableCollection<ArtistGroupDto>();

        SelectCategoryModelsCommand = new RelayCommand(() => SelectedCategory = "Models");
        SelectCategoryReleasesCommand = new RelayCommand(() => SelectedCategory = "Releases");
        SelectCategoryArtistsCommand = new RelayCommand(() => SelectedCategory = "Artists");

        SelectModelCommand = new RelayCommand<ModelSummaryDto>(model =>
        {
            if (model is not null)
            {
                _onSelectModel?.Invoke(model);
            }
        });

        SelectReleaseCommand = new RelayCommand<ReleaseSummaryDto>(release =>
        {
            if (release is not null)
            {
                _onSelectRelease?.Invoke(release);
            }
        });

        SelectArtistCommand = new RelayCommand<string>(artist =>
        {
            if (!string.IsNullOrWhiteSpace(artist))
            {
                _onSelectArtist?.Invoke(artist);
            }
        });

        ToggleModelFavoriteCommand = new RelayCommand<ModelSummaryDto>(async model =>
        {
            if (model is not null)
            {
                await _modelDetailRepository.ToggleFavoriteAsync(model.Id).ConfigureAwait(true);
                void Remove()
                {
                    FavoriteModels.Remove(model);
                    ModelsCount = FavoriteModels.Count;
                }

                if (Dispatcher.UIThread.CheckAccess())
                {
                    Remove();
                }
                else
                {
                    Dispatcher.UIThread.Post(Remove, DispatcherPriority.Background);
                }
            }
        });

        ToggleReleaseFavoriteCommand = new RelayCommand<ReleaseSummaryDto>(async release =>
        {
            if (release is not null)
            {
                await _releaseDetailRepository.ToggleReleaseFavoriteAsync(release.ReleaseId).ConfigureAwait(true);
                void Remove()
                {
                    FavoriteReleases.Remove(release);
                    ReleasesCount = FavoriteReleases.Count;
                }

                if (Dispatcher.UIThread.CheckAccess())
                {
                    Remove();
                }
                else
                {
                    Dispatcher.UIThread.Post(Remove, DispatcherPriority.Background);
                }
            }
        });

        ToggleArtistFavoriteCommand = new RelayCommand<ArtistGroupDto>(async artist =>
        {
            if (artist is not null)
            {
                await _groupingQuery.ToggleArtistFavoriteAsync(artist.Name).ConfigureAwait(true);
                void Remove()
                {
                    FavoriteArtists.Remove(artist);
                    ArtistsCount = FavoriteArtists.Count;
                }

                if (Dispatcher.UIThread.CheckAccess())
                {
                    Remove();
                }
                else
                {
                    Dispatcher.UIThread.Post(Remove, DispatcherPriority.Background);
                }
            }
        });

        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModelSummaryDto> FavoriteModels { get; }

    public ObservableCollection<ReleaseSummaryDto> FavoriteReleases { get; }

    public ObservableCollection<ArtistGroupDto> FavoriteArtists { get; }

    public ICommand SelectCategoryModelsCommand { get; }

    public ICommand SelectCategoryReleasesCommand { get; }

    public ICommand SelectCategoryArtistsCommand { get; }

    public ICommand SelectModelCommand { get; }

    public ICommand SelectReleaseCommand { get; }

    public ICommand SelectArtistCommand { get; }

    public ICommand ToggleModelFavoriteCommand { get; }

    public ICommand ToggleReleaseFavoriteCommand { get; }

    public ICommand ToggleArtistFavoriteCommand { get; }

    public ICommand RefreshCommand { get; }

    public string ScanRoot
    {
        get => _scanRoot;
        set => SetField(ref _scanRoot, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(IsModelsSelected));
                OnPropertyChanged(nameof(IsReleasesSelected));
                OnPropertyChanged(nameof(IsArtistsSelected));
            }
        }
    }

    public bool IsModelsSelected => SelectedCategory == "Models";

    public bool IsReleasesSelected => SelectedCategory == "Releases";

    public bool IsArtistsSelected => SelectedCategory == "Artists";

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public int ModelsCount
    {
        get => _modelsCount;
        private set => SetField(ref _modelsCount, value);
    }

    public int ReleasesCount
    {
        get => _releasesCount;
        private set => SetField(ref _releasesCount, value);
    }

    public int ArtistsCount
    {
        get => _artistsCount;
        private set => SetField(ref _artistsCount, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var root = string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot;

            // Load Models
            var modelParams = new ModelQueryParameters(OnlyFavorites: true, ScanRoot: root, Limit: 200);
            var models = await _modelCatalogQuery.SearchModelsAsync(modelParams, cancellationToken).ConfigureAwait(true);

            // Load Releases
            var releaseParams = new ReleaseQueryParameters(OnlyFavorites: true, ScanRoot: root, Limit: 200);
            var releases = await _releaseCatalogQuery.GetReleasesAsync(releaseParams, cancellationToken).ConfigureAwait(true);

            // Load Artists
            var allArtists = await _groupingQuery.GetArtistsAsync(root, cancellationToken).ConfigureAwait(true);
            var favArtists = allArtists.Where(a => a.IsFavorite).ToList();

            FavoriteModels.Clear();
            foreach (var m in models)
            {
                FavoriteModels.Add(m);
            }
            ModelsCount = FavoriteModels.Count;

            FavoriteReleases.Clear();
            foreach (var r in releases)
            {
                FavoriteReleases.Add(r);
            }
            ReleasesCount = FavoriteReleases.Count;

            FavoriteArtists.Clear();
            foreach (var a in favArtists)
            {
                FavoriteArtists.Add(a);
            }
            ArtistsCount = FavoriteArtists.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
