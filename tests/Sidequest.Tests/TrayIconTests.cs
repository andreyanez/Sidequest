using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

/// <summary>
/// 5.9's mark: a rounded square with the "S" cut out, drawn at runtime. Rendering needs an
/// STA thread, as WPF drawing does anywhere.
/// </summary>
public class TrayIconTests
{
    private static void OnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void TrayIcon_IsDrawnAtTheNotificationAreasOwnSize()
    {
        OnSta(() =>
        {
            using var icon = TrayIconFactory.Create();
            var expected = System.Windows.Forms.SystemInformation.SmallIconSize;

            Assert.Equal(expected.Width, icon.Width);
            Assert.Equal(expected.Height, icon.Height);
        });
    }

    [Fact]
    public void Mark_IsASquareWithTheSCutOut()
    {
        OnSta(() =>
        {
            var source = TrayIconFactory.WindowIcon();
            using var bitmap = ToBitmap(source);
            var size = bitmap.Width;

            // Clear at the corners: the square is rounded, and inset from the edge.
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
            Assert.Equal(0, bitmap.GetPixel(size - 1, size - 1).A);

            // Solid down the square's left edge, beside the S rather than through it.
            Assert.Equal(255, bitmap.GetPixel(size / 8, size / 2).A);

            // The S's middle stroke crosses the centre, and is cut clean through.
            Assert.True(bitmap.GetPixel(size / 2, size / 2).A < 128, "the S should be cut out of the square");

            // Kept for looking at: the checks above cannot say whether it reads as an S.
            bitmap.Save(Path.Combine(Path.GetTempPath(), "sidequest-mark.png"));
        });
    }

    private static Bitmap ToBitmap(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
