using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DuplicatesViewModel _duplicatesViewModel;
    private readonly AnalyticsViewModel _analyticsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private object _currentViewModel;

    public ShellViewModel(
        DashboardViewModel dashboardViewModel,
        DuplicatesViewModel? duplicatesViewModel = null,
        AnalyticsViewModel? analyticsViewModel = null,
        SettingsViewModel? settingsViewModel = null)
    {
        _dashboardViewModel = dashboardViewModel;
        _duplicatesViewModel = duplicatesViewModel ?? new DuplicatesViewModel(new EmptyDuplicateRepository());
        _analyticsViewModel = analyticsViewModel ?? new AnalyticsViewModel(new EmptyStatsRepository());
        _settingsViewModel = settingsViewModel ?? new SettingsViewModel(new EmptyExportService(), new EmptyMaintenanceService());
        _currentViewModel = dashboardViewModel;
        _dashboardViewModel.ShowDuplicates = ShowDuplicates;
        ShowDashboardCommand = new DelegateCommand(() => CurrentViewModel = _dashboardViewModel);
        ShowDuplicatesCommand = new DelegateCommand(ShowDuplicates);
        ShowAnalyticsCommand = new DelegateCommand(ShowAnalytics);
        ShowSettingsCommand = new DelegateCommand(() => CurrentViewModel = _settingsViewModel);
    }

    public void ShowDuplicates()
    {
        CurrentViewModel = _duplicatesViewModel;
        _ = _duplicatesViewModel.LoadAsync();
    }

    public void ShowAnalytics()
    {
        CurrentViewModel = _analyticsViewModel;
        _ = _analyticsViewModel.LoadAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (ReferenceEquals(_currentViewModel, value))
            {
                return;
            }

            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowDuplicatesCommand { get; }

    public ICommand ShowAnalyticsCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }

    private sealed class EmptyDuplicateRepository : IDuplicateRepository
    {
        public Task<IReadOnlyCollection<DuplicateGroupDto>> GetDuplicateGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DuplicateGroupDto>>([]);

        public Task ResolveDuplicateMergeAsync(
            Guid primaryFileId,
            IEnumerable<Guid> duplicateFileIdsToDeactivate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateIgnoreAsync(string contentHash, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveDuplicateDeleteAsync(Guid fileIdToDelete, string filePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyStatsRepository : ILibraryStatsRepository
    {
        public Task<LibraryStatsDto> GetLibraryStatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryStatsDto(0, 0, 0, 0, 0, 0, [], [], []));
    }

    private sealed class EmptyExportService : ICatalogExportService
    {
        public Task<string> ExportToCsvAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("ModelName,FilePath,FileSizeBytes,Artist,Collection,LastModified" + Environment.NewLine);

        public Task<string> ExportToJsonAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("[]");
    }

    private sealed class EmptyMaintenanceService : IDbMaintenanceService
    {
        public Task VacuumDatabaseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BackupDatabaseAsync(string destinationFilePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RestoreDatabaseAsync(string sourceFilePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
