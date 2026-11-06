using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Tests.Support;
using DPV.UI.ViewModels;
using Xunit;

namespace DPV.Tests.Integration.Ui;

public class ApplicationWindowVerificationTests
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [Fact]
    public void ProcessStartup_CreatesSingleMainWindow_AndRemainsResponsive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        var exePath = Path.Combine(
            TestRepository.Root,
            "src",
            "DPV.App",
            "bin",
            configuration,
            "net10.0",
            "DPV.App.exe");

        if (!File.Exists(exePath))
        {
            // Build artifact not found; fallback to debug
            exePath = Path.Combine(TestRepository.Root, "src", "DPV.App", "bin", "Debug", "net10.0", "DPV.App.exe");
        }

        Assert.True(File.Exists(exePath), $"Executable not found at: {exePath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false
        };

        using var proc = Process.Start(startInfo);
        Assert.NotNull(proc);

        try
        {
            // Poll for window appearance up to 12 seconds
            var stopwatch = Stopwatch.StartNew();
            var visibleAppWindows = new List<(IntPtr Hwnd, string ClassName, string Title)>();
            var hasConsoleWindow = false;

            while (stopwatch.ElapsedMilliseconds < 12000)
            {
                proc.Refresh();
                if (proc.HasExited)
                {
                    break;
                }

                visibleAppWindows.Clear();
                hasConsoleWindow = false;

                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out var pid);
                    if (pid == proc.Id && IsWindowVisible(hWnd))
                    {
                        var sbTitle = new StringBuilder(256);
                        GetWindowText(hWnd, sbTitle, 256);
                        var sbClass = new StringBuilder(256);
                        GetClassName(hWnd, sbClass, 256);

                        var cls = sbClass.ToString();
                        var title = sbTitle.ToString();

                        // Filter out Windows OS IME and touch input overlay windows that Windows creates for all GUI threads
                        if (cls.Contains("IME") || cls.Contains("Cicero") || cls.Contains("UAC") || cls.Contains("InputIndicator") || cls.Contains("Touch"))
                        {
                            return true;
                        }

                        if (cls == "ConsoleWindowClass")
                        {
                            hasConsoleWindow = true;
                        }

                        visibleAppWindows.Add((hWnd, cls, title));
                    }
                    return true;
                }, IntPtr.Zero);

                if (visibleAppWindows.Count > 0)
                {
                    break;
                }

                Thread.Sleep(250);
            }

            proc.Refresh();
            Assert.False(proc.HasExited, "Application process exited prematurely during startup.");
            Assert.True(proc.Responding, "Application process entered 'Not Responding' state.");
            Assert.False(hasConsoleWindow, "A black ConsoleWindowClass window was created, which is prohibited for a desktop WinExe application.");
            Assert.Single(visibleAppWindows);
            Assert.Contains("3D Print Vault", visibleAppWindows[0].Title);

            // Wait additional duration to verify application remains responsive over time
            Thread.Sleep(2000);
            proc.Refresh();
            Assert.True(proc.Responding, "Application froze after startup.");
        }
        finally
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ShellNavigation_SwapsViewModelsInPlace_WithoutCreatingSecondaryWindows()
    {
        var import = new FakeImport();
        var catalog = new FakeCatalog();
        var dashboard = new DashboardViewModel(import, catalog);
        var shell = new ShellViewModel(dashboard);

        // Verify initial state
        Assert.Same(dashboard, shell.CurrentViewModel);

        // Test navigation to Artists
        shell.ShowArtistsCommand.Execute(null);
        Assert.IsType<ArtistBrowserViewModel>(shell.CurrentViewModel);

        // Test navigation to Collections
        shell.ShowCollectionsCommand.Execute(null);
        Assert.IsType<CollectionBrowserViewModel>(shell.CurrentViewModel);

        // Test navigation to Tags
        shell.ShowTagsCommand.Execute(null);
        Assert.IsType<TagBrowserViewModel>(shell.CurrentViewModel);

        // Test navigation to Favorites
        shell.ShowFavoritesCommand.Execute(null);
        Assert.Same(dashboard, shell.CurrentViewModel);
        Assert.True(dashboard.OnlyFavoritesFilter);

        // Test navigation to Library/Dashboard
        shell.ShowDashboardCommand.Execute(null);
        Assert.Same(dashboard, shell.CurrentViewModel);
        Assert.False(dashboard.OnlyFavoritesFilter);

        // Test navigation to Settings
        shell.ShowSettingsCommand.Execute(null);
        Assert.IsType<SettingsViewModel>(shell.CurrentViewModel);

        // Test navigation to Duplicates
        shell.ShowDuplicatesCommand.Execute(null);
        Assert.IsType<DuplicatesViewModel>(shell.CurrentViewModel);

        // Test navigation to Analytics
        shell.ShowAnalyticsCommand.Execute(null);
        Assert.IsType<AnalyticsViewModel>(shell.CurrentViewModel);
    }

    private sealed class FakeImport : IModelImportService
    {
        public Task<ImportLibraryResult> ImportAsync(ImportLibraryRequest request, CancellationToken cancellationToken = default, IProgress<ImportProgress>? progress = null) =>
            Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
    }

    private sealed class FakeCatalog : DPV.Domain.Contracts.Catalog.IModelCatalogQuery
    {
        public Task<IReadOnlyCollection<ModelSummaryDto>> SearchModelsAsync(ModelQueryParameters parameters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ModelSummaryDto>>([]);
    }
}
