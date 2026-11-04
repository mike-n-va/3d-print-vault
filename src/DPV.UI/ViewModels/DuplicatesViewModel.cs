using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class DuplicatesViewModel : INotifyPropertyChanged
{
    private readonly IDuplicateRepository _duplicates;
    private DuplicateGroupDto? _selectedGroup;
    private DuplicateFileItem? _selectedFile;
    private string _statusText = "Duplicate groups are exact SHA-256 matches.";

    public DuplicatesViewModel(IDuplicateRepository duplicates)
    {
        _duplicates = duplicates;
        Groups = new ObservableCollection<DuplicateGroupDto>();
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
        MergeCommand = new RelayCommand(
            () => _ = MergeAsync(),
            () => SelectedGroup is not null && SelectedFile is not null);
        IgnoreCommand = new RelayCommand(() => _ = IgnoreAsync(), () => SelectedGroup is not null);
        DeleteCommand = new RelayCommand(
            () => _ = DeleteAsync(),
            () => SelectedGroup is not null && SelectedFile is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Duplicate resolution";

    public ObservableCollection<DuplicateGroupDto> Groups { get; }

    public IReadOnlyList<DuplicateFileItem> SelectedFiles => SelectedGroup?.Files.ToList() ?? [];

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public DuplicateGroupDto? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (!SetField(ref _selectedGroup, value))
            {
                return;
            }

            SelectedFile = value?.Files.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedFiles));
            RaiseCommands();
        }
    }

    public DuplicateFileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetField(ref _selectedFile, value))
            {
                RaiseCommands();
            }
        }
    }

    public ICommand RefreshCommand { get; }

    public ICommand MergeCommand { get; }

    public ICommand IgnoreCommand { get; }

    public ICommand DeleteCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var selectedHash = SelectedGroup?.ContentHash;
        var groups = await _duplicates.GetDuplicateGroupsAsync(cancellationToken).ConfigureAwait(true);
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        SelectedGroup = selectedHash is null
            ? null
            : Groups.FirstOrDefault(group => string.Equals(group.ContentHash, selectedHash, StringComparison.Ordinal));
        StatusText = Groups.Count == 0
            ? "No active duplicate groups."
            : $"{Groups.Count} duplicate group{(Groups.Count == 1 ? "" : "s")}.";
    }

    private async Task MergeAsync()
    {
        if (SelectedGroup is null || SelectedFile is null)
        {
            return;
        }

        try
        {
            var redundant = SelectedGroup.Files
                .Select(file => file.FileId)
                .Where(fileId => fileId != SelectedFile.FileId);
            await _duplicates.ResolveDuplicateMergeAsync(SelectedFile.FileId, redundant)
                .ConfigureAwait(true);
            StatusText = "Kept the selected file and marked the other copies inactive.";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private async Task IgnoreAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        try
        {
            await _duplicates.ResolveDuplicateIgnoreAsync(SelectedGroup.ContentHash).ConfigureAwait(true);
            StatusText = "Ignored this hash. The files stay in the library.";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedGroup is null || SelectedFile is null)
        {
            return;
        }

        try
        {
            await _duplicates.ResolveDuplicateDeleteAsync(SelectedFile.FileId, SelectedFile.FilePath)
                .ConfigureAwait(true);
            StatusText = "Deleted the selected file from disk and marked it inactive.";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private void RaiseCommands()
    {
        if (MergeCommand is RelayCommand merge)
        {
            merge.RaiseCanExecuteChanged();
        }

        if (IgnoreCommand is RelayCommand ignore)
        {
            ignore.RaiseCanExecuteChanged();
        }

        if (DeleteCommand is RelayCommand delete)
        {
            delete.RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName ?? string.Empty);
        return true;
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
