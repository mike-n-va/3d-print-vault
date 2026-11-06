using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class ReleaseCardViewModel : INotifyPropertyChanged
{
    private bool _isFavorite;
    private string? _bannerPath;

    public ReleaseCardViewModel(
        ReleaseSummaryDto release,
        Action<ReleaseCardViewModel>? onSelect = null,
        Func<ReleaseCardViewModel, Task>? onToggleFavorite = null)
    {
        ReleaseId = release.ReleaseId;
        Name = release.Name;
        ArtistName = release.ArtistName;
        Description = release.Description;
        _bannerPath = release.CoverImagePath;
        ModelCount = release.ModelCount;
        _isFavorite = release.IsFavorite;
        ReleaseDate = release.ReleaseDate;

        SelectCommand = new RelayCommand(() => onSelect?.Invoke(this));
        ToggleFavoriteCommand = new RelayCommand(async () =>
        {
            if (onToggleFavorite is not null)
            {
                await onToggleFavorite(this).ConfigureAwait(true);
            }
            else
            {
                IsFavorite = !IsFavorite;
            }
        });
    }

    public ReleaseCardViewModel(
        Guid releaseId,
        string name,
        string? artistName = null,
        string? description = null,
        string? bannerPath = null,
        int modelCount = 0,
        bool isFavorite = false,
        string? releaseDate = null,
        Action<ReleaseCardViewModel>? onSelect = null,
        Func<ReleaseCardViewModel, Task>? onToggleFavorite = null)
    {
        ReleaseId = releaseId;
        Name = name;
        ArtistName = artistName;
        Description = description;
        _bannerPath = bannerPath;
        ModelCount = modelCount;
        _isFavorite = isFavorite;
        ReleaseDate = releaseDate;

        SelectCommand = new RelayCommand(() => onSelect?.Invoke(this));
        ToggleFavoriteCommand = new RelayCommand(async () =>
        {
            if (onToggleFavorite is not null)
            {
                await onToggleFavorite(this).ConfigureAwait(true);
            }
            else
            {
                IsFavorite = !IsFavorite;
            }
        });
    }

    public Guid ReleaseId { get; }

    public string Name { get; }

    public string? ArtistName { get; }

    public string? Description { get; }

    public int ModelCount { get; }

    public string? ReleaseDate { get; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public string? BannerPath
    {
        get => _bannerPath;
        set => SetField(ref _bannerPath, value);
    }

    public ICommand SelectCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
