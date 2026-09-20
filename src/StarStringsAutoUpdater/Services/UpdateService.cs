using System.Security.Cryptography;
using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.Services;

public enum CheckOutcome
{
    UpToDate,
    Installed,
    Declined,
    NeedsLiveFolder,
    Error,
}

/// <summary>
/// Orchestrates a single "check for updates" pass: ask GitHub for the current
/// "latest" StarStrings release, decide (via timestamp + zip hash) whether it's
/// actually new, optionally prompt the user, and install it.
///
/// This class deliberately does not touch WinForms UI directly - it calls back
/// into caller-supplied delegates for prompting/notifying, which the tray
/// context wires up. All async methods here avoid ConfigureAwait(false) so
/// that when invoked from the UI thread (which installs a
/// WindowsFormsSynchronizationContext), continuations - including any UI shown
/// by the callbacks - resume back on the UI thread.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly GitHubReleaseClient _client = new();

    public async Task<CheckOutcome> CheckForUpdatesAsync(
        AppSettings settings,
        bool manualTrigger,
        Func<GitHubRelease, Task<bool>> confirmInstallAsync,
        Action<string, bool>? notify,
        CancellationToken ct)
    {
        Logger.Info(manualTrigger ? "Manual update check started." : "Automatic update check started.");
        settings.LastCheckedAtUtc = DateTimeOffset.UtcNow;

        GitHubRelease release;
        try
        {
            release = await _client.GetLatestReleaseAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.Error($"Update check failed: {ex.Message}");
            notify?.Invoke("Couldn't reach GitHub to check for a StarStrings update. See log for details.", true);
            return CheckOutcome.Error;
        }

        var asset = GitHubReleaseClient.FindLiveZipAsset(release);
        if (asset is null)
        {
            Logger.Error($"Release '{release.Name}' has no recognizable StarStrings-LIVE.zip asset.");
            notify?.Invoke("The StarStrings release didn't contain the expected zip file. See log for details.", true);
            return CheckOutcome.Error;
        }

        if (settings.LastAppliedPublishedAt.HasValue && settings.LastAppliedPublishedAt.Value == release.PublishedAt)
        {
            Logger.Info("No update: release metadata unchanged since last applied version.");
            return CheckOutcome.UpToDate;
        }

        var tempZipPath = Path.Combine(AppPaths.DownloadCacheFolder, $"download-{Guid.NewGuid():N}.zip");
        try
        {
            await _client.DownloadFileAsync(asset.BrowserDownloadUrl, tempZipPath, ct);
            var hash = await Task.Run(() => ComputeSha256(tempZipPath), ct);

            if (!string.IsNullOrEmpty(settings.LastAppliedZipSha256) && settings.LastAppliedZipSha256 == hash)
            {
                // The "latest" release was republished (new timestamp/name) but the
                // actual StarStrings content didn't change. Track the new metadata so
                // we don't re-download every check, but there's nothing to install.
                settings.LastAppliedPublishedAt = release.PublishedAt;
                settings.LastAppliedReleaseName = release.Name;
                Logger.Info($"Release '{release.Name}' republished with identical content; nothing to install.");
                return CheckOutcome.UpToDate;
            }

            if (!settings.HasLiveFolder)
            {
                Logger.Warning($"New StarStrings version available ('{release.Name}') but no LIVE folder is configured.");
                notify?.Invoke(
                    "A new StarStrings version is available, but your Star Citizen LIVE folder isn't set yet. " +
                    "Right-click the tray icon to choose it.", true);
                return CheckOutcome.NeedsLiveFolder;
            }

            var previouslyDeclined = settings.LastDeclinedPublishedAt == release.PublishedAt;
            if (!manualTrigger && previouslyDeclined)
            {
                Logger.Info($"Skipping automatic prompt for '{release.Name}': user already declined it.");
                return CheckOutcome.Declined;
            }

            var accepted = await confirmInstallAsync(release);
            if (!accepted)
            {
                settings.LastDeclinedPublishedAt = release.PublishedAt;
                Logger.Info($"User declined installing version: {release.Name}");
                return CheckOutcome.Declined;
            }

            var result = await Task.Run(() => ZipInstaller.Install(tempZipPath, settings.LiveFolderPath!), ct);

            settings.LastAppliedPublishedAt = release.PublishedAt;
            settings.LastAppliedZipSha256 = hash;
            settings.LastAppliedReleaseName = release.Name;
            settings.LastAppliedAtUtc = DateTimeOffset.UtcNow;
            settings.LastDeclinedPublishedAt = null;

            Logger.RecordVersionInstalled(release.Name, release.PublishedAt, hash);
            if (result.GlobalIniBackedUp)
            {
                Logger.Info($"Previous global.ini backed up to {result.GlobalIniPath}.bak");
            }
            if (result.UserCfgWritten)
            {
                Logger.Info("No existing user.cfg found; installed the one bundled with StarStrings.");
            }

            notify?.Invoke($"Installed StarStrings: {release.Name}", false);
            return CheckOutcome.Installed;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to install update: {ex.Message}");
            notify?.Invoke("Failed to install the StarStrings update. See log for details.", true);
            return CheckOutcome.Error;
        }
        finally
        {
            TryDelete(tempZipPath);
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup; stale cache files aren't harmful.
        }
    }

    public void Dispose() => _client.Dispose();
}
