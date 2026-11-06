namespace DPV.Domain.Contracts.Catalog;

public sealed record UserPreferences(
    double WindowWidth = 1100,
    double WindowHeight = 720,
    int? WindowX = null,
    int? WindowY = null,
    string WindowState = "Normal",
    string ViewMode = "Grid",
    string GridDensity = "Medium",
    string? LastScanRoot = null);

public interface IUserPreferencesService
{
    UserPreferences LoadPreferences();

    void SavePreferences(UserPreferences preferences);
}
