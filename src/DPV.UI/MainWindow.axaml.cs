using System;
using Avalonia;
using Avalonia.Controls;
using DPV.Domain.Contracts.Catalog;
using DPV.UI.ViewModels;

namespace DPV.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel, IUserPreferencesService? preferencesService = null)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (preferencesService is not null)
        {
            var prefs = preferencesService.LoadPreferences();
            if (prefs.WindowWidth >= MinWidth)
            {
                Width = prefs.WindowWidth;
            }

            if (prefs.WindowHeight >= MinHeight)
            {
                Height = prefs.WindowHeight;
            }

            if (prefs.WindowX.HasValue && prefs.WindowY.HasValue)
            {
                Position = new PixelPoint(prefs.WindowX.Value, prefs.WindowY.Value);
            }

            if (Enum.TryParse<WindowState>(prefs.WindowState, out var state))
            {
                WindowState = state;
            }

            Closing += (_, _) =>
            {
                try
                {
                    var current = preferencesService.LoadPreferences();
                    preferencesService.SavePreferences(current with
                    {
                        WindowWidth = Bounds.Width > 0 ? Bounds.Width : Width,
                        WindowHeight = Bounds.Height > 0 ? Bounds.Height : Height,
                        WindowX = Position.X,
                        WindowY = Position.Y,
                        WindowState = WindowState.ToString()
                    });
                }
                catch
                {
                    // Non-critical preferences persistence failure
                }
            };
        }
    }

    public MainWindow()
    {
        // Designer fallback only
    }
}
