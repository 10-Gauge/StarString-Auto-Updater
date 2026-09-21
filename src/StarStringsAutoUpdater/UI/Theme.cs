using System.Drawing;
using System.Windows.Forms;

namespace StarStringsAutoUpdater.UI;

/// <summary>Shared colors, fonts, and control factory helpers for the dark-themed control panel.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(24, 24, 33);
    public static readonly Color CardBackground = Color.FromArgb(34, 34, 46);
    public static readonly Color CardBackgroundAlt = Color.FromArgb(46, 46, 62);
    public static readonly Color CardBackgroundHover = Color.FromArgb(58, 58, 76);
    public static readonly Color Accent = Color.FromArgb(255, 159, 67);
    public static readonly Color AccentHover = Color.FromArgb(255, 178, 102);
    public static readonly Color Danger = Color.FromArgb(200, 70, 70);
    public static readonly Color DangerHover = Color.FromArgb(220, 90, 90);
    public static readonly Color TextPrimary = Color.FromArgb(240, 240, 245);
    public static readonly Color TextSecondary = Color.FromArgb(170, 170, 185);
    public static readonly Color Success = Color.FromArgb(110, 200, 130);

    public static readonly Font FontHeading = new("Segoe UI Semibold", 14f);
    public static readonly Font FontSectionHeading = new("Segoe UI Semibold", 10f);
    public static readonly Font FontBody = new("Segoe UI", 9.5f);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);

    /// <summary>
    /// A fixed-width, height-autosized "card" container. Locking width via Min/MaxSize (instead of an
    /// anchored Location/Size guess) lets FlowLayoutPanel grow the height to fit content reliably.
    /// </summary>
    public static FlowLayoutPanel CreateCard(int width)
    {
        return new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = CardBackground,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 16),
            MinimumSize = new Size(width, 0),
            MaximumSize = new Size(width, 0),
        };
    }

    public static Label SectionHeading(string text) => new()
    {
        Text = text,
        Font = FontSectionHeading,
        ForeColor = Accent,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 10),
    };

    public static Label Caption(string text) => new()
    {
        Text = text,
        Font = FontSmall,
        ForeColor = TextSecondary,
        AutoSize = true,
        Margin = new Padding(0, 6, 0, 0),
    };

    /// <summary>A value label that wraps at maxWidth instead of guessing it'll always fit on one line.</summary>
    public static Label Value(string text, int maxWidth) => new()
    {
        Text = text,
        Font = FontBody,
        ForeColor = TextPrimary,
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Margin = new Padding(0, 1, 0, 0),
    };

    public static Button CreateButton(string text, Color backColor, Color hoverColor, Color? foreColor = null)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor ?? Color.White,
            Font = FontBody,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 6, 14, 6),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = hoverColor;
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(hoverColor, 0.1f);
        return button;
    }
}
