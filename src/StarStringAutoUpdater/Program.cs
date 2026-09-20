using System.Windows.Forms;
using StarStringAutoUpdater.Services;
using StarStringAutoUpdater.UI;

namespace StarStringAutoUpdater;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\StarStringAutoUpdater-SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "StarString Auto-Updater is already running. Check your system tray.",
                "Already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error($"Unhandled exception: {e.ExceptionObject}");

        Application.ThreadException += (_, e) =>
            Logger.Error($"UI thread exception: {e.Exception}");

        Application.Run(new TrayApplicationContext());

        GC.KeepAlive(mutex);
    }
}
