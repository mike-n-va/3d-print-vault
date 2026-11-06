using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class CollectionBrowserViewModel : INotifyPropertyChanged
{
    private readonly IModelGroupingQuery _groupingQuery;
    private readonly Action<string> _onSelectCollection;
    private string _searchFilter = string.Empty;
    private bool _isLoading;
    private string _scanRoot = string.Empty;

    public CollectionBrowserViewModel(IModelGroupingQuery groupingQuery, Action<string> onSelectCollection)
    {
        _groupingQuery = groupingQuery;
        _onSelectCollection = onSelectCollection;
        AllCollections = [];
        Collections = new ObservableCollection<CollectionGroupDto>();
        SelectCollectionCommand = new RelayCommand<string>(collection =>
        {
            if (!string.IsNullOrWhiteSpace(collection))
            {
                _onSelectCollection(collection);
            }
        });
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CollectionGroupDto> Collections { get; }

    private List<CollectionGroupDto> AllCollections { get; set; }

    public ICommand SelectCollectionCommand { get; }

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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var results = await _groupingQuery.GetCollectionsAsync(
                string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot,
                cancellationToken).ConfigureAwait(true);
            AllCollections = results.ToList();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Collections.Clear();
        var filter = SearchFilter?.Trim();
        var matching = string.IsNullOrWhiteSpace(filter)
            ? AllCollections
            : AllCollections.Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matching)
        {
            Collections.Add(item);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
