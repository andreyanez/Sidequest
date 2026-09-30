using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// Reads and writes settings.json under %APPDATA%\Sidequest. Meant to be readable and
/// hand-editable. The Sidequests themselves live in <see cref="SidequestStore"/>.
/// </summary>
public static class Storage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Settable so a dev run can be sandboxed alongside SidequestStore. Without this the
    /// settings half of the app keeps writing to the real folder while its data does not.
    /// </summary>
    public static string Directory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sidequest");

    public static string SettingsPath => Path.Combine(Directory, "settings.json");

    /// <summary>
    /// A settings file written by an earlier build may still carry WeeklyReminder's fields
    /// (PopupTimes and the rest); unknown properties are ignored, so it still loads.
    /// </summary>
    public static AppSettings LoadSettings() => Read<AppSettings>(SettingsPath) ?? new AppSettings();

    public static void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    private static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Keep the unreadable file around rather than silently destroying data.
            TryBackup(path);
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);

        // Write to a sibling temp file first so a crash mid-write can't truncate the real one.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }

    private static void TryBackup(string path)
    {
        try
        {
            File.Move(path, $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort only; a failed backup must not stop the app from starting.
        }
    }
}
