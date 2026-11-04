using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace DPV.UI;

public partial class App : Application
{
    private readonly IServiceProvider? _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Avalonia designer entry point.
    /// </summary>
    public App()
    {
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_serviceProvider is not null)
            {
                desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
