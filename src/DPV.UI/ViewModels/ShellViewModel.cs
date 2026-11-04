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
    private object _currentViewModel;

    public ShellViewModel(DashboardViewModel dashboardViewModel, DuplicatesViewModel? duplicatesViewModel = null)
    {
        _dashboardViewModel = dashboardViewModel;
        _duplicatesViewModel = duplicatesViewModel ?? new DuplicatesViewModel(new EmptyDuplicateRepository());
        _currentViewModel = dashboardViewModel;
        _dashboardViewModel.ShowDuplicates = ShowDuplicates;
        ShowDashboardCommand = new DelegateCommand(() => CurrentViewModel = _dashboardViewModel);
        ShowDuplicatesCommand = new DelegateCommand(ShowDuplicates);
    }

    public void ShowDuplicates()
    {
        CurrentViewModel = _duplicatesViewModel;
        _ = _duplicatesViewModel.LoadAsync();
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
}
