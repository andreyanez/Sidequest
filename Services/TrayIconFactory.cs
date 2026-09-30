using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using Icon = System.Drawing.Icon;
using Point = System.Windows.Point;

namespace Sidequest.Services;

/// <summary>
/// Draws the app's mark at runtime instead of shipping an .ico, so the project carries no
/// binary assets (5.9): a neutral rounded square with the "S" cut out of it.
///
/// Its colour is 4.2's Text for the taskbar it sits on - light on a dark taskbar, dark on
/// a light one - and not for the app's theme: Windows sets the two apart, and a light app
/// on a dark taskbar would otherwise draw a dark icon onto dark.
/// </summary>
public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    // 4.2's Text, dark and light.
    private static readonly Color OnDark = Color.FromRgb(0xE8, 0xE9, 0xEC);
    private static readonly Color OnLight = Color.FromRgb(0x17, 0x18, 0x1A);

    private static Color TaskbarText => ThemeService.TaskbarIsLight() ? OnLight : OnDark;

    /// <summary>
    /// The tray icon, at the size the notification area really shows (16px at 100 %), so
    /// Windows does not scale a larger one down into a blur.
    /// </summary>
    public static Icon Create()
    {
        var size = System.Windows.Forms.SystemInformation.SmallIconSize.Width;
        return ToIcon(Render(size, TaskbarText));
    }

    /// <summary>The same mark for a window's taskbar button, which sits on the same taskbar.</summary>
    public static BitmapSource WindowIcon() => Render(32, TaskbarText);

    private static BitmapSource Render(int size, Color colour)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // A pixel clear of the edge at 16, so the square does not touch its neighbours.
            var inset = Math.Max(1, size / 16.0);
            var box = new Rect(inset, inset, size - inset * 2, size - inset * 2);
            var square = new RectangleGeometry(box, box.Width * 0.24, box.Height * 0.24);

            var font = System.Windows.Application.Current?.TryFindResource("AppFont") as FontFamily
                       ?? new FontFamily("Segoe UI");
            var text = new FormattedText("S", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                box.Height * 0.8, Brushes.Black, pixelsPerDip: 1.0);

            // Centred on its ink, not its line box, which carries the font's ascent.
            var glyph = text.BuildGeometry(new Point(0, 0));
            var ink = glyph.Bounds;
            glyph.Transform = new TranslateTransform(
                box.X + (box.Width - ink.Width) / 2 - ink.X,
                box.Y + (box.Height - ink.Height) / 2 - ink.Y);

            var mark = new CombinedGeometry(GeometryCombineMode.Exclude, square, glyph);
            dc.DrawGeometry(new SolidColorBrush(colour), null, mark);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>A WPF bitmap as the GDI icon NotifyIcon needs, by way of a PNG.</summary>
    private static Icon ToIcon(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        using var bitmap = new System.Drawing.Bitmap(stream);

        // Icon.FromHandle does not take ownership, so clone into a managed icon and release
        // the GDI handle at once.
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
