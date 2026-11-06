using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class ModelCardViewModel : INotifyPropertyChanged
{
    private bool _isHovered;
    private bool _isFavorite;
    private string? _thumbnailPath;
    private string? _hoverPreviewPath;

    public ModelCardViewModel(
        ModelSummaryDto model,
        Action<ModelCardViewModel>? onSelect = null,
        Func<ModelCardViewModel, Task>? onToggleFavorite = null)
    {
        Id = model.Id;
        Name = model.Name;
        ArtistName = model.ArtistName;
        CollectionName = model.CollectionName;
        ReleaseName = model.CollectionName;
        AssetCount = model.AssetCount;
        _isFavorite = model.IsFavorite;
        _thumbnailPath = model.ThumbnailPath;
        _hoverPreviewPath = model.ThumbnailPath;

        IsPreSupported = model.Name.Contains("presupported", StringComparison.OrdinalIgnoreCase)
                         || model.Name.Contains("supported", StringComparison.OrdinalIgnoreCase);

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

    public ModelCardViewModel(
        Guid id,
        string name,
        string? artistName = null,
        string? collectionName = null,
        string? releaseName = null,
        string? thumbnailPath = null,
        string? hoverPreviewPath = null,
        int assetCount = 1,
        bool isFavorite = false,
        bool isPreSupported = false,
        Action<ModelCardViewModel>? onSelect = null,
        Func<ModelCardViewModel, Task>? onToggleFavorite = null)
    {
        Id = id;
        Name = name;
        ArtistName = artistName;
        CollectionName = collectionName;
        ReleaseName = releaseName ?? collectionName;
        _thumbnailPath = thumbnailPath;
        _hoverPreviewPath = hoverPreviewPath ?? thumbnailPath;
        AssetCount = assetCount;
        _isFavorite = isFavorite;
        IsPreSupported = isPreSupported;

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

    public Guid Id { get; }

    public string Name { get; }

    public string? ArtistName { get; }

    public string? CollectionName { get; }

    public string? ReleaseName { get; }

    public int AssetCount { get; }

    public bool IsPreSupported { get; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (SetField(ref _isHovered, value))
            {
                OnPropertyChanged(nameof(ActiveImagePath));
            }
        }
    }

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (SetField(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(ActiveImagePath));
            }
        }
    }

    public string? HoverPreviewPath
    {
        get => _hoverPreviewPath;
        set
        {
            if (SetField(ref _hoverPreviewPath, value))
            {
                OnPropertyChanged(nameof(ActiveImagePath));
            }
        }
    }

    public string? ActiveImagePath => (IsHovered && !string.IsNullOrWhiteSpace(_hoverPreviewPath))
        ? _hoverPreviewPath
        : _thumbnailPath;

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
