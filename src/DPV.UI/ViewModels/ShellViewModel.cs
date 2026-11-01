using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DPV.UI.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly DashboardViewModel _dashboardViewModel;
    private object _currentViewModel;

    public ShellViewModel(DashboardViewModel dashboardViewModel)
    {
        _dashboardViewModel = dashboardViewModel;
        _currentViewModel = dashboardViewModel;
        ShowDashboardCommand = new DelegateCommand(() => CurrentViewModel = _dashboardViewModel);
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
}
