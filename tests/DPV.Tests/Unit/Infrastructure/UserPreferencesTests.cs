using System;
using System.IO;
using DPV.Domain.Contracts.Catalog;
using DPV.Infrastructure.Persistence;

namespace DPV.Tests.Unit.Infrastructure;

public class UserPreferencesTests : IDisposable
{
    private readonly string _tempFile;

    public UserPreferencesTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"dpv-prefs-test-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void LoadPreferences_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var service = new JsonUserPreferencesService(_tempFile);
        var prefs = service.LoadPreferences();

        Assert.NotNull(prefs);
        Assert.Equal(1100, prefs.WindowWidth);
        Assert.Equal(720, prefs.WindowHeight);
        Assert.Equal("Grid", prefs.ViewMode);
        Assert.Equal("Medium", prefs.GridDensity);
        Assert.Null(prefs.LastScanRoot);
    }

    [Fact]
    public void SaveAndLoadPreferences_RoundTripsSuccessfully()
    {
        var service = new JsonUserPreferencesService(_tempFile);
        var prefsToSave = new UserPreferences(
            WindowWidth: 1400,
            WindowHeight: 900,
            WindowX: 100,
            WindowY: 150,
            WindowState: "Maximized",
            ViewMode: "List",
            GridDensity: "Large",
            LastScanRoot: @"C:\CustomLibrary");

        service.SavePreferences(prefsToSave);
        var loaded = service.LoadPreferences();

        Assert.Equal(1400, loaded.WindowWidth);
        Assert.Equal(900, loaded.WindowHeight);
        Assert.Equal(100, loaded.WindowX);
        Assert.Equal(150, loaded.WindowY);
        Assert.Equal("Maximized", loaded.WindowState);
        Assert.Equal("List", loaded.ViewMode);
        Assert.Equal("Large", loaded.GridDensity);
        Assert.Equal(@"C:\CustomLibrary", loaded.LastScanRoot);
    }

    [Fact]
    public void LoadPreferences_WhenFileIsCorrupted_ReturnsDefaultsWithoutThrowing()
    {
        File.WriteAllText(_tempFile, "{ invalid json content !!!");
        var service = new JsonUserPreferencesService(_tempFile);
        var prefs = service.LoadPreferences();

        Assert.NotNull(prefs);
        Assert.Equal(1100, prefs.WindowWidth);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try
            {
                File.Delete(_tempFile);
            }
            catch
            {
            }
        }
    }
}
