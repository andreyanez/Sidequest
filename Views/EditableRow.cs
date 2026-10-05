using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sidequest.Models;

namespace Sidequest.Views;

/// <summary>
/// A row whose item can be renamed in place (5.1, 5.6). Editing is a state of the row that
/// its template switches on, so starting and ending it restyles the row without rebuilding
/// the list, and keyboard focus is not thrown back to the top of the window.
/// </summary>
public abstract class EditableRow : INotifyPropertyChanged
{
    private bool _isEditing;

    protected EditableRow(SidequestItem item)
    {
        Item = item;
    }

    public SidequestItem Item { get; }

    /// <summary>Whether its template has an edit state at all.</summary>
    public virtual bool CanRename => true;

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
