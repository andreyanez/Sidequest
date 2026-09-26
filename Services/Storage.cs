using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// Reads and writes the two JSON files under %APPDATA%\Sidequest.
/// Both files are meant to be readable and hand-editable.
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

    public static string WeekPath => Path.Combine(Directory, "week.json");
    public static string SettingsPath => Path.Combine(Directory, "settings.json");

    /// <summary>
    /// Loads the plan and advances it to the current week if it is stale.
    /// A rollover is persisted immediately so it happens exactly once.
    /// </summary>
    public static WeekPlan LoadWeek()
    {
        var plan = Read<WeekPlan>(WeekPath) ?? new WeekPlan();
        if (plan.RollForwardToCurrentWeek())
            SaveWeek(plan);
        return plan;
    }

    public static void SaveWeek(WeekPlan plan) => Write(WeekPath, plan);

    public static AppSettings LoadSettings()
    {
        var settings = Read<AppSettings>(SettingsPath) ?? new AppSettings();
        settings.NormalizePopupTimes();
        return settings;
    }

    public static void SaveSettings(AppSettings settings)
    {
        settings.NormalizePopupTimes();
        Write(SettingsPath, settings);
    }

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
