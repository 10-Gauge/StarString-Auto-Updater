using System.Drawing;
using System.Windows.Forms;

namespace StarStringsAutoUpdater.UI;

/// <summary>
/// A resizable Yes/No prompt that measures its own heading, subheading, and notes
/// text and sizes itself to fit without internal scrolling (falling back to the
/// notes box's own scrollbar only for unusually long text), clamped to the
/// screen's working area. Shared by the StarStrings update prompt and the app's
/// own self-update prompt so this sizing logic only needs to be right once.
/// </summary>
public sealed class ConfirmationPromptForm : Form
{
    private const int Margin = 16;
    private const int ContentWidth = 480;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;
    private const int ButtonSpacing = 10;
    private const int MinNotesHeight = 90;
    private const int MaxNotesHeight = 420;

    private readonly Font _headingFont;

    public ConfirmationPromptForm(
        string title,
        string heading,
        string subheading,
        string notesLabelText,
        string notesText,
        string primaryButtonText,
        string secondaryButtonText)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(420, 320);

        _headingFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);

        var headingLabel = new Label
        {
            Text = heading,
            Font = _headingFont,
            AutoSize = false,
            Location = new Point(Margin, Margin),
            Size = new Size(ContentWidth, MeasureTextHeight(heading, _headingFont, ContentWidth)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var subheadingLabel = new Label
        {
            Text = subheading,
            AutoSize = false,
            Location = new Point(Margin, headingLabel.Bottom + 10),
            Size = new Size(ContentWidth, MeasureTextHeight(subheading, Font, ContentWidth)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var notesLabel = new Label
        {
            Text = notesLabelText,
            AutoSize = false,
            Location = new Point(Margin, subheadingLabel.Bottom + 12),
            Size = new Size(ContentWidth, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        // +20 covers the TextBox's own internal padding/border so the measured text
        // itself doesn't need to scroll; MaxNotesHeight is a fallback cap for unusually
        // long text, in which case the box's own scrollbar takes over.
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
        var primaryButton = new Button
        {
            Text = primaryButtonText,
            DialogResult = DialogResult.Yes,
            Size = new Size(ButtonWidth, ButtonHeight),
            Location = new Point(Margin + ContentWidth - ButtonWidth - ButtonSpacing - (ButtonWidth - 10), buttonsTop),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        var secondaryButton = new Button
        {
            Text = secondaryButtonText,
            DialogResult = DialogResult.No,
            Size = new Size(ButtonWidth - 10, ButtonHeight),
            Location = new Point(primaryButton.Right + ButtonSpacing, buttonsTop),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        // Set the final size *before* adding the anchored controls: anchoring locks in
        // each control's distance from the right/bottom edges at the moment it's parented,
        // so adding them while the form is still at its small default size (then resizing)
        // would fling the Bottom/Right-anchored controls off the visible client area.
        ClientSize = new Size(Margin * 2 + ContentWidth, buttonsTop + ButtonHeight + Margin);

        Controls.AddRange([headingLabel, subheadingLabel, notesLabel, notesBox, primaryButton, secondaryButton]);
        AcceptButton = primaryButton;
        CancelButton = secondaryButton;
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

    /// <summary>Shows the prompt and returns true if the user chose the primary action.</summary>
    public static bool Ask(
        string title,
        string heading,
        string subheading,
        string notesLabelText,
        string notesText,
        string primaryButtonText,
        string secondaryButtonText)
    {
        using var form = new ConfirmationPromptForm(title, heading, subheading, notesLabelText, notesText, primaryButtonText, secondaryButtonText);
        return form.ShowDialog() == DialogResult.Yes;
    }
}
