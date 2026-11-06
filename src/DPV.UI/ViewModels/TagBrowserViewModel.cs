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

public sealed class TagBrowserViewModel : INotifyPropertyChanged
{
    private readonly IModelGroupingQuery _groupingQuery;
    private readonly Action<string> _onSelectTag;
    private string _searchFilter = string.Empty;
    private bool _isLoading;
    private string _scanRoot = string.Empty;

    public TagBrowserViewModel(IModelGroupingQuery groupingQuery, Action<string> onSelectTag)
    {
        _groupingQuery = groupingQuery;
        _onSelectTag = onSelectTag;
        AllTags = [];
        Tags = new ObservableCollection<TagGroupDto>();
        SelectTagCommand = new RelayCommand<string>(tag =>
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _onSelectTag(tag);
            }
        });
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TagGroupDto> Tags { get; }

    private List<TagGroupDto> AllTags { get; set; }

    public ICommand SelectTagCommand { get; }

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
            var results = await _groupingQuery.GetTagsAsync(
                string.IsNullOrWhiteSpace(ScanRoot) ? null : ScanRoot,
                cancellationToken).ConfigureAwait(true);
            AllTags = results.ToList();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Tags.Clear();
        var filter = SearchFilter?.Trim();
        var matching = string.IsNullOrWhiteSpace(filter)
            ? AllTags
            : AllTags.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matching)
        {
            Tags.Add(item);
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
