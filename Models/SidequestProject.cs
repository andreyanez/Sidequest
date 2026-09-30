using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Sidequest.Models;

/// <summary>
/// A large task or project, with its own colour theme and its list of small steps.
///
/// The spec calls this a "Sidequest". The type is named SidequestProject instead
/// because the application's root namespace is also Sidequest, and a type sharing the
/// name makes every qualified reference ambiguous. The JSON property name is unaffected,
/// and the UI never shows a type name.
/// </summary>
public sealed class SidequestProject
{
    public const int MaxNameLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public SidequestTheme Theme { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Order in the Sidequests list. Append-only: nothing reorders (D7).</summary>
    public int SortIndex { get; set; }

    public ObservableCollection<SidequestItem> Items { get; set; } = new();

    /// <summary>
    /// Progress counts every item, including those currently in the panel. Computed
    /// properties are kept out of the file: a stored count goes stale the moment an
    /// item is ticked.
    /// </summary>
    [JsonIgnore]
    public int DoneCount => Items.Count(i => i.Done);

    [JsonIgnore]
    public int TotalCount => Items.Count;

    [JsonIgnore]
    public double Progress => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;
}
