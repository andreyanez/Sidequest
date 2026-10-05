using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Sidequest.Models;

/// <summary>
/// A small step inside a Sidequest. An item with <see cref="InPanel"/> set is what the
/// spec calls an "objetivo": the same object, shown in the main panel, not a second
/// entity. Done therefore reads the same in both places.
///
/// In 0.1.0 an item also carried notes, files and images. They moved to the Sidequest in
/// 0.2, and a 0.1.0 file's are not read, so the next save drops them (D10).
/// </summary>
public sealed class SidequestItem : INotifyPropertyChanged
{
    public const int MaxTextLength = 120;

    private string _text = "";
    private bool _done;
    private bool _inPanel;

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

    /// <summary>Order within its Sidequest, which dragging changes (D8).</summary>
    public int SortIndex { get; set; }

    /// <summary>
    /// Order within the main panel, or null when not in it. Dragging changes it (D8),
    /// independently of SortIndex.
    /// </summary>
    public int? PanelIndex { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
