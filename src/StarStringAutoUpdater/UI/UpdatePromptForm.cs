using System.Drawing;
using System.Windows.Forms;
using StarStringAutoUpdater.Models;

namespace StarStringAutoUpdater.UI;

/// <summary>Modal "a new StarStrings version is available" prompt.</summary>
public sealed class UpdatePromptForm : Form
{
    public UpdatePromptForm(GitHubRelease release)
    {
        Text = "StarString Auto-Updater";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(440, 260);
        AutoScaleMode = AutoScaleMode.Dpi;

        var heading = new Label
        {
            Text = "A new StarStrings version is available",
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(408, 24),
        };

        var subheading = new Label
        {
            Text = $"{release.Name}\nPublished {release.PublishedAt.ToLocalTime():f}",
            AutoSize = false,
            Location = new Point(16, 46),
            Size = new Size(408, 40),
        };

        var notesLabel = new Label
        {
            Text = "Release notes:",
            AutoSize = false,
            Location = new Point(16, 92),
            Size = new Size(408, 18),
        };

        var notesBox = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(release.Body) ? "(no release notes provided)" : release.Body,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(16, 112),
            Size = new Size(408, 90),
        };

        var installButton = new Button
        {
            Text = "Install Now",
            DialogResult = DialogResult.Yes,
            Location = new Point(248, 214),
            Size = new Size(90, 28),
        };

        var skipButton = new Button
        {
            Text = "Skip",
            DialogResult = DialogResult.No,
            Location = new Point(344, 214),
            Size = new Size(80, 28),
        };

        Controls.AddRange([heading, subheading, notesLabel, notesBox, installButton, skipButton]);
        AcceptButton = installButton;
        CancelButton = skipButton;
    }

    /// <summary>Shows the prompt and returns true if the user chose to install.</summary>
    public static bool AskUserToInstall(GitHubRelease release)
    {
        using var form = new UpdatePromptForm(release);
        return form.ShowDialog() == DialogResult.Yes;
    }
}
