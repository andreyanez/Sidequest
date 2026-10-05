using System.Windows;
using System.Windows.Media.Animation;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Canvas = System.Windows.Controls.Canvas;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
using DragCompletedEventArgs = System.Windows.Controls.Primitives.DragCompletedEventArgs;
using DragDeltaEventArgs = System.Windows.Controls.Primitives.DragDeltaEventArgs;
using DragStartedEventArgs = System.Windows.Controls.Primitives.DragStartedEventArgs;
using ItemsControl = System.Windows.Controls.ItemsControl;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Mouse = System.Windows.Input.Mouse;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Thumb = System.Windows.Controls.Primitives.Thumb;
using Transform = System.Windows.Media.Transform;
using TranslateTransform = System.Windows.Media.TranslateTransform;

namespace Sidequest.Views;

/// <summary>
/// Reordering by dragging a card's grip (D8, 4.4, FnMainDrag), shared by the panel and a
/// Sidequest's Items tab. Past the system's drag distance the card lifts - a Line2 border
/// and a shadow, from the card's own template, and 6 to the right - and follows the
/// pointer up and down. The cards between it and where it would land slide aside, opening
/// a gap there, dashed in its Sidequest's colour over a 6 % tint. Letting go settles it
/// into the gap and hands the move to the view, which saves; Escape, or losing the
/// pointer to another window, puts everything back. A list taller than its scroll area
/// scrolls when the card is held near its top or bottom edge.
///
/// From the keyboard, Alt+Up and Alt+Down on a focused grip move its card one place.
///
/// A card moves only within the span `range` gives it: the whole panel, or its own group
/// of the detail (5.6). The view keeps the model and the saving, and this the pointer and
/// the pixels. Its templates name the parts it uses: "Grip", and "Card", whose size the
/// gap takes.
/// </summary>
internal sealed class DragReorder
{
    /// <summary>4.4: the other cards make way within 120 ms.</summary>
    private static readonly Duration Slide = new(TimeSpan.FromMilliseconds(110));

    /// <summary>FnMainDrag sets the dragged card 6 to the right of the column.</summary>
    private const double LiftX = 6;

    /// <summary>How near the scroll area's top or bottom edge the card makes it scroll.</summary>
    private const double ScrollEdge = 32;

    private readonly ItemsControl _list;
    private readonly ScrollViewer _scroll;
    private readonly Canvas _layer;
    private readonly Func<ItemRow, (int First, int Last)?> _range;
    private readonly Action<ItemRow, int> _move;
    private readonly DispatcherTimer _scrollTimer;

    // The drag under way, if there is one.
    private ItemRow? _row;
    private Thumb? _grip;
    private List<FrameworkElement> _containers = new();
    private double[] _tops = Array.Empty<double>();
    private double[] _heights = Array.Empty<double>();
    private int _from;
    private int _first;
    private int _last;
    private int _target;
    private double _downY;
    private double _grabY;
    private bool _lifted;
    private bool _settling;
    private Rectangle? _gap;

    /// <param name="list">The cards.</param>
    /// <param name="scroll">The scroll area they sit in.</param>
    /// <param name="layer">A canvas behind the cards, at the same origin, for the gap.</param>
    /// <param name="range">The indexes a card may move among, or null if it may not move.</param>
    /// <param name="move">Moves a card's item to a new index, saves and rebuilds the list.</param>
    public DragReorder(ItemsControl list, ScrollViewer scroll, Canvas layer,
        Func<ItemRow, (int First, int Last)?> range, Action<ItemRow, int> move)
    {
        _list = list;
        _scroll = scroll;
        _layer = layer;
        _range = range;
        _move = move;

        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollTimer.Tick += (_, _) => ScrollNearEdge();
    }

    // ---- The grip's Thumb events ---------------------------------------------------------

    public void Started(object sender, DragStartedEventArgs e)
    {
        if (_settling || sender is not Thumb { DataContext: ItemRow row } grip || !row.CanMove) return;
        if (_range(row) is not { } span) return;

        var containers = Containers();
        var from = containers.FindIndex(c => c.DataContext == row);
        if (from < span.First || from > span.Last) return;

        _row = row;
        _grip = grip;
        _containers = containers;
        _from = _target = from;
        (_first, _last) = span;
        _downY = Mouse.GetPosition(_list).Y;
        _lifted = false;
    }

    public void Delta(object sender, DragDeltaEventArgs e)
    {
        if (_row is null || _settling) return;

        var y = Mouse.GetPosition(_list).Y;
        if (!_lifted)
        {
            // A press that hardly moves is a click on the grip, not a drag.
            if (Math.Abs(y - _downY) < SystemParameters.MinimumVerticalDragDistance) return;
            Lift();
        }

        Follow(y);
    }

    public void Completed(object sender, DragCompletedEventArgs e)
    {
        if (_row is null || _settling) return;
        if (!_lifted)
        {
            Clear();
            return;
        }

        _scrollTimer.Stop();
        Settle(e.Canceled ? _from : _target);
    }

    /// <summary>5.1: Escape during a drag puts everything back.</summary>
    public bool TryCancel(KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _row is null || _grip is null || !_lifted) return false;

        _grip.CancelDrag();
        return true;
    }

    /// <summary>5.1: Alt+Up and Alt+Down on a focused grip move its card one place.</summary>
    public bool TryMoveFromKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.Up or Key.Down) || Keyboard.Modifiers != ModifierKeys.Alt) return false;
        if (_row is not null || e.OriginalSource is not Thumb { DataContext: ItemRow row } || !row.CanMove) return false;
        if (_range(row) is not { } span) return false;

        var from = Containers().FindIndex(c => c.DataContext == row);
        var to = key == Key.Up ? from - 1 : from + 1;

        // At either end of its span the key does nothing, rather than moving focus.
        if (from >= 0 && to >= span.First && to <= span.Last) _move(row, to);
        return true;
    }

    // ---- Dragging ----------------------------------------------------------------------

    private void Lift()
    {
        _lifted = true;

        // Where every card sits now, before anything moves: the slots the drag is laid out on.
        _tops = _containers.Select(c => c.TranslatePoint(new Point(0, 0), _list).Y).ToArray();
        _heights = _containers.Select(c => c.ActualHeight).ToArray();
        _grabY = _downY - _tops[_from];

        foreach (var container in _containers) container.RenderTransform = new TranslateTransform();
        var lifted = _containers[_from];
        Panel.SetZIndex(lifted, 1);
        ((TranslateTransform)lifted.RenderTransform).X = LiftX;
        _row!.IsLifted = true;

        // The gap takes the card's own size, without the space that follows it.
        var card = VisualTree.FindDescendant<FrameworkElement>(lifted, f => f.Name == "Card");
        var theme = _row.Project.Theme;
        _gap = new Rectangle
        {
            Width = card?.ActualWidth ?? lifted.ActualWidth,
            Height = card?.ActualHeight ?? _heights[_from],
            RadiusX = 9.5,
            RadiusY = 9.5,
            StrokeThickness = 1,
            StrokeDashArray = { 3, 2 },
            Stroke = SidequestPalette.Brush(theme, ThemeService.Current),
            Fill = SidequestPalette.Brush(theme, ThemeService.Current, SidequestPalette.Alpha.DropGapFill),
            SnapsToDevicePixels = true,
        };
        Canvas.SetTop(_gap, _tops[_from]);
        _layer.Children.Add(_gap);

        _scrollTimer.Start();
    }

    /// <summary>
    /// The card follows the pointer, held within its span; the others make way for the
    /// place it is nearest.
    /// </summary>
    private void Follow(double pointerY)
    {
        var top = Math.Clamp(pointerY - _grabY, LandingTop(_first), LandingTop(_last));
        ((TranslateTransform)_containers[_from].RenderTransform).Y = top - _tops[_from];

        var target = Nearest(top);
        if (target == _target) return;
        _target = target;

        for (var i = _first; i <= _last; i++)
        {
            if (i == _from) continue;

            var shift = _from < i && i <= target ? -_heights[_from]
                : target <= i && i < _from ? _heights[_from]
                : 0;
            Animate(_containers[i].RenderTransform, TranslateTransform.YProperty, shift);
        }

        Animate(_gap, Canvas.TopProperty, LandingTop(target));
    }

    /// <summary>Where the card's top lands if dropped at <paramref name="index"/>.</summary>
    private double LandingTop(int index) =>
        index > _from ? _tops[index] + _heights[index] - _heights[_from] : _tops[index];

    private int Nearest(double top)
    {
        var nearest = _from;
        for (var i = _first; i <= _last; i++)
            if (Math.Abs(LandingTop(i) - top) < Math.Abs(LandingTop(nearest) - top)) nearest = i;
        return nearest;
    }

    /// <summary>
    /// Slides the card into the gap at <paramref name="index"/> - its own place when the
    /// drag was cancelled - and then hands the move over, or tidies up.
    /// </summary>
    private void Settle(int index)
    {
        _settling = true;
        var row = _row!;

        if (index == _from)
        {
            for (var i = _first; i <= _last; i++)
                if (i != _from) Animate(_containers[i].RenderTransform, TranslateTransform.YProperty, 0);
            Animate(_gap, Canvas.TopProperty, _tops[_from]);
        }

        var lifted = _containers[_from].RenderTransform;
        Animate(lifted, TranslateTransform.XProperty, 0);
        Animate(lifted, TranslateTransform.YProperty, LandingTop(index) - _tops[_from], () =>
        {
            Clear();
            if (index != _from) _move(row, index);
        });
    }

    private void Clear()
    {
        _scrollTimer.Stop();

        foreach (var container in _containers)
        {
            container.RenderTransform = Transform.Identity;
            container.ClearValue(Panel.ZIndexProperty);
        }

        if (_gap is not null) _layer.Children.Remove(_gap);
        if (_row is not null) _row.IsLifted = false;

        _row = null;
        _grip = null;
        _gap = null;
        _containers = new List<FrameworkElement>();
        _lifted = false;
        _settling = false;
    }

    /// <summary>Held near the scroll area's top or bottom edge, the card scrolls the list.</summary>
    private void ScrollNearEdge()
    {
        if (!_lifted || _settling) return;

        var y = Mouse.GetPosition(_scroll).Y;
        var step = y < ScrollEdge ? -(ScrollEdge - y)
            : y > _scroll.ActualHeight - ScrollEdge ? y - (_scroll.ActualHeight - ScrollEdge)
            : 0;
        if (step == 0) return;

        var before = _scroll.VerticalOffset;
        _scroll.ScrollToVerticalOffset(before + Math.Clamp(step / 3, -10, 10));
        _scroll.UpdateLayout();

        if (_scroll.VerticalOffset != before) Follow(Mouse.GetPosition(_list).Y);
    }

    private List<FrameworkElement> Containers() =>
        Enumerable.Range(0, _list.Items.Count)
            .Select(i => _list.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement)
            .Select(c => c ?? new FrameworkElement())
            .ToList();

    private static void Animate(DependencyObject? target, DependencyProperty property, double to, Action? done = null)
    {
        if (target is not IAnimatable animatable) return;

        var animation = new DoubleAnimation(to, Slide)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        if (done is not null) animation.Completed += (_, _) => done();
        animatable.BeginAnimation(property, animation);
    }
}
