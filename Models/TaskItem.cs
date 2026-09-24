using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sidequest.Models;

/// <summary>A single thing planned for the week.</summary>
public sealed class TaskItem : INotifyPropertyChanged
{
    private string _text = "";
    private bool _done;
    private bool _repeatsWeekly;

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

    /// <summary>Survives the Monday rollover instead of being cleared.</summary>
    public bool RepeatsWeekly
    {
        get => _repeatsWeekly;
        set => Set(ref _repeatsWeekly, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
