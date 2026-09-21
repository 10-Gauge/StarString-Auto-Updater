using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.UI;

/// <summary>"A new StarStrings version is available" prompt.</summary>
public static class UpdatePromptForm
{
    /// <summary>Shows the prompt and returns true if the user chose to install.</summary>
    public static bool AskUserToInstall(GitHubRelease release) =>
        ConfirmationPromptForm.Ask(
            title: "StarStrings Auto-Updater",
            heading: "A new StarStrings version is available",
            subheading: $"{release.Name}\nPublished {release.PublishedAt.ToLocalTime():f}",
            notesLabelText: "Release notes:",
            notesText: string.IsNullOrWhiteSpace(release.Body) ? "(no release notes provided)" : release.Body,
            primaryButtonText: "Install Now",
            secondaryButtonText: "Skip");
}
