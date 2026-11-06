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

public sealed class ReleaseBrowserViewModel : INotifyPropertyChanged
{
    private readonly IReleaseCatalogQuery _releaseCatalogQuery;
    private readonly IReleaseDetailRepository _releaseDetailRepository;
    private readonly Action<ReleaseSummaryDto> _onSelectRelease;
    private string _searchFilter = string.Empty;
    private bool _isLoading;
    private string _scanRoot = string.Empty;
    private bool _onlyFavorites;
    private Guid? _artistFilter;
    private string? _artistFilterName;

    public ReleaseBrowserViewModel(
        IReleaseCatalogQuery releaseCatalogQuery,
        IReleaseDetailRepository releaseDetailRepository,
        Action<ReleaseSummaryDto> onSelectRelease)
    {
        _releaseCatalogQuery = releaseCatalogQuery;
        _releaseDetailRepository = releaseDetailRepository;
        _onSelectRelease = onSelectRelease;

        AllReleases = [];
        Releases = new ObservableCollection<ReleaseSummaryDto>();

        SelectReleaseCommand = new RelayCommand<ReleaseSummaryDto>(release =>
        {
            if (release is not null)
            {
                _onSelectRelease(release);
            }
        });

        ToggleFavoriteCommand = new RelayCommand<ReleaseSummaryDto>(async release =>
        {
            if (release is not null)
            {
                await ToggleFavoriteAsync(release).ConfigureAwait(true);
            }
        });

        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ReleaseSummaryDto> Releases { get; }

    private List<ReleaseSummaryDto> AllReleases { get; set; }

    public ICommand SelectReleaseCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand RefreshCommand { get; }

    public string ScanRoot
    {
        get => _scanRoot;
        set => SetField(ref _scanRoot, value);
    }

    public bool OnlyFavorites
    {
        get => _onlyFavorites;
        set
        {
            if (SetField(ref _onlyFavorites, value))
            {
                _ = LoadAsync();
            }
        }
    }

    public Guid? ArtistFilter
    {
        get => _artistFilter;
        set => SetField(ref _artistFilter, value);
    }

    public string? ArtistFilterName
    {
        get => _artistFilterName;
        set => SetField(ref _artistFilterName, value);
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var parameters = new ReleaseQueryParameters(
                SearchTerm: string.IsNullOrWhiteSpace(SearchFilter) ? null : SearchFilter.Trim(),
                ArtistId: ArtistFilter,
                ArtistName: ArtistFilterName,
                OnlyFavorites: OnlyFavorites,
                ScanRoot: string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot,
                Limit: 200);

            var results = await _releaseCatalogQuery.GetReleasesAsync(parameters, cancellationToken).ConfigureAwait(true);
            AllReleases = results.ToList();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ToggleFavoriteAsync(ReleaseSummaryDto release)
    {
        ArgumentNullException.ThrowIfNull(release);

        await _releaseDetailRepository.ToggleReleaseFavoriteAsync(release.ReleaseId).ConfigureAwait(true);

        var newFavorite = !release.IsFavorite;
        var updated = release with { IsFavorite = newFavorite };

        var idx = Releases.IndexOf(release);
        if (idx >= 0)
        {
            if (OnlyFavorites && !newFavorite)
            {
                Releases.RemoveAt(idx);
            }
            else
            {
                Releases[idx] = updated;
            }
        }

        var allIdx = AllReleases.FindIndex(r => r.ReleaseId == release.ReleaseId);
        if (allIdx >= 0)
        {
            AllReleases[allIdx] = updated;
        }
    }

    private void ApplyFilter()
    {
        Releases.Clear();
        var filter = SearchFilter?.Trim();
        var matching = string.IsNullOrWhiteSpace(filter)
            ? AllReleases
            : AllReleases.Where(r =>
                r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (r.ArtistName is not null && r.ArtistName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (r.Description is not null && r.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)));

        foreach (var item in matching)
        {
            Releases.Add(item);
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
