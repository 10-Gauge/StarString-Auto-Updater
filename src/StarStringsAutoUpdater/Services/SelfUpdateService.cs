using System.Reflection;
using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.Services;

public enum SelfUpdateOutcome
{
    UpToDate,
    Downloaded,
    Declined,
    Error,
}

/// <summary>
/// Checks GitHub for a newer release of this app itself (as opposed to
/// UpdateService, which checks for new StarStrings content) and, if the user
/// agrees, downloads it. This class never replaces the running executable or
/// exits the process itself - it hands the downloaded path to the caller via
/// <paramref name="onDownloadedAsync"/> (see CheckForUpdatesAsync), since only
/// the tray context knows how to shut itself down cleanly (stop the timer,
/// hide the tray icon, release the single-instance mutex).
/// </summary>
public sealed class SelfUpdateService : IDisposable
{
    private readonly GitHubReleaseClient _client = new();

    public static Version GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public async Task<SelfUpdateOutcome> CheckForUpdatesAsync(
        AppSettings settings,
        bool manualTrigger,
        Func<GitHubRelease, Version, Task<bool>> confirmDownloadAsync,
        Func<string, GitHubRelease, Task> onDownloadedAsync,
        Action<string, bool>? notify,
        CancellationToken ct)
    {
        GitHubRelease release;
        try
        {
            release = await _client.GetLatestReleaseAsync(GitHubReleaseClient.AppLatestReleaseUrl, ct);
        }
        catch (Exception ex)
        {
            Logger.Error($"App update check failed: {ex.Message}");
            if (manualTrigger)
            {
                notify?.Invoke("Couldn't reach GitHub to check for an app update. See log for details.", true);
            }
            return SelfUpdateOutcome.Error;
        }

        if (!TryParseVersion(release.TagName, out var latestVersion))
        {
            Logger.Warning($"App release '{release.TagName}' doesn't look like a version tag; skipping self-update check.");
            return SelfUpdateOutcome.Error;
        }

        var currentVersion = GetCurrentVersion();
        if (!IsNewer(latestVersion, currentVersion))
        {
            Logger.Info($"App is up to date (running {currentVersion}, latest release is {release.TagName}).");
            return SelfUpdateOutcome.UpToDate;
        }

        var previouslySeen = string.Equals(settings.LastDeclinedAppVersion, release.TagName, StringComparison.OrdinalIgnoreCase);
        if (!manualTrigger && previouslySeen)
        {
            Logger.Info($"Skipping automatic app-update prompt for '{release.TagName}': user already saw it.");
            return SelfUpdateOutcome.Declined;
        }

        var asset = GitHubReleaseClient.FindAppExeAsset(release);
        if (asset is null)
        {
            Logger.Error($"App release '{release.TagName}' has no recognizable single .exe asset.");
            notify?.Invoke("A new app version is available, but its download couldn't be found. See log for details.", true);
            return SelfUpdateOutcome.Error;
        }

        var accepted = await confirmDownloadAsync(release, currentVersion);
        if (!accepted)
        {
            settings.LastDeclinedAppVersion = release.TagName;
            Logger.Info($"User declined downloading app version: {release.TagName}");
            return SelfUpdateOutcome.Declined;
        }

        try
        {
            ClearUpdatesFolder();
            var destinationPath = Path.Combine(AppPaths.UpdatesFolder, asset.Name);
            await _client.DownloadFileAsync(asset.BrowserDownloadUrl, destinationPath, ct);

            // Whether or not the user restarts into it right away, they've now seen this
            // version - don't keep nagging the still-running old instance about it.
            settings.LastDeclinedAppVersion = release.TagName;
            Logger.Info($"Downloaded app update {release.TagName} to {destinationPath}");
            notify?.Invoke($"Downloaded StarStrings Auto-Updater {release.TagName}.", false);

            await onDownloadedAsync(destinationPath, release);
            return SelfUpdateOutcome.Downloaded;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to download app update: {ex.Message}");
            notify?.Invoke("Failed to download the app update. See log for details.", true);
            return SelfUpdateOutcome.Error;
        }
    }

    private static bool TryParseVersion(string tagName, out Version version) =>
        Version.TryParse(tagName.TrimStart('v', 'V'), out version!);

    /// <summary>Compares Major/Minor/Build only (ignores Revision), so a 3-part release
    /// tag like "1.1.0" compares correctly against the running assembly's 4-part
    /// AssemblyVersion (e.g. "1.1.0.0") without System.Version's -1-vs-0 revision quirk
    /// making equal versions look different.</summary>
    private static bool IsNewer(Version latest, Version current) =>
        latest.Major != current.Major ? latest.Major > current.Major :
        latest.Minor != current.Minor ? latest.Minor > current.Minor :
        Math.Max(latest.Build, 0) > Math.Max(current.Build, 0);

    private static void ClearUpdatesFolder()
    {
        AppPaths.EnsureFoldersExist();
        foreach (var file in Directory.EnumerateFiles(AppPaths.UpdatesFolder))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best effort; a leftover file from a previous download isn't harmful.
            }
        }
    }

    public void Dispose() => _client.Dispose();
}
