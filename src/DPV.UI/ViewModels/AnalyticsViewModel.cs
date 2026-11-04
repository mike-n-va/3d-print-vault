using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class AnalyticsViewModel : INotifyPropertyChanged
{
    private readonly ILibraryStatsRepository _stats;
    private string _statusText = "Library totals are counted from the catalog.";
    private long _totalModels;
    private long _totalFiles;
    private long _totalBytesUsed;
    private int _totalArtists;
    private int _totalCollections;
    private int _totalTags;

    public AnalyticsViewModel(ILibraryStatsRepository stats)
    {
        _stats = stats;
        TopArtists = new ObservableCollection<RankedCount>();
        TopCollections = new ObservableCollection<RankedCount>();
        TopTags = new ObservableCollection<RankedCount>();
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Library analytics";

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public long TotalModels
    {
        get => _totalModels;
        private set => SetField(ref _totalModels, value);
    }

    public long TotalFiles
    {
        get => _totalFiles;
        private set => SetField(ref _totalFiles, value);
    }

    public long TotalBytesUsed
    {
        get => _totalBytesUsed;
        private set
        {
            if (SetField(ref _totalBytesUsed, value))
            {
                OnPropertyChanged(nameof(DiskUsageText));
            }
        }
    }

    public string DiskUsageText => FormatBytes(TotalBytesUsed);

    public int TotalArtists
    {
        get => _totalArtists;
        private set => SetField(ref _totalArtists, value);
    }

    public int TotalCollections
    {
        get => _totalCollections;
        private set => SetField(ref _totalCollections, value);
    }

    public int TotalTags
    {
        get => _totalTags;
        private set => SetField(ref _totalTags, value);
    }

    public ObservableCollection<RankedCount> TopArtists { get; }

    public ObservableCollection<RankedCount> TopCollections { get; }

    public ObservableCollection<RankedCount> TopTags { get; }

    public ICommand RefreshCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _stats.GetLibraryStatsAsync(cancellationToken).ConfigureAwait(true);
            TotalModels = stats.TotalModels;
            TotalFiles = stats.TotalFiles;
            TotalBytesUsed = stats.TotalBytesUsed;
            TotalArtists = stats.TotalArtists;
            TotalCollections = stats.TotalCollections;
            TotalTags = stats.TotalTags;
            Replace(TopArtists, stats.TopArtists);
            Replace(TopCollections, stats.TopCollections);
            Replace(TopTags, stats.TopTags);
            StatusText = $"{TotalModels} models, {DiskUsageText} in active files.";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private static void Replace<T>(ObservableCollection<RankedCount> target, IReadOnlyCollection<T> source)
        where T : notnull
    {
        target.Clear();
        var counts = new List<(string Name, int Count)>();
        foreach (var item in source)
        {
            counts.Add(item switch
            {
                TopArtistDto artist => (artist.Name, artist.ModelCount),
                TopCollectionDto collection => (collection.Name, collection.ModelCount),
                TopTagDto tag => (tag.Name, tag.ModelCount),
                _ => throw new InvalidOperationException("Unexpected ranking row.")
            });
        }

        var maximum = 0;
        foreach (var row in counts)
        {
            maximum = Math.Max(maximum, row.Count);
        }

        foreach (var row in counts)
        {
            target.Add(new RankedCount(row.Name, row.Count, maximum == 0 ? 0 : (100d * row.Count) / maximum));
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes} {units[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{size:0.##} {units[unit]}");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

    public sealed record RankedCount(string Name, int Count, double Share);

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
