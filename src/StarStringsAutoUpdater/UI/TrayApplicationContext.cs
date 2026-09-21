using System.Windows.Forms;
using StarStringsAutoUpdater.Models;
using StarStringsAutoUpdater.Services;

namespace StarStringsAutoUpdater.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsService _settingsService = new();
    private readonly UpdateService _updateService = new();
    private readonly SelfUpdateService _selfUpdateService = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly string _appVersionText = FormatVersion(SelfUpdateService.GetCurrentVersion());

    private ToolStripMenuItem _appVersionItem = null!;
    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _toggleAutoCheckItem = null!;
    private ToolStripMenuItem _checkNowItem = null!;
    private ToolStripMenuItem _restoreBackupItem = null!;
    private ToolStripMenuItem _startWithWindowsItem = null!;
    private ToolStripMenuItem _interval30Item = null!;
    private ToolStripMenuItem _interval60Item = null!;
    private ToolStripMenuItem _interval180Item = null!;
    private ToolStripMenuItem _interval720Item = null!;
    private ToolStripMenuItem _interval1440Item = null!;
    private ToolStripMenuItem _intervalCustomItem = null!;

    private AppSettings _settings;
    private bool _checkInProgress;
    private bool _isExiting;

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

        _appVersionItem = new ToolStripMenuItem($"StarStrings Auto-Updater {_appVersionText}") { Enabled = false };
        menu.Items.Add(_appVersionItem);

        _statusItem = new ToolStripMenuItem("Status: starting...") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        _toggleAutoCheckItem = new ToolStripMenuItem("Stop Auto-Check", null, OnToggleAutoCheck);
        menu.Items.Add(_toggleAutoCheckItem);

        _checkNowItem = new ToolStripMenuItem("Check for Updates Now", null, async (_, _) => await PerformCheckAsync(manualTrigger: true));
        menu.Items.Add(_checkNowItem);

        var intervalMenu = new ToolStripMenuItem("Check Interval");
        _interval30Item = new ToolStripMenuItem("Every 30 Minutes", null, (_, _) => SetCheckInterval(30));
        _interval60Item = new ToolStripMenuItem("Every 60 Minutes", null, (_, _) => SetCheckInterval(60));
        _interval180Item = new ToolStripMenuItem("Every 3 Hours", null, (_, _) => SetCheckInterval(180));
        _interval720Item = new ToolStripMenuItem("Every 12 Hours", null, (_, _) => SetCheckInterval(720));
        _interval1440Item = new ToolStripMenuItem("Daily", null, (_, _) => SetCheckInterval(1440));
        _intervalCustomItem = new ToolStripMenuItem("Custom...", null, OnCustomInterval);
        intervalMenu.DropDownItems.AddRange([
            _interval30Item, _interval60Item, _interval180Item, _interval720Item, _interval1440Item,
            new ToolStripSeparator(),
            _intervalCustomItem,
        ]);
        menu.Items.Add(intervalMenu);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Change Star Citizen LIVE Folder...", null, OnChangeLiveFolder));
        menu.Items.Add(new ToolStripMenuItem("Open Log Folder", null, (_, _) => OpenLogFolder()));

        menu.Items.Add(new ToolStripSeparator());

        _restoreBackupItem = new ToolStripMenuItem("Restore Backup (global.ini.bak)", null, OnRestoreBackup);
        menu.Items.Add(_restoreBackupItem);
        menu.Opening += (_, _) => RefreshRestoreBackupState();

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

            var selfUpdateOutcome = await _selfUpdateService.CheckForUpdatesAsync(
                _settings,
                manualTrigger,
                (release, currentVersion) => Task.FromResult(AppUpdatePromptForm.AskUserToDownload(release, currentVersion)),
                OnAppUpdateDownloadedAsync,
                ShowBalloon,
                CancellationToken.None);

            _settingsService.Save(_settings);

            if (manualTrigger && outcome == CheckOutcome.UpToDate && selfUpdateOutcome == SelfUpdateOutcome.UpToDate)
            {
                ShowBalloon("You're already running the latest StarStrings content and app version.", isError: false);
            }
        }
        finally
        {
            _checkInProgress = false;
            if (!_isExiting)
            {
                _checkNowItem.Enabled = true;
                RefreshMenuState();
            }
        }
    }

    private Task OnAppUpdateDownloadedAsync(string exePath, GitHubRelease release)
    {
        var restart = MessageBox.Show(
            $"StarStrings Auto-Updater {release.TagName} has been downloaded.\n\n" +
            "Restart now to use it? The current version will close.\n\n" +
            $"You can also run it later from:\n{exePath}",
            "Update Downloaded", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;

        if (!restart)
        {
            return Task.CompletedTask;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });
            Logger.Info($"Launched new app version from {exePath}; exiting current instance.");
            ExitApplication();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to launch new app version: {ex.Message}");
            ShowBalloon("Failed to launch the new version automatically. You can run it manually from the updates folder.", isError: true);
        }

        return Task.CompletedTask;
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

    private void SetCheckInterval(int minutes)
    {
        if (_settings.CheckIntervalMinutes == minutes)
        {
            return;
        }

        _settings.CheckIntervalMinutes = minutes;
        _settingsService.Save(_settings);

        _timer.Stop();
        _timer.Interval = minutes * 60_000;
        _timer.Start();

        Logger.Info($"Check interval set to {FormatInterval(minutes)}.");
        RefreshMenuState();
    }

    private void OnCustomInterval(object? sender, EventArgs e)
    {
        if (CustomIntervalForm.TryAskForInterval(_settings.CheckIntervalMinutes, out var totalMinutes))
        {
            SetCheckInterval(totalMinutes);
        }
    }

    private void RefreshCheckIntervalMenu()
    {
        var minutes = _settings.CheckIntervalMinutes;
        _interval30Item.Checked = minutes == 30;
        _interval60Item.Checked = minutes == 60;
        _interval180Item.Checked = minutes == 180;
        _interval720Item.Checked = minutes == 720;
        _interval1440Item.Checked = minutes == 1440;

        var isPreset = minutes is 30 or 60 or 180 or 720 or 1440;
        _intervalCustomItem.Checked = !isPreset;
        _intervalCustomItem.Text = isPreset ? "Custom..." : $"Custom... ({FormatInterval(minutes)})";
    }

    private static string FormatInterval(int totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return (hours, minutes) switch
        {
            (0, _) => $"{minutes}m",
            (_, 0) => $"{hours}h",
            _ => $"{hours}h {minutes}m",
        };
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

    private void OnRestoreBackup(object? sender, EventArgs e)
    {
        if (!_settings.HasLiveFolder || !ZipInstaller.BackupExists(_settings.LiveFolderPath!))
        {
            ShowBalloon("No global.ini backup was found to restore.", isError: true);
            return;
        }

        var confirm = MessageBox.Show(
            "This will overwrite your current global.ini with the backup taken before the last " +
            "update (global.ini.bak). The app will treat this as reverted and offer to reinstall " +
            "the latest StarStrings version again next time it checks.\n\nRestore the backup now?",
            "Restore Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            ZipInstaller.RestoreBackup(_settings.LiveFolderPath!);
            _settings.ResetInstalledVersionTracking();
            _settingsService.Save(_settings);
            Logger.Info("Restored global.ini from backup at the user's request.");
            ShowBalloon("global.ini restored from backup.", isError: false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to restore global.ini from backup: {ex.Message}");
            ShowBalloon("Failed to restore the backup. See log for details.", isError: true);
        }

        RefreshMenuState();
    }

    private void RefreshRestoreBackupState()
    {
        _restoreBackupItem.Enabled = _settings.HasLiveFolder && ZipInstaller.BackupExists(_settings.LiveFolderPath!);
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
        RefreshRestoreBackupState();
        RefreshCheckIntervalMenu();

        var folderStatus = _settings.HasLiveFolder ? _settings.LiveFolderPath! : "not set - right-click to choose";
        var versionStatus = _settings.LastAppliedReleaseName ?? "none installed yet";
        var lastChecked = _settings.LastCheckedAtUtc?.ToLocalTime().ToString("g") ?? "never";

        _statusItem.Text = $"LIVE folder: {folderStatus} | Installed: {versionStatus} | Last checked: {lastChecked}";
        _trayIcon.Text = Truncate(
            $"StarStrings Auto-Updater {_appVersionText} - {(_settings.AutoCheckEnabled ? "running" : "stopped")}", 127);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string FormatVersion(Version v) => $"v{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Logger.Info("StarStrings Auto-Updater exiting.");
        _timer.Stop();
        _timer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _updateService.Dispose();
        _selfUpdateService.Dispose();
        Application.Exit();
    }
}
