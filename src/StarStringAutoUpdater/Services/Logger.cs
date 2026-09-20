namespace StarStringAutoUpdater.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Very small append-only file logger. Two separate log files are maintained:
/// the diagnostic log (every check, download, error) and the version-history
/// log (one line per version actually installed), since the app's job is
/// primarily to keep a legible trail of which StarStrings version is running.
/// </summary>
public static class Logger
{
    private static readonly object DiagLock = new();
    private static readonly object HistoryLock = new();
    private const long MaxDiagLogBytes = 2 * 1024 * 1024; // 2 MB, then rotate

    public static void Log(LogLevel level, string message)
    {
        AppPaths.EnsureFoldersExist();
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";

        lock (DiagLock)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(AppPaths.DiagnosticLogFile, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }

    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message) => Log(LogLevel.Error, message);

    /// <summary>Records that a specific StarStrings version was installed.</summary>
    public static void RecordVersionInstalled(string releaseName, DateTimeOffset publishedAt, string zipSha256)
    {
        AppPaths.EnsureFoldersExist();
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\t{releaseName}\tpublished={publishedAt:O}\tsha256={zipSha256}";

        lock (HistoryLock)
        {
            try
            {
                File.AppendAllText(AppPaths.VersionHistoryFile, line + Environment.NewLine);
            }
            catch
            {
                // Best effort.
            }
        }

        Info($"Installed version: {releaseName}");
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.DiagnosticLogFile))
        {
            return;
        }

        var info = new FileInfo(AppPaths.DiagnosticLogFile);
        if (info.Length < MaxDiagLogBytes)
        {
            return;
        }

        var archivePath = Path.Combine(
            AppPaths.LogsFolder,
            $"update.{DateTime.Now:yyyyMMddHHmmss}.log");

        File.Move(AppPaths.DiagnosticLogFile, archivePath, overwrite: true);
    }
}
