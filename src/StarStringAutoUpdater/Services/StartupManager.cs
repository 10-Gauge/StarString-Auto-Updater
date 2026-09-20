using Microsoft.Win32;

namespace StarStringAutoUpdater.Services;

/// <summary>
/// Registers/unregisters the app to start automatically when the current user
/// logs in, via the per-user Run key. This intentionally does not require
/// administrator rights or a real Windows Service registration — see README
/// for why a literal SCM service can't host a tray icon.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StarStringAutoUpdater";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not determine the running executable's path.");
            key.SetValue(ValueName, $"\"{exePath}\" --tray", RegistryValueKind.String);
        }
        else
        {
            if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }
}
