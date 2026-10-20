using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using AutomationProperties = System.Windows.Automation.AutomationProperties;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
using DragCompletedEventArgs = System.Windows.Controls.Primitives.DragCompletedEventArgs;
using DragDeltaEventArgs = System.Windows.Controls.Primitives.DragDeltaEventArgs;
using DragStartedEventArgs = System.Windows.Controls.Primitives.DragStartedEventArgs;
using InputManager = System.Windows.Input.InputManager;
using Key = System.Windows.Input.Key;
using KeyboardDevice = System.Windows.Input.KeyboardDevice;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using Visual = System.Windows.Media.Visual;

namespace Sidequest.Views;

/// <summary>
/// A Sidequest's detail (5.6): its band, and under it the Items and Notas tabs. Items holds
/// the items in three fixed groups, renamed and added in place; Notas is NotesTab. Every
/// change saves at once; there is no save button anywhere.
/// </summary>
public partial class SidequestView : UserControl
{
    /// <summary>How long a press on the title has to be held to rename it (5.6).</summary>
    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(500);

    /// <summary>5.6: how long an item Enter has just added stays lit.</summary>
    private static readonly TimeSpan NewItemGlow = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// The tab last chosen in each Sidequest, for the session only (5.6): leaving and
    /// coming back finds it again, and quitting forgets it.
    /// </summary>
    private static readonly Dictionary<Guid, int> LastTab = new();

    private readonly MainWindow _shell;
    private readonly SidequestProject _project;
    private readonly bool _justCreated;
    private readonly Action _refresh;
    private readonly DispatcherTimer _pressTimer;
    private readonly DispatcherTimer _glowTimer;
    private readonly InlineRename _rename;
    private readonly DragReorder _drag;

    private SidequestItem? _newItem;
    private SidequestTheme _themeBeforePicker;
    private bool _closingPicker;

    public SidequestView(MainWindow shell, SidequestProject project, bool justCreated = false)
    {
        InitializeComponent();
        _shell = shell;
        _project = project;
        _justCreated = justCreated;
        _rename = new InlineRename(ItemList);
        _drag = new DragReorder(ItemList, ListScroll, DragLayer, GroupSpan, MoveItem);

        NotesTabItem.Content = new NotesTab(project);

        // A new Sidequest is empty, and its next step is always its first item.
        Tabs.SelectedIndex = justCreated ? 0 : LastTab.GetValueOrDefault(project.Id);

        _pressTimer = new DispatcherTimer { Interval = LongPress };
        _pressTimer.Tick += (_, _) =>
        {
            _pressTimer.Stop();
            BeginRename();
        };

        _glowTimer = new DispatcherTimer { Interval = NewItemGlow };
        _glowTimer.Tick += (_, _) => EndGlow();

        // Escape closes the menu without choosing, and focus would otherwise drop to the
        // window. An entry that was chosen moves focus on after this, so it still wins.
        ((ContextMenu)Resources["MoreMenu"]).Closed += (_, _) =>
        {
            if (!IsKeyboardFocusWithin) MoreButton.Focus();
        };

        // An app theme switch recolours the swatches too; nothing else touches them.
        _refresh = () => Refresh(rebuildSwatches: true);
        Loaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            ThemeService.Changed += _refresh;
            LanguageService.Changed += _refresh;
            Refresh();

            if (_justCreated) OpenAddField();
        };
        Unloaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            _pressTimer.Stop();
            _glowTimer.Stop();
        };
    }

    private static void Save() => SidequestStore.Save(App.Current.Projects);

    private void Refresh(bool rebuildSwatches = false)
    {
        DataContext = new SidequestHeader(_project);
        ItemList.ItemsSource = SidequestRules.DetailOrder(_project)
            .Select(item => DetailRow.For(item, _project))
            .ToList();

        // A rebuild inside the 1.5 s keeps the glow on the item that has it.
        if (_newItem is not null && RowOf(_newItem) is { } row) row.IsNew = true;

        // Items shows the number of items, whatever their state (5.6).
        var count = _project.Items.Count.ToString(CultureInfo.CurrentCulture);
        ItemsTab.Header = count;
        AutomationProperties.SetName(ItemsTab, $"{LanguageService.Get("L_Items")} {count}");

        if (rebuildSwatches && ThemePicker.Visibility == Visibility.Visible)
        {
            ThemePicker.ItemsSource = ThemeSwatch.All();
            SelectSwatch(_project.Theme);
        }
    }

    private DetailRow? RowOf(SidequestItem item) =>
        (ItemList.ItemsSource as IEnumerable<DetailRow>)?.FirstOrDefault(r => r.Item == item);

    private List<SidequestItem> PendingItems() =>
        SidequestRules.DetailOrder(_project).Where(i => SidequestRules.GroupOf(i) == ItemGroup.Pending).ToList();

    /// <summary>
    /// After an item leaves the pending group, keyboard focus goes to the pending item that
    /// took its place, or the one before it, or failing both the way to add one - never
    /// back to the top of the window.
    /// </summary>
    private void FocusPendingNear(int index)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var pending = PendingItems();
            if (pending.Count == 0)
            {
                FocusAddEntry();
                return;
            }

            FocusSendButton(pending[Math.Clamp(index, 0, pending.Count - 1)]);
        });
    }

    private void FocusSendButton(SidequestItem item)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var button = VisualTree.FindDescendant<Button>(ItemList, b =>
                b.Name == "SendButton" && b.DataContext is PendingItemRow row && row.Item == item);
            button?.Focus();
        });
    }

    private void FocusAddEntry()
    {
        if (AddField.Visibility == Visibility.Visible) AddBox.Focus();
        else AddButton.Focus();
    }

    // ---- Tabs (5.6) -------------------------------------------------------------------

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selections inside the tabs bubble up through here too.
        if (e.OriginalSource != Tabs) return;

        LastTab[_project.Id] = Tabs.SelectedIndex;
    }

    // ---- Items ----------------------------------------------------------------------

    /// <summary>"Enviar objetivo": into the panel, and to the top group, without leaving (5.6).</summary>
    private void Send_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PendingItemRow row }) return;

        var index = PendingItems().IndexOf(row.Item);
        SidequestRules.SendToPanel(row.Item, App.Current.Projects);
        Save();
        Refresh();
        FocusPendingNear(index);
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PendingItemRow row }) DeleteItem(row);
    }

    /// <summary>
    /// Supr on a pending row deletes it, as it does on the panel; F2 on any card renames
    /// it, and Alt+Up and Alt+Down on its grip move it (5.1). Escape drops a drag. A row
    /// being renamed keeps its keys for the field.
    /// </summary>
    private void ItemList_KeyDown(object sender, KeyEventArgs e)
    {
        if (_drag.TryCancel(e) || _drag.TryMoveFromKey(e) || _rename.TryBeginFromKey(e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Delete) return;
        if (Keyboard.FocusedElement is not FrameworkElement { DataContext: PendingItemRow row } || row.IsEditing) return;

        DeleteItem(row);
        e.Handled = true;
    }

    private void DeleteItem(PendingItemRow row)
    {
        var owner = Window.GetWindow(this);
        if (owner is null) return;

        if (!ConfirmDialog.Ask(
                owner,
                LanguageService.Get("L_DeleteItemQ"),
                row.Item.Text,
                confirmLabel: LanguageService.Get("L_DeleteItem")))
            return;

        var index = PendingItems().IndexOf(row.Item);
        _project.Items.Remove(row.Item);
        Save();
        Refresh();
        FocusPendingNear(index);
    }

    /// <summary>
    /// Unticking a completed item that is not in the panel - the only place 5.6 lets Done
    /// be edited from here. It goes back to pending, and focus follows it.
    /// </summary>
    private void Done_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CompletedItemRow row }) return;

        Save();
        Refresh();
        FocusSendButton(row.Item);
    }

    // ---- Renaming an item (5.6, as 5.1) ---------------------------------------------

    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DetailRow row }) _rename.Begin(row);
    }

    private void ItemNameEdit_KeyDown(object sender, KeyEventArgs e) => _rename.FieldKeyDown(e);

    private void ItemNameEdit_Pasting(object sender, DataObjectPastingEventArgs e) => InlineRename.Pasting(sender, e);

    private void SaveName_Click(object sender, RoutedEventArgs e) => _rename.Commit(focusRow: true);

    private void CancelName_Click(object sender, RoutedEventArgs e) => _rename.Cancel(focusRow: true);

    private void Card_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => _rename.FocusLeaving(e);

    /// <summary>
    /// A click elsewhere counts as leaving, for a rename and for an empty add field, even
    /// on a spot that cannot take focus and so would leave it where it was.
    /// </summary>
    private void View_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rename.PointerDown(e);

        if (AddField.Visibility == Visibility.Visible && AddBox.Text.Trim().Length == 0
            && !(e.OriginalSource is Visual target && AddField.IsAncestorOf(target)))
            CloseAddField(focusButton: false);
    }

    // ---- Reordering an item (5.6, D8) ----------------------------------------------

    private void Grip_DragStarted(object sender, DragStartedEventArgs e) => _drag.Started(sender, e);

    private void Grip_DragDelta(object sender, DragDeltaEventArgs e) => _drag.Delta(sender, e);

    private void Grip_DragCompleted(object sender, DragCompletedEventArgs e) => _drag.Completed(sender, e);

    /// <summary>
    /// 5.6: an item moves only among its own group - current objectives among themselves,
    /// pending items among themselves - which DetailOrder keeps together.
    /// </summary>
    private (int First, int Last)? GroupSpan(ItemRow row)
    {
        if (ItemList.ItemsSource is not List<DetailRow> rows || !row.CanMove) return null;

        var group = row.GetType();
        var first = rows.FindIndex(r => r.GetType() == group);
        return first < 0 ? null : (first, rows.FindLastIndex(r => r.GetType() == group));
    }

    /// <summary>The Sidequest's order is SortIndex, traded within the group and saved.</summary>
    private void MoveItem(ItemRow row, int to)
    {
        if (GroupSpan(row) is not { } span) return;
        if (!SidequestRules.MoveInGroup(_project, row.Item, to - span.First)) return;

        Save();
        Refresh();
        FocusGrip(row.Item);
    }

    /// <summary>After a move the list is new, so focus goes back to the grip that moved.</summary>
    private void FocusGrip(SidequestItem item)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var grip = VisualTree.FindDescendant<Thumb>(ItemList, t =>
                t.Name == "Grip" && t.DataContext is DetailRow row && row.Item == item);
            grip?.Focus();
        });
    }

    // ---- Add item (5.6) -------------------------------------------------------------

    private void AddButton_Click(object sender, RoutedEventArgs e) => OpenAddField();

    /// <summary>
    /// FnSidequestAdd: the dashed button gives way to the field and its hint, and the
    /// footer takes a Line along its top and 12 rather than 14 below.
    /// </summary>
    private void OpenAddField()
    {
        AddButton.Visibility = Visibility.Collapsed;
        AddField.Visibility = Visibility.Visible;
        Footer.BorderThickness = new Thickness(0, 1, 0, 0);
        Footer.Padding = new Thickness(12, 10, 12, 12);
        AddBox.Text = string.Empty;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddBox.Focus());
    }

    private void CloseAddField(bool focusButton)
    {
        AddField.Visibility = Visibility.Collapsed;
        AddButton.Visibility = Visibility.Visible;
        Footer.BorderThickness = new Thickness(0);
        Footer.Padding = new Thickness(12, 10, 12, 14);
        AddBox.Text = string.Empty;

        if (focusButton) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddButton.Focus());
    }

    private void CloseAdd_Click(object sender, RoutedEventArgs e) => CloseAddField(focusButton: true);

    /// <summary>Enter adds and leaves the field open, empty, for the next one; Escape closes it.</summary>
    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseAddField(focusButton: true);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;
        e.Handled = true;

        if (AddBox.Text.Trim().Length == 0)
        {
            AddBox.Text = string.Empty;
            return;
        }

        var item = SidequestRules.AddItem(_project, AddBox.Text);
        Save();

        _newItem = item;
        Refresh();
        AddBox.Text = string.Empty;

        // 5.6: lit for 1.5 s, counted afresh for each new item.
        _glowTimer.Stop();
        _glowTimer.Start();

        // It joins the end of the pending group, which can be scrolled out of sight above
        // the completed ones.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var card = VisualTree.FindDescendant<FrameworkElement>(ItemList, f =>
                f.Name == "Card" && f.DataContext is DetailRow r && r.Item == item);
            card?.BringIntoView();
        });
    }

    private void EndGlow()
    {
        _glowTimer.Stop();
        if (_newItem is not null && RowOf(_newItem) is { } row) row.IsNew = false;
        _newItem = null;
    }

    private void AddBox_TextChanged(object sender, TextChangedEventArgs e) =>
        AddHint.Visibility = AddBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Leaving an empty field closes it; one with text in it stays open and adds nothing,
    /// so a click elsewhere never makes an item by accident (5.6). Focus going nowhere is
    /// the window losing it to another app, and there the field waits: the next item's
    /// name may be being copied from there.
    /// </summary>
    private void AddBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is null) return;
        if (AddField.Visibility == Visibility.Visible && AddBox.Text.Trim().Length == 0)
            CloseAddField(focusButton: false);
    }

    // ---- "⋯" menu -------------------------------------------------------------------

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)Resources["MoreMenu"];
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Custom;

        // Under the button with its right edge on the button's. The template's margin is
        // room for the shadow (10 at the sides, 2 on top), so the visible edge is inset.
        menu.CustomPopupPlacementCallback = (popup, target, _) => new[]
        {
            new CustomPopupPlacement(new Point(target.Width - popup.Width + 10, target.Height + 2), PopupPrimaryAxis.Horizontal),
        };

        menu.IsOpen = true;

        // Opened from the keyboard, the first entry takes focus so the arrows work at
        // once; opened with the mouse, nothing is highlighted until hovered.
        if (InputManager.Current.MostRecentInputDevice is KeyboardDevice)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => (menu.Items[0] as MenuItem)?.Focus());
    }

    // ---- Rename the Sidequest (5.6) -------------------------------------------------

    private void Rename_Click(object sender, RoutedEventArgs e) => BeginRename();

    private void NameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _pressTimer.Stop();
            BeginRename();
            e.Handled = true;
            return;
        }

        _pressTimer.Start();
    }

    private void NameText_MouseReleased(object sender, RoutedEventArgs e) => _pressTimer.Stop();

    private void BeginRename()
    {
        NameEdit.Text = _project.Name;
        NameText.Visibility = Visibility.Hidden;
        NameEdit.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            NameEdit.Focus();
            NameEdit.SelectAll();
        });
    }

    private void EndRename(bool commit)
    {
        if (NameEdit.Visibility != Visibility.Visible) return;

        NameEdit.Visibility = Visibility.Collapsed;
        NameText.Visibility = Visibility.Visible;

        // A blank name is refused and the old one kept, rather than a nameless Sidequest.
        if (commit && SidequestRules.Rename(_project, NameEdit.Text))
        {
            Save();
            Refresh();
        }
    }

    /// <summary>Enter keeps the new name, Escape keeps the old one.</summary>
    private void NameEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;

        EndRename(commit: e.Key == Key.Enter);
        MoreButton.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// Leaving the field keeps the new name, as leaving a renamed item does. Focus going
    /// nowhere is the window losing it to another app, and there the field waits.
    /// </summary>
    private void NameEdit_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is null) return;
        EndRename(commit: true);
    }

    // ---- Change theme ---------------------------------------------------------------

    private void ChangeTheme_Click(object sender, RoutedEventArgs e)
    {
        _themeBeforePicker = _project.Theme;
        ThemePicker.ItemsSource = ThemeSwatch.All();
        SelectSwatch(_project.Theme);
        ThemePicker.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var selected = ThemePicker.ItemContainerGenerator.ContainerFromItem(ThemePicker.SelectedItem) as ListBoxItem;
            selected?.Focus();
        });
    }

    private void SelectSwatch(SidequestTheme theme) =>
        ThemePicker.SelectedItem = ThemePicker.Items.Cast<ThemeSwatch>().First(s => s.Theme == theme);

    /// <summary>Live: the band and every row take the new colour as the selection moves.</summary>
    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.Visibility != Visibility.Visible) return;
        if (ThemePicker.SelectedItem is not ThemeSwatch swatch || swatch.Theme == _project.Theme) return;

        _project.Theme = swatch.Theme;
        Refresh();
    }

    /// <summary>A click is a choice: keep it and close.</summary>
    private void ThemePicker_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ClosePicker(keep: true, focusMore: false));

    private void ThemePicker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;

        ClosePicker(keep: e.Key == Key.Enter, focusMore: true);
        e.Handled = true;
    }

    /// <summary>
    /// Tabbing or clicking away keeps whatever is showing. The event bubbles up from each
    /// swatch as the arrow keys move between them, so focus staying inside is not "away".
    /// </summary>
    private void ThemePicker_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is Visual next && ThemePicker.IsAncestorOf(next)) return;
        ClosePicker(keep: true, focusMore: false);
    }

    private void ClosePicker(bool keep, bool focusMore)
    {
        if (_closingPicker || ThemePicker.Visibility != Visibility.Visible) return;
        _closingPicker = true;

        if (!keep && _project.Theme != _themeBeforePicker)
        {
            _project.Theme = _themeBeforePicker;
            Refresh();
        }

        ThemePicker.Visibility = Visibility.Collapsed;
        if (_project.Theme != _themeBeforePicker) Save();
        if (focusMore) MoreButton.Focus();

        _closingPicker = false;
    }

    // ---- Delete Sidequest -----------------------------------------------------------

    /// <summary>
    /// Deleting a Sidequest takes its items with it, including any objectives in the panel
    /// (5.6), and the images its Notas tab copied. v1 has no Bitácora yet, so nothing else
    /// goes, and the question does not mention one.
    /// </summary>
    private void DeleteSidequest_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is null) return;

        var count = _project.Items.Count;
        var question = count switch
        {
            0 => LanguageService.Format("L_DeleteSidequestEmptyQ", _project.Name),
            1 => LanguageService.Format("L_DeleteSidequestOneQ", _project.Name),
            _ => LanguageService.Format("L_DeleteSidequestQ", _project.Name, count),
        };

        if (!ConfirmDialog.Ask(owner, question, detail: string.Empty,
                confirmLabel: LanguageService.Get("L_DeleteSidequest")))
            return;

        App.Current.Projects.Remove(_project);
        Attachments.ReleaseMedia(new[] { _project }, App.Current.Projects);
        Save();
        _shell.ShowSidequests();
    }
}
