using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using StarStringsAutoUpdater.Models;
using StarStringsAutoUpdater.Services;

namespace StarStringsAutoUpdater.UI;

/// <summary>Callbacks the control panel invokes; the tray context owns all real state and logic.</summary>
public sealed record ControlPanelActions(
    Action CheckNow,
    Action ChangeLiveFolder,
    Action ToggleAutoCheck,
    Action<int> SetInterval,
    Action CustomInterval,
    Action RestoreBackup,
    Action OpenLogFolder,
    Action ToggleStartWithWindows,
    Action Exit);

/// <summary>A dark-themed dashboard exposing every tray action in one window, opened by double-clicking the tray icon.</summary>
public sealed class ControlPanelForm : Form
{
    private const int CardWidth = 420;
    private const int OuterMargin = 20;
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly (string Label, int Minutes)[] IntervalPresets =
    [
        ("Every 30 minutes", 30),
        ("Every 60 minutes", 60),
        ("Every 3 hours", 180),
        ("Every 12 hours", 720),
        ("Daily", 1440),
    ];

    private readonly ControlPanelActions _actions;

    private AppSettings _settings;
    private Label _statusValue = null!;
    private Label _folderValue = null!;
    private Label _installedValue = null!;
    private Label _scVersionValue = null!;
    private Label _lastCheckedValue = null!;
    private Button _toggleAutoCheckButton = null!;
    private Button _restoreBackupButton = null!;
    private CheckBox _startWithWindowsCheck = null!;
    private Label _currentIntervalValue = null!;
    private ComboBox _intervalCombo = null!;
    private bool _suppressIntervalEvent;

    public ControlPanelForm(AppSettings settings, string appVersionText, ControlPanelActions actions)
    {
        _settings = settings;
        _actions = actions;

        Text = "StarStrings Auto-Updater - Control Panel";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
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
            Padding = new Padding(OuterMargin),
        };

        root.Controls.Add(BuildHeader(appVersionText));
        root.Controls.Add(BuildStarStringsCard());
        root.Controls.Add(BuildScheduleCard());
        root.Controls.Add(BuildMaintenanceCard());
        root.Controls.Add(BuildFooter());

        // Measure the fully-populated, fixed-width content first, then size the form to it,
        // and only then parent it - avoids the anchor-offset-lock trap of resizing after adding controls.
        root.PerformLayout();
        ClientSize = root.PreferredSize;
        Controls.Add(root);

        RefreshFromSettings(settings);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            var enabled = 1;
            DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch
        {
            // Cosmetic only - older Windows builds simply keep the default title bar.
        }
    }

    private Control BuildHeader(string appVersionText)
    {
        Bitmap headerBitmap;
        using (var headerIcon = IconFactory.CreateTrayIcon(size: 64))
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

        _statusValue = new Label
        {
            Text = $"{appVersionText} - starting...",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };

        textColumn.Controls.Add(title);
        textColumn.Controls.Add(_statusValue);

        panel.Controls.Add(pictureBox);
        panel.Controls.Add(textColumn);

        return panel;
    }

    private Control BuildStarStringsCard()
    {
        var card = Theme.CreateCard(CardWidth);
        var contentWidth = CardWidth - card.Padding.Horizontal;

        card.Controls.Add(Theme.SectionHeading("StarStrings Installation Info"));

        card.Controls.Add(Theme.Caption("Star Citizen LIVE Folder"));
        _folderValue = Theme.Value("...", contentWidth);
        card.Controls.Add(_folderValue);

        card.Controls.Add(Theme.Caption("Installed Version"));
        _installedValue = Theme.Value("...", contentWidth);
        card.Controls.Add(_installedValue);

        card.Controls.Add(Theme.Caption("Built For Star Citizen Version"));
        _scVersionValue = Theme.Value("...", contentWidth);
        card.Controls.Add(_scVersionValue);

        card.Controls.Add(Theme.Caption("Last Checked"));
        _lastCheckedValue = Theme.Value("...", contentWidth);
        card.Controls.Add(_lastCheckedValue);

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 12, 0, 0),
        };

        var checkNowButton = Theme.CreateButton("Check Now", Theme.Accent, Theme.AccentHover);
        checkNowButton.Click += (_, _) => _actions.CheckNow();

        var changeFolderButton = Theme.CreateButton("Change Folder...", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        changeFolderButton.Click += (_, _) => _actions.ChangeLiveFolder();

        buttonRow.Controls.Add(checkNowButton);
        buttonRow.Controls.Add(changeFolderButton);
        card.Controls.Add(buttonRow);

        return card;
    }

    private Control BuildScheduleCard()
    {
        var card = Theme.CreateCard(CardWidth);
        var contentWidth = CardWidth - card.Padding.Horizontal;

        card.Controls.Add(Theme.SectionHeading("Check Schedule"));

        _toggleAutoCheckButton = Theme.CreateButton("Stop Auto-Check", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        _toggleAutoCheckButton.Margin = new Padding(0, 0, 0, 12);
        _toggleAutoCheckButton.Click += (_, _) => _actions.ToggleAutoCheck();
        card.Controls.Add(_toggleAutoCheckButton);

        card.Controls.Add(Theme.Caption("Current Interval"));
        _currentIntervalValue = Theme.Value("...", contentWidth);
        card.Controls.Add(_currentIntervalValue);

        card.Controls.Add(Theme.Caption("Change Interval"));

        _intervalCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.CardBackgroundAlt,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontBody,
            Width = contentWidth,
            Margin = new Padding(0, 4, 0, 0),
        };
        _intervalCombo.SelectedIndexChanged += OnIntervalComboChanged;
        card.Controls.Add(_intervalCombo);

        return card;
    }

    private Control BuildMaintenanceCard()
    {
        var card = Theme.CreateCard(CardWidth);

        card.Controls.Add(Theme.SectionHeading("Maintenance"));

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12),
        };

        _restoreBackupButton = Theme.CreateButton("Restore Backup", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        _restoreBackupButton.Click += (_, _) => _actions.RestoreBackup();

        var openLogsButton = Theme.CreateButton("Open Log Folder", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        openLogsButton.Click += (_, _) => _actions.OpenLogFolder();

        buttonRow.Controls.Add(_restoreBackupButton);
        buttonRow.Controls.Add(openLogsButton);
        card.Controls.Add(buttonRow);

        _startWithWindowsCheck = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            AutoCheck = false, // state is driven exclusively by RefreshFromSettings, matching the tray menu item's pattern
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.CardBackground,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _startWithWindowsCheck.Click += (_, _) => _actions.ToggleStartWithWindows();
        card.Controls.Add(_startWithWindowsCheck);

        return card;
    }

    private static string DescribeInterval(int minutes)
    {
        var preset = Array.Find(IntervalPresets, p => p.Minutes == minutes);
        return preset.Label ?? $"Custom ({TrayApplicationContext.FormatInterval(minutes)})";
    }

    /// <summary>About sits left-justified on the same row as Close/Exit App, which stay
    /// right-justified. FlowLayoutPanel doesn't support mixed alignment in one row on its
    /// own, so a computed blank spacer fills the gap between the two groups - simpler and
    /// safer here than Anchor (which locks its offset at parent-size-at-add-time) or a
    /// second nested layout container.</summary>
    private Control BuildFooter()
    {
        const int ButtonGap = 8;

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(CardWidth, 0),
            MaximumSize = new Size(CardWidth, 0),
            Margin = new Padding(0, 4, 0, 0),
        };

        var aboutButton = Theme.CreateButton("About", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        aboutButton.Margin = Padding.Empty;
        aboutButton.Click += (_, _) => new AboutForm().ShowDialog(this);

        var exitButton = Theme.CreateButton("Exit App", Theme.Danger, Theme.DangerHover);
        exitButton.Margin = Padding.Empty;
        exitButton.Click += OnExitClicked;

        var closeButton = Theme.CreateButton("Close", Theme.CardBackgroundAlt, Theme.CardBackgroundHover, Theme.TextPrimary);
        closeButton.Margin = Padding.Empty;
        closeButton.Click += (_, _) => Close();

        var usedWidth = aboutButton.PreferredSize.Width + exitButton.PreferredSize.Width + closeButton.PreferredSize.Width + ButtonGap;
        var spacer = new Panel { Width = Math.Max(0, CardWidth - usedWidth), Height = 1, Margin = Padding.Empty, BackColor = Theme.Background };
        var buttonGap = new Panel { Width = ButtonGap, Height = 1, Margin = Padding.Empty, BackColor = Theme.Background };

        row.Controls.Add(aboutButton);
        row.Controls.Add(spacer);
        row.Controls.Add(exitButton);
        row.Controls.Add(buttonGap);
        row.Controls.Add(closeButton);

        CancelButton = closeButton;
        AcceptButton = closeButton;

        return row;
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Exit StarStrings Auto-Updater completely? Automatic checking will stop until you run it again.",
            "Exit StarStrings Auto-Updater", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            _actions.Exit();
        }
    }

    private void OnIntervalComboChanged(object? sender, EventArgs e)
    {
        if (_suppressIntervalEvent)
        {
            return;
        }

        var index = _intervalCombo.SelectedIndex;
        if (index >= 0 && index < IntervalPresets.Length)
        {
            _actions.SetInterval(IntervalPresets[index].Minutes);
        }
        else
        {
            _actions.CustomInterval();
            // A cancelled custom-interval dialog doesn't route back through RefreshFromSettings,
            // so re-sync here unconditionally using the (possibly unchanged) live settings value.
            RefreshIntervalCombo(_settings.CheckIntervalMinutes);
        }
    }

    /// <summary>Called right when a check starts, for immediate feedback before the eventual RefreshFromSettings.</summary>
    public void SetChecking()
    {
        if (_statusValue.IsDisposed)
        {
            return;
        }

        _statusValue.Text = "Checking for updates...";
        _statusValue.ForeColor = Theme.TextSecondary;
    }

    public void RefreshFromSettings(AppSettings settings)
    {
        _settings = settings;

        _statusValue.Text = settings.AutoCheckEnabled
            ? "Running - checking automatically"
            : "Paused - automatic checking stopped";
        _statusValue.ForeColor = settings.AutoCheckEnabled ? Theme.Success : Theme.TextSecondary;

        _folderValue.Text = settings.HasLiveFolder ? settings.LiveFolderPath! : "Not set - click Change Folder to choose one";
        _installedValue.Text = settings.LastAppliedReleaseName ?? "None installed yet";
        _scVersionValue.Text = settings.LastAppliedScVersion ?? "Unknown";
        _lastCheckedValue.Text = settings.LastCheckedAtUtc?.ToLocalTime().ToString("f") ?? "Never";

        _toggleAutoCheckButton.Text = settings.AutoCheckEnabled ? "Stop Auto-Check" : "Start Auto-Check";
        _restoreBackupButton.Enabled = settings.HasLiveFolder && ZipInstaller.BackupExists(settings.LiveFolderPath!);
        _startWithWindowsCheck.Checked = settings.StartWithWindows;
        _currentIntervalValue.Text = DescribeInterval(settings.CheckIntervalMinutes);

        RefreshIntervalCombo(settings.CheckIntervalMinutes);
    }

    private void RefreshIntervalCombo(int minutes)
    {
        _suppressIntervalEvent = true;
        try
        {
            _intervalCombo.Items.Clear();
            foreach (var preset in IntervalPresets)
            {
                _intervalCombo.Items.Add(preset.Label);
            }

            var presetIndex = Array.FindIndex(IntervalPresets, p => p.Minutes == minutes);
            if (presetIndex >= 0)
            {
                _intervalCombo.Items.Add("Custom...");
                _intervalCombo.SelectedIndex = presetIndex;
            }
            else
            {
                _intervalCombo.Items.Add($"Custom... ({TrayApplicationContext.FormatInterval(minutes)})");
                _intervalCombo.SelectedIndex = IntervalPresets.Length;
            }
        }
        finally
        {
            _suppressIntervalEvent = false;
        }
    }
}
