using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.UI;

/// <summary>"A new version of StarStrings Auto-Updater itself is available" prompt.</summary>
public static class AppUpdatePromptForm
{
    /// <summary>Shows the prompt and returns true if the user chose to download it.</summary>
    public static bool AskUserToDownload(GitHubRelease release, Version currentVersion) =>
        ConfirmationPromptForm.Ask(
            title: "StarStrings Auto-Updater",
            heading: "A new version of StarStrings Auto-Updater is available",
            subheading: $"{release.TagName} (currently running v{currentVersion.Major}.{currentVersion.Minor}.{Math.Max(currentVersion.Build, 0)})\n" +
                        $"Published {release.PublishedAt.ToLocalTime():f}",
            notesLabelText: "Release notes:",
            notesText: string.IsNullOrWhiteSpace(release.Body) ? "(no release notes provided)" : release.Body,
            primaryButtonText: "Download",
            secondaryButtonText: "Skip");
}
