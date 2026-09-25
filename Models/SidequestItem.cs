using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Sidequest.Models;

/// <summary>
/// A small step inside a Sidequest. An item with <see cref="InPanel"/> set is what the
/// spec calls an "objetivo": the same object, shown in the main panel, not a second
/// entity. Done therefore reads the same in both places.
/// </summary>
public sealed class SidequestItem : INotifyPropertyChanged
{
    public const int MaxTextLength = 120;
    public const int MaxNotesLength = 500;
    public const int MaxFiles = 5;
    public const int MaxImages = 5;

    private string _text = "";
    private bool _done;
    private bool _inPanel;
    private string _notes = "";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public bool Done
    {
        get => _done;
        set => Set(ref _done, value);
    }

    /// <summary>True while this item sits in the main panel.</summary>
    public bool InPanel
    {
        get => _inPanel;
        set => Set(ref _inPanel, value);
    }

    /// <summary>Order within its Sidequest. Append-only: nothing reorders (D7).</summary>
    public int SortIndex { get; set; }

    /// <summary>Order within the main panel, or null when not in it. Append-only (D7).</summary>
    public int? PanelIndex { get; set; }

    public string Notes
    {
        get => _notes;
        set => Set(ref _notes, value);
    }

    /// <summary>Absolute paths, referenced not copied (D4).</summary>
    public List<string> Files { get; set; } = new();

    /// <summary>File names inside media\, copied on add (D4).</summary>
    public List<string> Images { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
