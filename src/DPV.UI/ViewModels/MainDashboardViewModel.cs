using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;

namespace DPV.UI.ViewModels;

public class MainDashboardViewModel : DashboardViewModel
{
    private readonly IDirectoryScannerService? _directoryScanner;

    public MainDashboardViewModel(
        IModelImportService importService,
        CatalogQuery catalogQuery,
        long maxFileSizeBytes = 2L * 1024 * 1024 * 1024,
        int batchLimit = 500,
        string? rootPath = null,
        IModelDetailRepository? detailRepository = null,
        IThumbnailService? thumbnails = null,
        IUserPreferencesService? preferencesService = null,
        IModelGroupingQuery? groupingQuery = null,
        IReleaseCatalogQuery? releaseCatalogQuery = null,
        IReleaseDetailRepository? releaseDetailRepository = null,
        IArtworkResolutionService? artworkService = null,
        IDirectoryScannerService? directoryScanner = null)
        : base(
            importService,
            catalogQuery,
            maxFileSizeBytes,
            batchLimit,
            rootPath,
            detailRepository,
            thumbnails,
            preferencesService,
            groupingQuery,
            releaseCatalogQuery,
            releaseDetailRepository,
            artworkService)
    {
        _directoryScanner = directoryScanner;
        BrowseFolderCommand = new RelayCommand(async () => await BrowseFolderAsync().ConfigureAwait(true));
    }

    public string ScanPath
    {
        get => RootPath;
        set
        {
            RootPath = value;
            OnPropertyChanged(nameof(ScanPath));
        }
    }

    public ICommand BrowseFolderCommand { get; }

    public ICommand ScanVaultCommand => ImportCommand;

    public override async Task ImportAsync()
    {
        if (_directoryScanner is null)
        {
            await base.ImportAsync().ConfigureAwait(true);
            return;
        }

        if (IsScanning || string.IsNullOrWhiteSpace(RootPath))
        {
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        ProgressPercentage = 0;
        CurrentStatusText = "Scanning 4-tier library...";
        RaiseCommands();

        var progress = new Progress<ScanProgressReport>(report =>
        {
            ProgressPercentage = report.Percentage;
            CurrentStatusText = $"Scanning: {report.DiscoveredArtists} artists, {report.DiscoveredReleases} releases, {report.DiscoveredModels} models ({report.DiscoveredFiles} files)";
        });

        try
        {
            var result = await _directoryScanner.BulkScanAsync(RootPath, progress, _scanCancellation.Token).ConfigureAwait(true);
            CurrentStatusText = $"Scan complete. Added {result.TotalArtists} Artists, {result.TotalReleases} Releases, {result.TotalModels} Models, {result.TotalFiles} Files.";
            ProgressPercentage = 100;
            await LoadCatalogAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            CurrentStatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            CurrentStatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
            RaiseCommands();
        }
    }

    public async Task BrowseFolderAsync()
    {
        try
        {
            var desktop = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var topLevel = TopLevel.GetTopLevel(desktop?.MainWindow);
            if (topLevel?.StorageProvider != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select 3D Printing Stash Root",
                    AllowMultiple = false
                }).ConfigureAwait(true);

                if (folders.Count > 0)
                {
                    var selectedPath = folders[0].Path.LocalPath;
                    ScanPath = selectedPath;
                }
            }
        }
        catch
        {
            // Non-fatal dialog fallback
        }
    }
}
