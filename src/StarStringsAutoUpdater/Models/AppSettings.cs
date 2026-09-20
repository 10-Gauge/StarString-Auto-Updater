using System.Text.Json.Serialization;

namespace StarStringsAutoUpdater.Models;

public sealed class AppSettings
{
    /// <summary>Root of the user's Star Citizen LIVE install, e.g. ...\StarCitizen\LIVE</summary>
    public string? LiveFolderPath { get; set; }

    /// <summary>Whether the periodic background check loop is currently active.</summary>
    public bool AutoCheckEnabled { get; set; } = true;

    /// <summary>Whether the app registers itself to start with Windows.</summary>
    public bool StartWithWindows { get; set; } = true;

    public int CheckIntervalMinutes { get; set; } = 30;

    /// <summary>published_at of the release whose content we last installed.</summary>
    public DateTimeOffset? LastAppliedPublishedAt { get; set; }

    /// <summary>SHA-256 of the StarStrings-LIVE.zip we last installed from.</summary>
    public string? LastAppliedZipSha256 { get; set; }

    /// <summary>Human-readable release name we last installed, for display/logging.</summary>
    public string? LastAppliedReleaseName { get; set; }

    public DateTimeOffset? LastAppliedAtUtc { get; set; }

    /// <summary>published_at of a release the user was prompted about and declined, so we
    /// don't nag again on every automatic check for that same release.</summary>
    public DateTimeOffset? LastDeclinedPublishedAt { get; set; }

    public DateTimeOffset? LastCheckedAtUtc { get; set; }

    [JsonIgnore]
    public bool HasLiveFolder => !string.IsNullOrWhiteSpace(LiveFolderPath);

    /// <summary>Clears the "what's currently installed" bookkeeping. Call this whenever the
    /// global.ini on disk no longer matches what we last applied (e.g. after a manual
    /// restore-from-backup), so the next check treats any available release as new again.</summary>
    public void ResetInstalledVersionTracking()
    {
        LastAppliedPublishedAt = null;
        LastAppliedZipSha256 = null;
        LastAppliedReleaseName = null;
        LastAppliedAtUtc = null;
        LastDeclinedPublishedAt = null;
    }
}
