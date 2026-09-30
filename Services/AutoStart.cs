using System.IO;
using Microsoft.Win32;

namespace Sidequest.Services;

/// <summary>
/// Start-with-Windows via the per-user Run key. No elevation needed, and it is
/// trivial for the user to inspect or remove by hand.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Sidequest";

    /// <summary>
    /// When set, on/off lives in this file instead of the Run key. A sandboxed dev run sets it
    /// (SIDEQUEST_DATA), and so does the test suite. A dev build registered under Run would
    /// start at the next sign-in without the sandbox - against the user's real data, and with
    /// the WeeklyReminder import ready to rename their week.json.
    /// </summary>
    public static string? SandboxFile { get; set; }

    public static bool IsEnabled()
    {
        if (SandboxFile is not null) return File.Exists(SandboxFile);

        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public static void Set(bool enabled)
    {
        if (SandboxFile is not null)
        {
            if (!enabled)
            {
                File.Delete(SandboxFile);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SandboxFile)!);
            File.WriteAllText(SandboxFile, Environment.ProcessPath ?? string.Empty);
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null) return;

        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
