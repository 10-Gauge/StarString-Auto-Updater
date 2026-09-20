using System.Drawing;
using System.Windows.Forms;
using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.UI;

/// <summary>Modal "a new StarStrings version is available" prompt.</summary>
public sealed class UpdatePromptForm : Form
{
    private const int Margin = 16;
    private const int ContentWidth = 480;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;
    private const int ButtonSpacing = 10;
    private const int MinNotesHeight = 90;
    private const int MaxNotesHeight = 420;

    private readonly Font _headingFont;

    public UpdatePromptForm(GitHubRelease release)
    {
        Text = "StarStrings Auto-Updater";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(420, 320);

        _headingFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);

        const string headingText = "A new StarStrings version is available";
        var heading = new Label
        {
            Text = headingText,
            Font = _headingFont,
            AutoSize = false,
            Location = new Point(Margin, Margin),
            Size = new Size(ContentWidth, MeasureTextHeight(headingText, _headingFont, ContentWidth)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var subheadingText = $"{release.Name}\nPublished {release.PublishedAt.ToLocalTime():f}";
        var subheading = new Label
        {
            Text = subheadingText,
            AutoSize = false,
            Location = new Point(Margin, heading.Bottom + 10),
            Size = new Size(ContentWidth, MeasureTextHeight(subheadingText, Font, ContentWidth)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var notesLabel = new Label
        {
            Text = "Release notes:",
            AutoSize = false,
            Location = new Point(Margin, subheading.Bottom + 12),
            Size = new Size(ContentWidth, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var notesText = string.IsNullOrWhiteSpace(release.Body) ? "(no release notes provided)" : release.Body;
        // +20 covers the TextBox's own internal padding/border so the measured text
        // itself doesn't need to scroll; MaxNotesHeight is a fallback cap for unusually
        // long release notes, in which case the box's own scrollbar takes over.
        var neededNotesHeight = MeasureTextHeight(notesText, Font, ContentWidth - 12) + 20;
        var notesHeight = Math.Clamp(neededNotesHeight, MinNotesHeight, MaxNotesHeight);

        var notesBox = new TextBox
        {
            Text = notesText,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(Margin, notesLabel.Bottom + 4),
            Size = new Size(ContentWidth, notesHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        // Shrink the notes box (never below its minimum) if the natural layout would
        // make the dialog taller than the screen it's opening on.
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var maxClientHeight = (int)(workingArea.Height * 0.85);
        var naturalClientHeight = notesBox.Bottom + Margin + ButtonHeight + Margin;
        if (naturalClientHeight > maxClientHeight)
        {
            var overflow = naturalClientHeight - maxClientHeight;
            notesBox.Height = Math.Max(MinNotesHeight, notesBox.Height - overflow);
        }

        var buttonsTop = notesBox.Bottom + Margin;
        var installButton = new Button
        {
            Text = "Install Now",
            DialogResult = DialogResult.Yes,
            Size = new Size(ButtonWidth, ButtonHeight),
            Location = new Point(Margin + ContentWidth - ButtonWidth - ButtonSpacing - (ButtonWidth - 10), buttonsTop),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        var skipButton = new Button
        {
            Text = "Skip",
            DialogResult = DialogResult.No,
            Size = new Size(ButtonWidth - 10, ButtonHeight),
            Location = new Point(installButton.Right + ButtonSpacing, buttonsTop),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        Controls.AddRange([heading, subheading, notesLabel, notesBox, installButton, skipButton]);
        AcceptButton = installButton;
        CancelButton = skipButton;

        ClientSize = new Size(Margin * 2 + ContentWidth, buttonsTop + ButtonHeight + Margin);
    }

    private static int MeasureTextHeight(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headingFont.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Shows the prompt and returns true if the user chose to install.</summary>
    public static bool AskUserToInstall(GitHubRelease release)
    {
        using var form = new UpdatePromptForm(release);
        return form.ShowDialog() == DialogResult.Yes;
    }
}
