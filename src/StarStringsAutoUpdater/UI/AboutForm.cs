using System.Drawing;
using System.Windows.Forms;

namespace StarStringsAutoUpdater.UI;

/// <summary>Credits dialog, opened from the Control Panel's About button.</summary>
public sealed class AboutForm : Form
{
    private const int ContentWidth = 360;
    private const int Margin = 20;

    public AboutForm(string appVersionText)
    {
        Text = "About StarStrings Auto-Updater";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Background;
        Font = Theme.FontBody;
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = IconFactory.CreateTrayIcon();

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Background,
            Padding = new Padding(Margin),
        };

        root.Controls.Add(BuildHeader(appVersionText));

        root.Controls.Add(Theme.CreateLinkLine(
            ContentWidth,
            "Created by 10 Gauge of Jokers Gambit",
            ("10 Gauge", "https://robertsspaceindustries.com/en/citizens/10Gauge"),
            ("Jokers Gambit", "https://robertsspaceindustries.com/en/orgs/J0K3R5")));

        var creditLine = Theme.CreateLinkLine(
            ContentWidth,
            "Thank you MrKraken for your hard work and dedication to the StarStrings project. Sincerely, The Star Citizen Community.",
            ("MrKraken", "https://github.com/MrKraken"),
            ("StarStrings", "https://github.com/MrKraken/StarStrings"));
        creditLine.Margin = new Padding(0, 10, 0, 0);
        root.Controls.Add(creditLine);

        var closeButton = Theme.CreateButton("Close", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        closeButton.Margin = new Padding(0, 18, 0, 0);
        closeButton.Click += (_, _) => Close();
        root.Controls.Add(closeButton);

        CancelButton = closeButton;
        AcceptButton = closeButton;

        // Measure the fully-populated, fixed-width content first, then size the form to it,
        // and only then parent it - avoids the anchor-offset-lock trap of resizing after adding controls.
        root.PerformLayout();
        ClientSize = root.PreferredSize;
        Controls.Add(root);
    }

    private static Control BuildHeader(string appVersionText)
    {
        Bitmap headerBitmap;
        using (var headerIcon = IconFactory.CreateTrayIcon(size: 48))
        {
            headerBitmap = headerIcon.ToBitmap();
        }

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 16),
        };

        var pictureBox = new PictureBox
        {
            Image = headerBitmap,
            SizeMode = PictureBoxSizeMode.AutoSize,
            Margin = new Padding(0, 0, 12, 0),
        };

        var textColumn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        var title = new Label
        {
            Text = "StarStrings Auto-Updater",
            Font = Theme.FontHeading,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
        };

        var versionLabel = new Label
        {
            Text = appVersionText,
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };

        textColumn.Controls.Add(title);
        textColumn.Controls.Add(versionLabel);

        panel.Controls.Add(pictureBox);
        panel.Controls.Add(textColumn);
        return panel;
    }
}
