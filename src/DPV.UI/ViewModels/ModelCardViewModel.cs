using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
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
        Scale = model.Scale;
        FolderPath = model.FolderPath;
        RootDirectoryPath = model.FolderPath;
        if (!string.IsNullOrWhiteSpace(model.ActiveImagePath))
        {
            _thumbnailPath = model.ActiveImagePath;
            _hoverPreviewPath = model.ActiveImagePath;
        }

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
        string? scale = null,
        string? folderPath = null,
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
        Scale = scale;
        FolderPath = folderPath;
        RootDirectoryPath = folderPath;

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

    public string? Scale { get; init; }

    public string? FolderPath { get; init; }

    public bool IsPreSupported { get; }

    public ArtworkTier ResolutionTier { get; set; } = ArtworkTier.Tier4GeometricFallback;

    public string? RootDirectoryPath { get; set; }

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

    public async Task ResolveArtworkAsync(
        IArtworkResolutionService artworkService,
        string? directoryPath = null,
        IEnumerable<string>? candidateFiles = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(ThumbnailPath) && System.IO.File.Exists(ThumbnailPath))
        {
            return;
        }

        var dir = directoryPath ?? RootDirectoryPath;
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }

        var result = await artworkService.ResolveModelArtworkAsync(
            Id,
            dir,
            candidateFiles,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.ResolvedPath))
        {
            ResolutionTier = result.Tier;
            ThumbnailPath = result.ResolvedPath;
            HoverPreviewPath = result.ResolvedPath;
        }
    }

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
