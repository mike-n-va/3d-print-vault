using System;
using System.IO;
using System.Text.Json;
using DPV.Domain.Contracts.Catalog;

namespace DPV.Infrastructure.Persistence;

public sealed class JsonUserPreferencesService : IUserPreferencesService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonUserPreferencesService(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "3DPrintVault");
            _filePath = Path.Combine(dir, "preferences.json");
        }
    }

    public UserPreferences LoadPreferences()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new UserPreferences();
            }

            var json = File.ReadAllText(_filePath);
            var preferences = JsonSerializer.Deserialize<UserPreferences>(json);
            return preferences ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    public void SavePreferences(UserPreferences preferences)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(preferences, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Non-critical persistence failure silently handled
        }
    }
}
