using System.Windows.Forms;
using StarStringsAutoUpdater.Services;
using StarStringsAutoUpdater.UI;

namespace StarStringsAutoUpdater;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\StarStringsAutoUpdater-SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName);

        bool acquired;
        try
        {
            // Wait briefly rather than failing immediately: a self-update relaunches the
            // new exe and then exits the old one, so the new instance may start a moment
            // before the old one has actually released the mutex.
            acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            // The previous owner exited without releasing it (e.g. crashed); we now own it.
            acquired = true;
        }

        if (!acquired)
        {
            MessageBox.Show(
                "StarStrings Auto-Updater is already running. Check your system tray.",
                "Already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error($"Unhandled exception: {e.ExceptionObject}");

        Application.ThreadException += (_, e) =>
            Logger.Error($"UI thread exception: {e.Exception}");

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
