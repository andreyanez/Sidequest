using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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

    [Fact]
    public void IconFile_HoldsTheMarkAtEverySizeWindowsAsksFor()
    {
        OnSta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"sidequest-icon-{Guid.NewGuid():N}.ico");
            try
            {
                TrayIconFactory.SaveIconFile(path);
                using var reader = new BinaryReader(File.OpenRead(path));

                Assert.Equal(0, (int)reader.ReadInt16());   // reserved
                Assert.Equal(1, (int)reader.ReadInt16());   // an icon, not a cursor
                int count = reader.ReadInt16();
                Assert.Equal(TrayIconFactory.IconFileSizes.Count, count);

                var entries = new List<(int Size, int Length, int Offset)>();
                for (var i = 0; i < count; i++)
                {
                    int width = reader.ReadByte(), height = reader.ReadByte();
                    Assert.Equal(width, height);
                    reader.ReadBytes(6);                    // palette, reserved, planes, bit count
                    entries.Add((width == 0 ? 256 : width, reader.ReadInt32(), reader.ReadInt32()));
                }

                Assert.Equal(TrayIconFactory.IconFileSizes, entries.Select(e => e.Size));

                // Every frame is a PNG of the size its entry states.
                foreach (var (size, length, offset) in entries)
                {
                    reader.BaseStream.Position = offset;
                    using var frame = new Bitmap(new MemoryStream(reader.ReadBytes(length)));
                    Assert.Equal(size, frame.Width);
                    Assert.Equal(size, frame.Height);
                }
            }
            finally
            {
                File.Delete(path);
            }
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
