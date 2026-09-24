using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sidequest.Models;
using Sidequest.Services;

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

namespace Sidequest.Views;

public partial class EditorWindow : Window
{
    /// <summary>
    /// Marker format only. The dragged item itself is held in <see cref="_draggingItem"/>
    /// rather than in the DataObject: a custom format makes WPF try to serialize the
    /// payload, and TaskItem is not serializable, so the data silently never arrives.
    /// The drag never leaves this process, so a field is sufficient.
    /// </summary>
    private const string ItemFormat = "Sidequest.TaskItem";

    private readonly WeekPlan _plan;
    private readonly NotifyCollectionChangedEventHandler _itemsChanged;
    private readonly Action _languageChanged;

    /// <summary>How close to the list's edge a drag must get before the list scrolls itself.</summary>
    private const double ScrollZone = 34;

    /// <summary>Pixels scrolled per tick while the drag sits in the edge zone.</summary>
    private const double ScrollStep = 16;

    private Point _dragOrigin;
    private TaskItem? _pendingDrag;
    private TaskItem? _draggingItem;
    private Border? _highlighted;

    private DispatcherTimer? _autoScroll;
    private double _autoScrollDelta;
    private Point _dragPointInScroller;

    public EditorWindow(WeekPlan plan)
    {
        _plan = plan;

        InitializeComponent();

        RangeText.Text = plan.RangeLabel;
        ItemList.ItemsSource = plan.Items;

        ThemeService.TrackTitleBar(this);

        // The plan outlives this window, so keep a handle to detach on close.
        _itemsChanged = (_, _) => UpdateEmptyState();
        _plan.Items.CollectionChanged += _itemsChanged;

        // The date range is culture-formatted, so it has to be rebuilt on a language switch.
        _languageChanged = () => RangeText.Text = _plan.RangeLabel;
        LanguageService.Changed += _languageChanged;

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

        var note = item.RepeatsWeekly ? LanguageService.Get("L_RemoveRepeatsNote") : string.Empty;

        if (!ConfirmDialog.Ask(this, LanguageService.Get("L_RemoveItemQ"), item.Text, footnote: note)) return;

        _plan.Items.Remove(item);
        Storage.SaveWeek(_plan);
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        var completed = _plan.Items.Where(i => i.Done).ToList();
        if (completed.Count == 0) return;

        var heading = completed.Count == 1
            ? LanguageService.Get("L_RemoveCompletedOneQ")
            : LanguageService.Format("L_RemoveCompletedManyQFmt", completed.Count);
        var detail = string.Join(Environment.NewLine, completed.Select(i => "•  " + i.Text));

        if (!ConfirmDialog.Ask(this, heading, detail, confirmLabel: LanguageService.Get("L_RemoveAll"))) return;

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
            StopAutoScroll();
            Highlight(null);
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

        _dragPointInScroller = e.GetPosition(ListScroll);
        UpdateAutoScroll();

        var target = RowBorderAt(e.GetPosition(ItemList));
        var valid = _draggingItem is not null
                    && e.Data.GetDataPresent(ItemFormat)
                    && target?.DataContext is TaskItem over
                    && !ReferenceEquals(over, _draggingItem);

        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        Highlight(valid ? target : null);
    }

    private void List_DragLeave(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        Highlight(null);
    }

    /// <summary>
    /// Scrolls the list while an item is dragged into its top or bottom edge. This needs a
    /// timer rather than acting straight from DragOver: DragOver only fires while the
    /// pointer moves, so holding still in the edge zone would stop the scroll after one step.
    /// </summary>
    private void UpdateAutoScroll()
    {
        var y = _dragPointInScroller.Y;

        if (y < ScrollZone) _autoScrollDelta = -ScrollStep;
        else if (y > ListScroll.ActualHeight - ScrollZone) _autoScrollDelta = ScrollStep;
        else _autoScrollDelta = 0;

        if (_autoScrollDelta == 0)
        {
            StopAutoScroll();
            return;
        }

        if (_autoScroll is not null) return;

        _autoScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _autoScroll.Tick += (_, _) =>
        {
            ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset + _autoScrollDelta);

            // The pointer has not moved, but the rows underneath it have, so the drop
            // highlight has to be recomputed from the unchanged screen position.
            var point = ItemList.PointFromScreen(ListScroll.PointToScreen(_dragPointInScroller));
            Highlight(RowBorderAt(point));
        };
        _autoScroll.Start();
    }

    private void StopAutoScroll()
    {
        _autoScroll?.Stop();
        _autoScroll = null;
        _autoScrollDelta = 0;
    }

    private void List_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        StopAutoScroll();
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
        LanguageService.Changed -= _languageChanged;
        base.OnClosed(e);
    }
}
