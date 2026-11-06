using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class ReleaseDetailViewModel : INotifyPropertyChanged
{
    private readonly IReleaseDetailRepository _releaseDetailRepository;
    private readonly IModelDetailRepository? _modelDetailRepository;
    private readonly Action _onBack;
    private readonly Action<string>? _onFilterByArtist;
    private readonly Action<ModelSummaryDto>? _onSelectModel;

    private Guid _releaseId;
    private string _name = string.Empty;
    private string? _artistName;
    private string? _description;
    private int _modelCount;
    private string? _coverImagePath;
    private bool _isFavorite;
    private string? _releaseDate;
    private bool _isLoading;
    private string _cardDensity = "Medium";
    private double _cardWidth = 200;
    private double _cardHeight = 250;
    private double _thumbnailHeight = 150;

    public ReleaseDetailViewModel(
        IReleaseDetailRepository releaseDetailRepository,
        Action onBack,
        IModelDetailRepository? modelDetailRepository = null,
        Action<string>? onFilterByArtist = null,
        Action<ModelSummaryDto>? onSelectModel = null)
    {
        _releaseDetailRepository = releaseDetailRepository;
        _onBack = onBack;
        _modelDetailRepository = modelDetailRepository;
        _onFilterByArtist = onFilterByArtist;
        _onSelectModel = onSelectModel;

        Models = new ObservableCollection<ModelSummaryDto>();

        BackCommand = new RelayCommand(_onBack);

        ToggleFavoriteCommand = new RelayCommand(async () =>
        {
            await ToggleFavoriteAsync().ConfigureAwait(true);
        });

        BrowseArtistCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(ArtistName))
            {
                _onFilterByArtist?.Invoke(ArtistName);
            }
        });

        SelectModelCommand = new RelayCommand<ModelSummaryDto>(model =>
        {
            if (model is not null)
            {
                _onSelectModel?.Invoke(model);
            }
        });

        ToggleModelFavoriteCommand = new RelayCommand<ModelSummaryDto>(async model =>
        {
            if (model is not null && _modelDetailRepository is not null)
            {
                await _modelDetailRepository.ToggleFavoriteAsync(model.Id).ConfigureAwait(true);
                var idx = Models.IndexOf(model);
                if (idx >= 0)
                {
                    var updated = model with { IsFavorite = !model.IsFavorite };
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        Models[idx] = updated;
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(() => Models[idx] = updated, DispatcherPriority.Background);
                    }
                }
            }
        });

        SetDensitySmallCommand = new RelayCommand(() => SetDensity("Small"));
        SetDensityMediumCommand = new RelayCommand(() => SetDensity("Medium"));
        SetDensityLargeCommand = new RelayCommand(() => SetDensity("Large"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModelSummaryDto> Models { get; }

    public ICommand BackCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand BrowseArtistCommand { get; }

    public ICommand SelectModelCommand { get; }

    public ICommand ToggleModelFavoriteCommand { get; }

    public ICommand SetDensitySmallCommand { get; }

    public ICommand SetDensityMediumCommand { get; }

    public ICommand SetDensityLargeCommand { get; }

    public Guid ReleaseId
    {
        get => _releaseId;
        private set => SetField(ref _releaseId, value);
    }

    public string Name
    {
        get => _name;
        private set => SetField(ref _name, value);
    }

    public string? ArtistName
    {
        get => _artistName;
        private set => SetField(ref _artistName, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public int ModelCount
    {
        get => _modelCount;
        private set => SetField(ref _modelCount, value);
    }

    public string? CoverImagePath
    {
        get => _coverImagePath;
        private set => SetField(ref _coverImagePath, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public string? ReleaseDate
    {
        get => _releaseDate;
        private set => SetField(ref _releaseDate, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string CardDensity
    {
        get => _cardDensity;
        private set => SetField(ref _cardDensity, value);
    }

    public double CardWidth
    {
        get => _cardWidth;
        private set => SetField(ref _cardWidth, value);
    }

    public double CardHeight
    {
        get => _cardHeight;
        private set => SetField(ref _cardHeight, value);
    }

    public double ThumbnailHeight
    {
        get => _thumbnailHeight;
        private set => SetField(ref _thumbnailHeight, value);
    }

    public async Task LoadReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        ReleaseId = releaseId;
        IsLoading = true;
        try
        {
            var detail = await _releaseDetailRepository.GetReleaseDetailAsync(releaseId, cancellationToken).ConfigureAwait(true);
            if (detail is not null)
            {
                Name = detail.Name;
                ArtistName = detail.ArtistName;
                Description = detail.Description;
                ModelCount = detail.ModelCount;
                CoverImagePath = detail.CoverImagePath;
                IsFavorite = detail.IsFavorite;
                ReleaseDate = detail.ReleaseDate;

                Models.Clear();
                foreach (var m in detail.Models)
                {
                    Models.Add(m);
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ToggleFavoriteAsync()
    {
        await _releaseDetailRepository.ToggleReleaseFavoriteAsync(ReleaseId).ConfigureAwait(true);
        IsFavorite = !IsFavorite;
    }

    private void SetDensity(string density)
    {
        CardDensity = density;
        switch (density)
        {
            case "Small":
                CardWidth = 140;
                CardHeight = 180;
                ThumbnailHeight = 100;
                break;
            case "Large":
                CardWidth = 280;
                CardHeight = 340;
                ThumbnailHeight = 220;
                break;
            default: // Medium
                CardWidth = 200;
                CardHeight = 250;
                ThumbnailHeight = 150;
                break;
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
