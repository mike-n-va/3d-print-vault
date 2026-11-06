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

public sealed class ArtistBrowserViewModel : INotifyPropertyChanged
{
    private readonly IModelGroupingQuery _groupingQuery;
    private readonly Action<string> _onSelectArtist;
    private readonly IReleaseCatalogQuery? _releaseCatalogQuery;
    private readonly Action<ReleaseSummaryDto>? _onSelectRelease;
    private string _searchFilter = string.Empty;
    private bool _isLoading;
    private string _scanRoot = string.Empty;
    private bool _isPortfolioMode;
    private ArtistGroupDto? _selectedArtist;
    private int _artistReleaseCount;

    public ArtistBrowserViewModel(
        IModelGroupingQuery groupingQuery,
        Action<string> onSelectArtist,
        IReleaseCatalogQuery? releaseCatalogQuery = null,
        Action<ReleaseSummaryDto>? onSelectRelease = null)
    {
        _groupingQuery = groupingQuery;
        _onSelectArtist = onSelectArtist;
        _releaseCatalogQuery = releaseCatalogQuery;
        _onSelectRelease = onSelectRelease;

        AllArtists = [];
        Artists = new ObservableCollection<ArtistGroupDto>();
        ArtistReleases = new ObservableCollection<ReleaseSummaryDto>();

        SelectArtistCommand = new RelayCommand<object>(param =>
        {
            string? artistName = param switch
            {
                ArtistGroupDto dto => dto.Name,
                string s => s,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(artistName))
            {
                _onSelectArtist?.Invoke(artistName);

                var existing = AllArtists.FirstOrDefault(a => a.Name.Equals(artistName, StringComparison.OrdinalIgnoreCase))
                    ?? new ArtistGroupDto(artistName, 0);
                OpenArtistPortfolio(existing);
            }
        });

        BackToArtistsCommand = new RelayCommand(() =>
        {
            IsPortfolioMode = false;
            SelectedArtist = null;
        });

        BrowseAllArtistModelsCommand = new RelayCommand(() =>
        {
            if (SelectedArtist is not null)
            {
                _onSelectArtist(SelectedArtist.Name);
            }
        });

        SelectReleaseCommand = new RelayCommand<ReleaseSummaryDto>(release =>
        {
            if (release is not null)
            {
                _onSelectRelease?.Invoke(release);
            }
        });

        ToggleArtistFavoriteCommand = new RelayCommand<ArtistGroupDto>(async artist =>
        {
            if (artist is not null)
            {
                await _groupingQuery.ToggleArtistFavoriteAsync(artist.Name).ConfigureAwait(true);
                var idx = Artists.IndexOf(artist);
                if (idx >= 0)
                {
                    var updated = artist with { IsFavorite = !artist.IsFavorite };
                    void Update() => Artists[idx] = updated;
                    if (Dispatcher.UIThread.CheckAccess()) Update();
                    else Dispatcher.UIThread.Post(Update, DispatcherPriority.Background);

                    if (SelectedArtist?.Name == artist.Name)
                    {
                        SelectedArtist = updated;
                    }
                }
            }
        });

        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ArtistGroupDto> Artists { get; }

    public ObservableCollection<ReleaseSummaryDto> ArtistReleases { get; }

    private List<ArtistGroupDto> AllArtists { get; set; }

    public ICommand SelectArtistCommand { get; }

    public ICommand BackToArtistsCommand { get; }

    public ICommand BrowseAllArtistModelsCommand { get; }

    public ICommand SelectReleaseCommand { get; }

    public ICommand ToggleArtistFavoriteCommand { get; }

    public ICommand RefreshCommand { get; }

    public string ScanRoot
    {
        get => _scanRoot;
        set => SetField(ref _scanRoot, value);
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetField(ref _searchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public bool IsPortfolioMode
    {
        get => _isPortfolioMode;
        private set => SetField(ref _isPortfolioMode, value);
    }

    public ArtistGroupDto? SelectedArtist
    {
        get => _selectedArtist;
        private set => SetField(ref _selectedArtist, value);
    }

    public int ArtistReleaseCount
    {
        get => _artistReleaseCount;
        private set => SetField(ref _artistReleaseCount, value);
    }

    public void OpenArtistPortfolio(ArtistGroupDto artist)
    {
        SelectedArtist = artist;
        IsPortfolioMode = true;
        _ = LoadArtistReleasesAsync(artist.Name);
    }

    public async Task LoadArtistReleasesAsync(string artistName)
    {
        if (_releaseCatalogQuery is null)
        {
            return;
        }

        try
        {
            var releases = await _releaseCatalogQuery.GetReleasesAsync(
                new ReleaseQueryParameters(ArtistName: artistName, ScanRoot: string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot, Limit: 100))
                .ConfigureAwait(true);

            void Update()
            {
                ArtistReleases.Clear();
                foreach (var r in releases)
                {
                    ArtistReleases.Add(r);
                }
                ArtistReleaseCount = ArtistReleases.Count;
            }

            if (Dispatcher.UIThread.CheckAccess()) Update();
            else Dispatcher.UIThread.Post(Update, DispatcherPriority.Background);
        }
        catch
        {
            // Ignore failure loading artist releases
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var results = await _groupingQuery.GetArtistsAsync(
                string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot,
                cancellationToken).ConfigureAwait(true);
            AllArtists = results.ToList();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Artists.Clear();
        var filter = SearchFilter?.Trim();
        var matching = string.IsNullOrWhiteSpace(filter)
            ? AllArtists
            : AllArtists.Where(a => a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matching)
        {
            Artists.Add(item);
        }
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
