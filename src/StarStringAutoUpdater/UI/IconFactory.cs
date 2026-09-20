using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace StarStringAutoUpdater.UI;

/// <summary>
/// Draws the tray icon at runtime instead of shipping a binary .ico asset,
/// so the whole app is buildable from source with nothing but the SDK.
/// </summary>
public static class IconFactory
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateTrayIcon(bool paused = false)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var backColor = paused ? Color.FromArgb(255, 120, 120, 120) : Color.FromArgb(255, 46, 125, 214);
            using (var brush = new SolidBrush(backColor))
            {
                g.FillEllipse(brush, 1, 1, size - 2, size - 2);
            }

            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var text = "S";
            var textSize = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush,
                (size - textSize.Width) / 2f - 1,
                (size - textSize.Height) / 2f - 1);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone(); // clone so the handle below can be safely destroyed
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
