using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WeeklyReminder.Models;
using WeeklyReminder.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's for
// most of the drag-and-drop and media types. Alias them rather than qualifying each use.
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace WeeklyReminder.Views;

public partial class EditorWindow : Window
{
    /// <summary>
    /// Marker format only. The dragged item itself is held in <see cref="_draggingItem"/>
    /// rather than in the DataObject: a custom format makes WPF try to serialize the
    /// payload, and TaskItem is not serializable, so the data silently never arrives.
    /// The drag never leaves this process, so a field is sufficient.
    /// </summary>
    private const string ItemFormat = "WeeklyReminder.TaskItem";

    private readonly WeekPlan _plan;
    private readonly NotifyCollectionChangedEventHandler _itemsChanged;

    private Point _dragOrigin;
    private TaskItem? _pendingDrag;
    private TaskItem? _draggingItem;
    private Border? _highlighted;

    public EditorWindow(WeekPlan plan)
    {
        _plan = plan;

        InitializeComponent();

        RangeText.Text = plan.RangeLabel;
        ItemList.ItemsSource = plan.Items;

        // The plan outlives this window, so keep a handle to detach on close.
        _itemsChanged = (_, _) => UpdateEmptyState();
        _plan.Items.CollectionChanged += _itemsChanged;

        UpdateEmptyState();
        Loaded += (_, _) => NewItemText.Focus();
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _plan.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void NewItemText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddItem();
        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddItem();

    private void AddItem()
    {
        var text = NewItemText.Text.Trim();
        if (text.Length == 0) return;

        _plan.Items.Add(new TaskItem { Text = text });

        NewItemText.Clear();
        NewItemText.Focus();
        Storage.SaveWeek(_plan);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItem item }) return;

        var note = item.RepeatsWeekly
            ? "This one repeats weekly, so removing it stops it coming back."
            : string.Empty;

        if (!ConfirmDialog.Ask(this, "Remove this item?", item.Text, footnote: note)) return;

        _plan.Items.Remove(item);
        Storage.SaveWeek(_plan);
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        var completed = _plan.Items.Where(i => i.Done).ToList();
        if (completed.Count == 0) return;

        var heading = completed.Count == 1
            ? "Remove the completed item?"
            : $"Remove {completed.Count} completed items?";
        var detail = string.Join(Environment.NewLine, completed.Select(i => "•  " + i.Text));

        if (!ConfirmDialog.Ask(this, heading, detail, confirmLabel: "Remove all")) return;

        foreach (var item in completed)
            _plan.Items.Remove(item);

        Storage.SaveWeek(_plan);
    }

    // ---- Drag to reorder -------------------------------------------------------

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItem item } handle) return;

        // Only arm the drag here; starting it on mouse-down would swallow plain clicks.
        _dragOrigin = e.GetPosition(this);
        _pendingDrag = item;

        // Capture, or a quick flick moves the pointer off this small grip before the
        // threshold is crossed and the move events go elsewhere - the drag never starts.
        handle.CaptureMouse();
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingDrag is null || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _pendingDrag;
        _pendingDrag = null;

        // DoDragDrop installs its own capture, so ours has to go first.
        (sender as FrameworkElement)?.ReleaseMouseCapture();

        _draggingItem = item;
        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                new DataObject(ItemFormat, string.Empty),
                DragDropEffects.Move);
        }
        finally
        {
            _draggingItem = null;
        }
    }

    /// <summary>Released without crossing the threshold — it was a click, not a drag.</summary>
    private void DragHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _pendingDrag = null;
        (sender as FrameworkElement)?.ReleaseMouseCapture();
    }

    private void List_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var target = RowBorderAt(e.GetPosition(ItemList));
        var valid = _draggingItem is not null
                    && e.Data.GetDataPresent(ItemFormat)
                    && target?.DataContext is TaskItem over
                    && !ReferenceEquals(over, _draggingItem);

        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        Highlight(valid ? target : null);
    }

    private void List_DragLeave(object sender, DragEventArgs e) => Highlight(null);

    private void List_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        Highlight(null);

        if (_draggingItem is null || !e.Data.GetDataPresent(ItemFormat)) return;
        if (RowBorderAt(e.GetPosition(ItemList))?.DataContext is not TaskItem target) return;
        if (ReferenceEquals(target, _draggingItem)) return;

        var from = _plan.Items.IndexOf(_draggingItem);
        var to = _plan.Items.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return;

        _plan.Items.Move(from, to);
        Storage.SaveWeek(_plan);
    }

    /// <summary>Walks up from whatever was hit to the row container, identified by its Tag.</summary>
    private Border? RowBorderAt(Point point)
    {
        var hit = VisualTreeHelper.HitTest(ItemList, point);
        DependencyObject? node = hit?.VisualHit;

        while (node is not null)
        {
            if (node is Border { Tag: "Row" } row) return row;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private void Highlight(Border? row)
    {
        if (ReferenceEquals(_highlighted, row)) return;

        if (_highlighted is not null) _highlighted.Background = Brushes.Transparent;
        _highlighted = row;
        if (_highlighted is not null) _highlighted.Background = (Brush)FindResource("DropTargetBrush");
    }

    // ----------------------------------------------------------------------------

    /// <summary>Shortcut to the schedule, which is otherwise only reachable from the tray menu.</summary>
    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _plan.Items.CollectionChanged -= _itemsChanged;
        base.OnClosed(e);
    }
}
