using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DPV.UI.ViewModels;

namespace DPV.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        AttachSplitterCursor();
    }

    private void AttachSplitterCursor()
    {
        try
        {
            var splitter = this.FindControl<GridSplitter>("CatalogGridSplitter");
            if (splitter is not null)
            {
                splitter.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeWestEast);
            }
        }
        catch (InvalidOperationException)
        {
            // Suppressed in headless testing environments where ICursorFactory is uninitialized
        }
    }

    private async void BrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not DashboardViewModel viewModel)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select a library folder"
        });
        if (folders.Count == 0)
        {
            return;
        }

        viewModel.RootPath = folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
    }
}
