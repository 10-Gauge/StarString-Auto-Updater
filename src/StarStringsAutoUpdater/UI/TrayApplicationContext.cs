using System.Windows.Forms;
using StarStringsAutoUpdater.Models;
using StarStringsAutoUpdater.Services;

namespace StarStringsAutoUpdater.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsService _settingsService = new();
    private readonly UpdateService _updateService = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer;

    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _toggleAutoCheckItem = null!;
    private ToolStripMenuItem _checkNowItem = null!;
    private ToolStripMenuItem _startWithWindowsItem = null!;

    private AppSettings _settings;
    private bool _checkInProgress;

    public TrayApplicationContext()
    {
        _settings = _settingsService.Load();

        // Reconcile the registry Run key with the saved setting on every launch,
        // in case the exe was moved/renamed since it was last registered.
        StartupManager.SetEnabled(_settings.StartWithWindows);

        _menu = BuildMenu();
        _trayIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(paused: !_settings.AutoCheckEnabled),
            Text = "StarStrings Auto-Updater",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => _ = PerformCheckAsync(manualTrigger: true);

        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, _settings.CheckIntervalMinutes) * 60_000,
        };
        _timer.Tick += async (_, _) => await OnTimerTickAsync();
        _timer.Start();

        RefreshMenuState();
        Logger.Info("StarStrings Auto-Updater started.");

        _ = RunStartupSequenceAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Status: starting...") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        _toggleAutoCheckItem = new ToolStripMenuItem("Stop Auto-Check", null, OnToggleAutoCheck);
        menu.Items.Add(_toggleAutoCheckItem);

        _checkNowItem = new ToolStripMenuItem("Check for Updates Now", null, async (_, _) => await PerformCheckAsync(manualTrigger: true));
        menu.Items.Add(_checkNowItem);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Change Star Citizen LIVE Folder...", null, OnChangeLiveFolder));
        menu.Items.Add(new ToolStripMenuItem("Open Log Folder", null, (_, _) => OpenLogFolder()));

        menu.Items.Add(new ToolStripSeparator());

        _startWithWindowsItem = new ToolStripMenuItem("Start with Windows", null, OnToggleStartWithWindows)
        {
            CheckOnClick = false,
        };
        menu.Items.Add(_startWithWindowsItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication()));

        return menu;
    }

    private async Task RunStartupSequenceAsync()
    {
        if (!_settings.HasLiveFolder)
        {
            MessageBox.Show(
                "StarStrings Auto-Updater keeps MrKraken's StarStrings global.ini up to date for Star Citizen LIVE.\n\n" +
                "First, please locate your Star Citizen LIVE folder (the one containing the \"data\" folder and user.cfg), " +
                "e.g. ...\\Roberts Space Industries\\StarCitizen\\LIVE.",
                "Welcome to StarStrings Auto-Updater",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            PromptForLiveFolder();
        }

        if (_settings.AutoCheckEnabled)
        {
            await PerformCheckAsync(manualTrigger: false);
        }
        else
        {
            RefreshMenuState();
        }
    }

    private async Task OnTimerTickAsync()
    {
        if (_settings.AutoCheckEnabled)
        {
            await PerformCheckAsync(manualTrigger: false);
        }
    }

    private async Task PerformCheckAsync(bool manualTrigger)
    {
        if (_checkInProgress)
        {
            return;
        }

        _checkInProgress = true;
        _checkNowItem.Enabled = false;
        _statusItem.Text = "Status: checking for updates...";

        try
        {
            var outcome = await _updateService.CheckForUpdatesAsync(
                _settings,
                manualTrigger,
                release => Task.FromResult(UpdatePromptForm.AskUserToInstall(release)),
                ShowBalloon,
                CancellationToken.None);

            _settingsService.Save(_settings);

            if (manualTrigger && outcome == CheckOutcome.UpToDate)
            {
                ShowBalloon("You're already running the latest StarStrings version.", isError: false);
            }
        }
        finally
        {
            _checkInProgress = false;
            _checkNowItem.Enabled = true;
            RefreshMenuState();
        }
    }

    private void OnToggleAutoCheck(object? sender, EventArgs e)
    {
        _settings.AutoCheckEnabled = !_settings.AutoCheckEnabled;
        _settingsService.Save(_settings);
        _trayIcon.Icon = IconFactory.CreateTrayIcon(paused: !_settings.AutoCheckEnabled);
        Logger.Info(_settings.AutoCheckEnabled ? "Auto-check started by user." : "Auto-check stopped by user.");
        RefreshMenuState();

        if (_settings.AutoCheckEnabled)
        {
            _ = PerformCheckAsync(manualTrigger: false);
        }
    }

    private void OnChangeLiveFolder(object? sender, EventArgs e) => PromptForLiveFolder();

    private void PromptForLiveFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select your Star Citizen LIVE folder (contains the \"data\" folder and user.cfg)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (!string.IsNullOrWhiteSpace(_settings.LiveFolderPath) && Directory.Exists(_settings.LiveFolderPath))
        {
            dialog.SelectedPath = _settings.LiveFolderPath;
        }

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var chosen = dialog.SelectedPath;
        if (!chosen.TrimEnd('\\', '/').EndsWith("LIVE", StringComparison.OrdinalIgnoreCase))
        {
            var proceed = MessageBox.Show(
                $"\"{chosen}\" doesn't look like a Star Citizen LIVE folder (its name isn't \"LIVE\").\n\nUse it anyway?",
                "Confirm folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (proceed != DialogResult.Yes)
            {
                return;
            }
        }

        _settings.LiveFolderPath = chosen;
        _settingsService.Save(_settings);
        Logger.Info($"Star Citizen LIVE folder set to: {chosen}");
        RefreshMenuState();
        ShowBalloon("Star Citizen LIVE folder updated.", isError: false);
    }

    private void OnToggleStartWithWindows(object? sender, EventArgs e)
    {
        _settings.StartWithWindows = !_settings.StartWithWindows;
        StartupManager.SetEnabled(_settings.StartWithWindows);
        _settingsService.Save(_settings);
        Logger.Info($"Start with Windows set to: {_settings.StartWithWindows}");
        RefreshMenuState();
    }

    private void OpenLogFolder()
    {
        AppPaths.EnsureFoldersExist();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppPaths.LogsFolder,
            UseShellExecute = true,
        });
    }

    private void ShowBalloon(string message, bool isError)
    {
        _trayIcon.BalloonTipTitle = isError ? "StarStrings Auto-Updater - Attention" : "StarStrings Auto-Updater";
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = isError ? ToolTipIcon.Warning : ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(6000);
    }

    private void RefreshMenuState()
    {
        _toggleAutoCheckItem.Text = _settings.AutoCheckEnabled ? "Stop Auto-Check" : "Start Auto-Check";
        _startWithWindowsItem.Checked = _settings.StartWithWindows;

        var folderStatus = _settings.HasLiveFolder ? _settings.LiveFolderPath! : "not set - right-click to choose";
        var versionStatus = _settings.LastAppliedReleaseName ?? "none installed yet";
        var lastChecked = _settings.LastCheckedAtUtc?.ToLocalTime().ToString("g") ?? "never";

        _statusItem.Text = $"LIVE folder: {folderStatus} | Installed: {versionStatus} | Last checked: {lastChecked}";
        _trayIcon.Text = Truncate($"StarStrings Auto-Updater - {(_settings.AutoCheckEnabled ? "running" : "stopped")}", 127);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private void ExitApplication()
    {
        Logger.Info("StarStrings Auto-Updater exiting.");
        _timer.Stop();
        _timer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _updateService.Dispose();
        Application.Exit();
    }
}
