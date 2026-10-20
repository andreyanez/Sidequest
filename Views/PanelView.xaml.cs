using System.Globalization;
using System.Windows;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using DragCompletedEventArgs = System.Windows.Controls.Primitives.DragCompletedEventArgs;
using DragDeltaEventArgs = System.Windows.Controls.Primitives.DragDeltaEventArgs;
using DragStartedEventArgs = System.Windows.Controls.Primitives.DragStartedEventArgs;
using Key = System.Windows.Input.Key;
using Thumb = System.Windows.Controls.Primitives.Thumb;
using ToolTipEventArgs = System.Windows.Controls.ToolTipEventArgs;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Sidequest.Views;

public partial class PanelView : UserControl
{
    private readonly MainWindow _shell;
    private readonly Action _refresh;
    private readonly InlineRename _rename;
    private readonly DragReorder _drag;

    public PanelView(MainWindow shell)
    {
        InitializeComponent();
        _shell = shell;
        _rename = new InlineRename(ObjectiveList);

        // 5.1: any objective, done or not, moves anywhere in the panel.
        _drag = new DragReorder(ObjectiveList, ListScroll, DragLayer,
            _ => (0, ObjectiveList.Items.Count - 1), MoveObjective);

        // A theme switch changes every Sidequest's colour, and a language switch changes
        // the weekday and date. Both are resolved in code, so both need a rebuild.
        //
        // Subscribed on Loaded rather than once in the constructor: navigating to a
        // sub-panel unloads this view, and it has to hear about switches again when it
        // comes back. Unsubscribing first keeps a second Loaded from doubling up.
        _refresh = Refresh;
        Loaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            ThemeService.Changed += _refresh;
            LanguageService.Changed += _refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
        };
    }

    private static IEnumerable<ObjectiveRow> BuildRows()
    {
        // PanelIndex: the order things were sent in, until dragging changes it (D8).
        return App.Current.Projects
            .SelectMany(project => project.Items
                .Where(item => item.InPanel)
                .Select(item => new ObjectiveRow(item, project)))
            .OrderBy(row => row.Item.PanelIndex ?? int.MaxValue)
            .ToList();
    }

    public void Refresh()
    {
        var rows = BuildRows().ToList();
        ObjectiveList.ItemsSource = rows;

        WeekdayText.Text = Weekday(DateTime.Today);
        DateText.Text = FullDate(DateTime.Today);
        UpdateCounter();

        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CounterText.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // The empty state carries its own "Sidequests →" (5.1), so the footer's gives way
        // to it: 4.4 allows one primary button per screen.
        SidequestsButton.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Where keyboard focus lands on coming back: the "Sidequests →" on screen, which is
    /// the one that left unless the panel filled or emptied in the meantime.
    /// </summary>
    internal void FocusSidequestsButton() =>
        Dispatcher.BeginInvoke(() => SidequestsEntry().Focus(), System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>The empty state's "Sidequests →" while it shows, the footer's otherwise.</summary>
    private Button SidequestsEntry() =>
        EmptyState.Visibility == Visibility.Visible ? EmptySidequestsButton : SidequestsButton;

    /// <summary>The collapsed row shows the same date and counter, so both are shared.</summary>
    internal static string FullDateText() => FullDate(DateTime.Today);

    internal (int Done, int Total) Counts()
    {
        var rows = BuildRows().ToList();
        return (rows.Count(r => r.Item.Done), rows.Count);
    }

    /// <summary>
    /// Upper case, because it is a section label (4.1) - the artboard renders "JUEVES"
    /// through a CSS text-transform, which the markup's own text does not show. WPF has
    /// no equivalent, so it happens here.
    /// </summary>
    private static string Weekday(DateTime date) =>
        date.ToString("dddd", CultureInfo.CurrentCulture).ToUpper(CultureInfo.CurrentCulture);

    /// <summary>
    /// The culture's long date with the weekday removed: es-ES's pattern leads with
    /// "dddd," and the line above already carries the day, as the artboard shows.
    /// </summary>
    private static string FullDate(DateTime date)
    {
        var pattern = CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern
            .Replace("dddd", string.Empty)
            .Trim()
            .TrimStart(',', ' ')
            .Trim();

        return date.ToString(pattern, CultureInfo.CurrentCulture);
    }

    private static void Save() => SidequestStore.Save(App.Current.Projects);

    /// <summary>Re-counts from the rows already on screen, without rebuilding them.</summary>
    private void UpdateCounter()
    {
        var rows = ObjectiveList.ItemsSource as IReadOnlyCollection<ObjectiveRow>
                   ?? Array.Empty<ObjectiveRow>();

        CounterText.Text = LanguageService.Format(
            "L_CounterFmt", rows.Count(r => r.Item.Done), rows.Count);
    }

    private void Done_Changed(object sender, RoutedEventArgs e)
    {
        // Deliberately not Refresh(): rebuilding the list here replaces the very row that
        // was just ticked, which threw keyboard focus back to the top of the window after
        // every tick. The strikethrough comes from a DataTrigger and SidequestItem raises
        // PropertyChanged, so the row restyles itself; only the counter needs saying.
        Save();
        UpdateCounter();
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        var index = RowIndex(row);
        SidequestRules.ReturnToSidequest(row.Item);

        Save();
        Refresh();
        FocusRowNear(index);
    }

    private int RowIndex(ObjectiveRow row) =>
        (ObjectiveList.ItemsSource as List<ObjectiveRow>)?.FindIndex(r => r.Item == row.Item) ?? 0;

    /// <summary>
    /// After a row leaves - returned or deleted - focus goes to the objective that took its
    /// place, or the one before it, or the Sidequests button: rebuilding the list would
    /// otherwise drop it back at the top of the window.
    /// </summary>
    private void FocusRowNear(int index) =>
        Dispatcher.BeginInvoke(() =>
        {
            var rows = ObjectiveList.ItemsSource as List<ObjectiveRow>;
            if (rows is not { Count: > 0 })
            {
                SidequestsEntry().Focus();
                return;
            }

            FocusObjectiveNow(rows[Math.Clamp(index, 0, rows.Count - 1)].Item);
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>A card's first stop is its checkbox.</summary>
    private void FocusObjectiveNow(SidequestItem item)
    {
        var check = VisualTree.FindDescendant<CheckBox>(ObjectiveList,
            c => c.Name == "Check" && c.DataContext is ObjectiveRow row && row.Item == item);
        if (check is not null) check.Focus();
        else SidequestsEntry().Focus();
    }

    /// <summary>5.1: the chip opens its Sidequest.</summary>
    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        _shell.ShowSidequest(row.Project);
    }

    /// <summary>
    /// Supr on a focused row deletes it, which 8 asks for: tabbing to the delete button is
    /// possible but slow, since every row carries several stops. F2 renames it, and
    /// Alt+Up and Alt+Down on its grip move it (5.1). Escape drops a drag. A row being
    /// renamed keeps its keys for the field.
    /// </summary>
    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (_drag.TryCancel(e) || _drag.TryMoveFromKey(e) || _rename.TryBeginFromKey(e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Delete) return;
        if (Keyboard.FocusedElement is not FrameworkElement { DataContext: ObjectiveRow row } || row.IsEditing) return;

        DeleteRow(row);
        e.Handled = true;
    }

    private void Title_ToolTipOpening(object sender, ToolTipEventArgs e) => TitleTip.Opening(sender, e);

    // ---- Renaming (5.1) -----------------------------------------------------------

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ObjectiveRow row }) _rename.Begin(row);
    }

    private void ItemNameEdit_KeyDown(object sender, KeyEventArgs e) => _rename.FieldKeyDown(e);

    private void ItemNameEdit_Pasting(object sender, DataObjectPastingEventArgs e) => InlineRename.Pasting(sender, e);

    private void SaveName_Click(object sender, RoutedEventArgs e) => _rename.Commit(focusRow: true);

    private void CancelName_Click(object sender, RoutedEventArgs e) => _rename.Cancel(focusRow: true);

    private void Card_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => _rename.FocusLeaving(e);

    private void View_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _rename.PointerDown(e);

    // ---- Reordering (5.1, D8) -----------------------------------------------------

    private void Grip_DragStarted(object sender, DragStartedEventArgs e) => _drag.Started(sender, e);

    private void Grip_DragDelta(object sender, DragDeltaEventArgs e) => _drag.Delta(sender, e);

    private void Grip_DragCompleted(object sender, DragCompletedEventArgs e) => _drag.Completed(sender, e);

    /// <summary>The panel's order is PanelIndex, renumbered in the new order and saved.</summary>
    private void MoveObjective(ItemRow row, int to)
    {
        if (ObjectiveList.ItemsSource is not List<ObjectiveRow> rows) return;

        var panel = rows.Select(r => r.Item).ToList();
        if (!SidequestRules.MoveInPanel(panel, panel.IndexOf(row.Item), to)) return;

        Save();
        Refresh();
        FocusGrip(row.Item);
    }

    /// <summary>After a move the list is new, so focus goes back to the grip that moved.</summary>
    private void FocusGrip(SidequestItem item) =>
        Dispatcher.BeginInvoke(() =>
        {
            var grip = VisualTree.FindDescendant<Thumb>(ObjectiveList,
                t => t.Name == "Grip" && t.DataContext is ObjectiveRow row && row.Item == item);
            grip?.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;
        DeleteRow(row);
    }

    private void DeleteRow(ObjectiveRow row)
    {
        var owner = Window.GetWindow(this);
        if (owner is null) return;

        // This removes the item from its Sidequest too, so it asks first.
        if (!ConfirmDialog.Ask(
                owner,
                LanguageService.Get("L_DeleteObjectiveQ"),
                row.Item.Text,
                confirmLabel: LanguageService.Get("L_DeleteObjective")))
            return;

        var index = RowIndex(row);
        row.Project.Items.Remove(row.Item);
        Save();
        Refresh();
        FocusRowNear(index);
    }

    private void Sidequests_Click(object sender, RoutedEventArgs e) => _shell.ShowSidequests();

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
}
