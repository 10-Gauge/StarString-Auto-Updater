using System.Drawing;
using System.Reflection;

namespace StarStringsAutoUpdater.UI;

/// <summary>
/// Loads the app's icon artwork (a star badge with a sync-arc, embedded as multi-resolution
/// .ico resources) rather than drawing it at runtime, so it can actually be designed and
/// previewed rather than guessed at via blind GDI+ calls.
/// </summary>
public static class IconFactory
{
    private const string ResourcePrefix = "StarStringsAutoUpdater.Assets.";

    private static readonly Icon RunningIcon = LoadEmbeddedIcon("AppIcon.ico");
    private static readonly Icon PausedIcon = LoadEmbeddedIcon("AppIconPaused.ico");

    /// <summary>Returns a new Icon instance at the requested size (an exact stored frame for
    /// the common sizes the artwork ships with, otherwise the closest one scaled).</summary>
    public static Icon CreateTrayIcon(bool paused = false, int size = 32)
    {
        var source = paused ? PausedIcon : RunningIcon;
        return new Icon(source, size, size);
    }

    private static Icon LoadEmbeddedIcon(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return new Icon(stream);
    }
}
