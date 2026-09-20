namespace StarStringsAutoUpdater.Services;

/// <summary>Central home for everything the app persists, under %AppData%.</summary>
public static class AppPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StarStringsAutoUpdater");

    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");

    public static string LogsFolder => Path.Combine(RootFolder, "logs");

    public static string DiagnosticLogFile => Path.Combine(LogsFolder, "update.log");

    public static string VersionHistoryFile => Path.Combine(LogsFolder, "version-history.log");

    public static string DownloadCacheFolder => Path.Combine(RootFolder, "cache");

    public static void EnsureFoldersExist()
    {
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(DownloadCacheFolder);
    }
}
