using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;

namespace DPV.UI.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ICatalogExportService _export;
    private readonly IDbMaintenanceService _maintenance;
    private string _statusText = "Export the active catalog, or maintain the SQLite database.";

    public SettingsViewModel(ICatalogExportService export, IDbMaintenanceService maintenance)
    {
        _export = export;
        _maintenance = maintenance;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Settings";

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public async Task<string> ExportToCsvAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(() => _export.ExportToCsvAsync(cancellationToken), "Catalog CSV is ready.").ConfigureAwait(true);

    public async Task<string> ExportToJsonAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(() => _export.ExportToJsonAsync(cancellationToken), "Catalog JSON is ready.").ConfigureAwait(true);

    public Task VacuumAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            await _maintenance.VacuumDatabaseAsync(cancellationToken).ConfigureAwait(true);
            return string.Empty;
        }, "Database vacuum completed.");

    public Task BackupAsync(string destinationFilePath, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            await _maintenance.BackupDatabaseAsync(destinationFilePath, cancellationToken).ConfigureAwait(true);
            return string.Empty;
        }, "Database backup saved.");

    public Task RestoreAsync(string sourceFilePath, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            await _maintenance.RestoreDatabaseAsync(sourceFilePath, cancellationToken).ConfigureAwait(true);
            return string.Empty;
        }, "Database restored from backup.");

    private async Task<string> RunAsync(Func<Task<string>> action, string success)
    {
        try
        {
            var result = await action().ConfigureAwait(true);
            StatusText = success;
            return result;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            throw;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}
