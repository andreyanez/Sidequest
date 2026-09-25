using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// One-time import of WeeklyReminder's data (D3, D5).
///
/// Runs only when Sidequest has no data of its own and the old folder still has some,
/// so it cannot overwrite anything. The old files are renamed rather than deleted: the
/// import is lossy by design - RepeatsWeekly has no equivalent once the weekly rollover
/// is gone (D2) - and the original should stay recoverable.
///
/// The old format is parsed through private DTOs rather than the live model, so this
/// keeps working after the old classes are deleted in phase 2.
/// </summary>
public static class LegacyMigration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Defaults to %APPDATA%\WeeklyReminder. Settable only so tests can point at a temp
    /// folder rather than the user's real WeeklyReminder data, which this renames.
    /// </summary>
    public static string LegacyDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WeeklyReminder");

    public static string LegacyWeekPath => Path.Combine(LegacyDirectory, "week.json");
    public static string LegacySettingsPath => Path.Combine(LegacyDirectory, "settings.json");

    private sealed class LegacyWeek
    {
        public List<LegacyItem> Items { get; set; } = new();
    }

    private sealed class LegacyItem
    {
        public string Text { get; set; } = "";
        public bool Done { get; set; }
    }

    private sealed class LegacySettings
    {
        public AppTheme? Theme { get; set; }
        public AppLanguage? Language { get; set; }
        public bool? KeepOnTop { get; set; }
        public bool? StartWithWindows { get; set; }
    }

    /// <summary>
    /// Imports the old week into a single Sidequest and copies across the handful of
    /// settings that still mean something. Returns the imported project, or null when
    /// there was nothing to do.
    /// </summary>
    public static SidequestProject? Run(AppSettings settings, string importedName)
    {
        if (SidequestStore.Exists) return null;
        if (!File.Exists(LegacyWeekPath)) return null;

        LegacyWeek? week;
        try
        {
            week = JsonSerializer.Deserialize<LegacyWeek>(File.ReadAllText(LegacyWeekPath), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }

        if (week is null) return null;

        var project = new SidequestProject
        {
            Name = importedName,
            Theme = SidequestTheme.Slate,
            SortIndex = 0,
        };

        // Array order is the order the user dragged them into, so it carries over as
        // SortIndex. Nothing lands in the panel: objectives are chosen deliberately.
        var index = 0;
        foreach (var item in week.Items)
        {
            project.Items.Add(new SidequestItem
            {
                Text = item.Text,
                Done = item.Done,
                InPanel = false,
                SortIndex = index++,
            });
        }

        SidequestStore.Save(new[] { project });
        CopySettings(settings);
        RenameLegacyWeek();

        return project;
    }

    private static void CopySettings(AppSettings settings)
    {
        if (!File.Exists(LegacySettingsPath)) return;

        try
        {
            var old = JsonSerializer.Deserialize<LegacySettings>(File.ReadAllText(LegacySettingsPath), Options);
            if (old is null) return;

            if (old.Theme is { } theme) settings.Theme = theme;
            if (old.Language is { } language) settings.Language = language;
            if (old.KeepOnTop is { } keepOnTop) settings.KeepOnTop = keepOnTop;
            if (old.StartWithWindows is { } startWithWindows) settings.StartWithWindows = startWithWindows;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Settings are a convenience; failing to carry them over must not block the import.
        }
    }

    private static void RenameLegacyWeek()
    {
        try
        {
            File.Move(LegacyWeekPath, LegacyWeekPath + ".migrated", overwrite: true);
        }
        catch (IOException)
        {
            // If the rename fails the import still stands: SidequestStore.Exists is now
            // true, so this will not run a second time.
        }
    }
}
