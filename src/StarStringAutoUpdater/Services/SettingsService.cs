using System.Text.Json;
using StarStringAutoUpdater.Models;

namespace StarStringAutoUpdater.Services;

/// <summary>Loads and saves AppSettings as JSON in %AppData%\StarStringAutoUpdater.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object _lock = new();

    public AppSettings Load()
    {
        AppPaths.EnsureFoldersExist();

        if (!File.Exists(AppPaths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            lock (_lock)
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // Corrupt or unreadable settings file: fall back to defaults rather than crash.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureFoldersExist();

        lock (_lock)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempFile = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, AppPaths.SettingsFile, overwrite: true);
        }
    }
}
