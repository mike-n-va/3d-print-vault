using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DPV.UI.ViewModels;

namespace DPV.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void ExportCsvClick(object? sender, RoutedEventArgs e) =>
        await SaveExportAsync("catalog.csv", "csv", "CSV", "Export catalog CSV", (viewModel, token) => viewModel.ExportToCsvAsync(token));

    private async void ExportJsonClick(object? sender, RoutedEventArgs e) =>
        await SaveExportAsync("catalog.json", "json", "JSON", "Export catalog JSON", (viewModel, token) => viewModel.ExportToJsonAsync(token));

    private async void VacuumClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.VacuumAsync();
        }
        catch (Exception)
        {
            // Status text already describes the failure.
        }
    }

    private async void BackupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Backup catalog database",
            SuggestedFileName = "3d-print-vault-backup.db",
            DefaultExtension = "db"
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await viewModel.BackupAsync(path);
        }
        catch (Exception)
        {
            // Status text already describes the failure.
        }
    }

    private async void RestoreClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Restore catalog database",
            AllowMultiple = false
        });
        var path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await viewModel.RestoreAsync(path);
        }
        catch (Exception)
        {
            // Status text already describes the failure.
        }
    }

    private async Task SaveExportAsync(
        string suggestedName,
        string extension,
        string typeName,
        string title,
        Func<SettingsViewModel, CancellationToken, Task<string>> export)
    {
        if (DataContext is not SettingsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = ["*." + extension] }]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var contents = await export(viewModel, CancellationToken.None);
            await File.WriteAllTextAsync(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception)
        {
            // Status text already describes the failure.
        }
    }
}
