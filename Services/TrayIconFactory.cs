using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Sidequest.Services;

/// <summary>
/// Draws the tray icon at runtime instead of shipping an .ico, so the project
/// carries no binary assets and the glyph stays crisp at any DPI.
/// </summary>
public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xEB);

    /// <summary>A blue disc with a white tick. Reads clearly down to 16px.</summary>
    public static Icon Create(int size = 32)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var inset = size * 0.06f;
            var disc = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
            using (var brush = new SolidBrush(Accent))
                g.FillEllipse(brush, disc);

            // Tick, as a fraction of the icon box so it scales with size.
            using var pen = new Pen(Color.White, size * 0.11f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            g.DrawLines(pen, new[]
            {
                new PointF(size * 0.28f, size * 0.52f),
                new PointF(size * 0.44f, size * 0.68f),
                new PointF(size * 0.73f, size * 0.34f),
            });
        }

        // Icon.FromHandle does not take ownership, so clone into a managed icon
        // and release the GDI handle immediately.
        var handle = bitmap.GetHicon();
        try
        {
            using var shared = Icon.FromHandle(handle);
            return (Icon)shared.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
