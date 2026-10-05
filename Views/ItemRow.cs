using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sidequest.Models;

namespace Sidequest.Views;

/// <summary>
/// A card on the panel or in a Sidequest's Items tab: its item, the Sidequest it belongs
/// to, and the states it passes through on screen - being renamed (5.1, 5.6) and being
/// dragged (D8). Those are states its template switches on, so they restyle the card
/// without rebuilding the list, and keyboard focus is not thrown back to the top of the
/// window.
/// </summary>
public abstract class ItemRow : INotifyPropertyChanged
{
    private bool _isEditing;
    private bool _isLifted;

    protected ItemRow(SidequestItem item, SidequestProject project)
    {
        Item = item;
        Project = project;
    }

    public SidequestItem Item { get; }

    public SidequestProject Project { get; }

    /// <summary>Whether its template has an edit state at all.</summary>
    public virtual bool CanRename => true;

    /// <summary>Whether it has a grip to be dragged by.</summary>
    public virtual bool CanMove => true;

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    /// <summary>Picked up by its grip and following the pointer (4.4's "tarjeta arrastrada").</summary>
    public bool IsLifted
    {
        get => _isLifted;
        set => Set(ref _isLifted, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
