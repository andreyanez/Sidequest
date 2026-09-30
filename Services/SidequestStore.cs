using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// Reads and writes sidequests.json under %APPDATA%\Sidequest. Keeps the guarantees the
/// old Storage had and that are worth keeping: readable JSON, an atomic write so a crash
/// mid-save cannot truncate the file, and quarantine rather than deletion when a file
/// will not parse.
/// </summary>
public static class SidequestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Defaults to %APPDATA%\Sidequest. Settable only so tests can redirect the store to
    /// a temp folder; the app never reassigns it. Without this a test run would read and
    /// overwrite the user's real data.
    /// </summary>
    public static string Directory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sidequest");

    public static string SidequestsPath => Path.Combine(Directory, "sidequests.json");

    /// <summary>Images copied out of their original location (D4).</summary>
    public static string MediaDirectory => Path.Combine(Directory, "media");

    public static bool Exists => File.Exists(SidequestsPath);

    public static ObservableCollection<SidequestProject> Load()
    {
        if (!File.Exists(SidequestsPath)) return new ObservableCollection<SidequestProject>();

        try
        {
            var loaded = JsonSerializer.Deserialize<List<SidequestProject>>(
                File.ReadAllText(SidequestsPath), Options);
            return new ObservableCollection<SidequestProject>(loaded ?? new List<SidequestProject>());
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Quarantine(SidequestsPath);
            return new ObservableCollection<SidequestProject>();
        }
    }

    public static void Save(IEnumerable<SidequestProject> projects)
    {
        System.IO.Directory.CreateDirectory(Directory);

        var temp = SidequestsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(projects.ToList(), Options));
        File.Move(temp, SidequestsPath, overwrite: true);
    }

    public static void EnsureMediaDirectory() => System.IO.Directory.CreateDirectory(MediaDirectory);

    private static void Quarantine(string path)
    {
        try
        {
            File.Move(path, $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort only; a failed quarantine must not stop the app from starting.
        }
    }
}
