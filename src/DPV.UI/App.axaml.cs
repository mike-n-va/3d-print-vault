using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DPV.UI;

public partial class App : Application
{
    private readonly MainWindow _mainWindow;

    public App(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    /// <summary>
    /// Avalonia designer entry point. Runtime startup uses DI via <see cref="DPV.App.Startup.AvaloniaApplicationStartup"/>.
    /// </summary>
    public App()
        : this(new MainWindow())
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
            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
