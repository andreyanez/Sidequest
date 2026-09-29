using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Sidequest.Services;

/// <summary>Opens files and web addresses with whatever Windows has associated with them (5.3).</summary>
public static class Shell
{
    /// <summary>A linked file, in its own app. False when it has gone or will not open.</summary>
    public static bool OpenFile(string path) => File.Exists(path) && Start(path);

    /// <summary>A note's link, in the default browser. Only http and https, as 5.3 detects.</summary>
    public static bool OpenUrl(string url) => NoteLinks.IsOpenable(url) && Start(new Uri(url).AbsoluteUri);

    private static bool Start(string target)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // No associated app, or it refused. Nothing to recover; the caller says so.
            return false;
        }
    }
}
