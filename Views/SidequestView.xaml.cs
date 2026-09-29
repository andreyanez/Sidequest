using System.Windows;
using System.Windows.Controls.Primitives;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
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
/// A Sidequest's detail (5.6): its band, its items in three fixed groups, and adding
/// items in place. Every change saves at once; there is no save button anywhere.
/// </summary>
public partial class SidequestView : UserControl
{
    /// <summary>How long a press on the title has to be held to rename it (5.6).</summary>
    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(500);

    private readonly MainWindow _shell;
    private readonly SidequestProject _project;
    private readonly bool _justCreated;
    private readonly Action _refresh;
    private readonly DispatcherTimer _pressTimer;

    private SidequestTheme _themeBeforePicker;
    private bool _closingPicker;

    public SidequestView(MainWindow shell, SidequestProject project, bool justCreated = false)
    {
        InitializeComponent();
        _shell = shell;
        _project = project;
        _justCreated = justCreated;

        _pressTimer = new DispatcherTimer { Interval = LongPress };
        _pressTimer.Tick += (_, _) =>
        {
            _pressTimer.Stop();
            BeginRename();
        };

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
        };
    }

    private static void Save() => SidequestStore.Save(App.Current.Projects);

    private void Refresh(bool rebuildSwatches = false)
    {
        DataContext = new SidequestHeader(_project);
        ItemList.ItemsSource = SidequestRules.DetailOrder(_project)
            .Select(item => DetailRow.For(item, _project))
            .ToList();

        if (rebuildSwatches && ThemePicker.Visibility == Visibility.Visible)
        {
            ThemePicker.ItemsSource = ThemeSwatch.All();
            SelectSwatch(_project.Theme);
        }
    }

    private List<SidequestItem> PendingItems() =>
        SidequestRules.DetailOrder(_project).Where(i => SidequestRules.GroupOf(i) == ItemGroup.Pending).ToList();

    /// <summary>
    /// After an item leaves the pending group, keyboard focus goes to the pending item that
    /// took its place, or the one before it, or failing both the add button - never back
    /// to the top of the window.
    /// </summary>
    private void FocusPendingNear(int index)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var pending = PendingItems();
            if (pending.Count == 0)
            {
                AddButton.Focus();
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
                b.DataContext is PendingItemRow row && row.Item == item && b.Name != "DeleteButton");
            button?.Focus();
        });
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

    /// <summary>Supr on a pending row deletes it, as it does on the panel.</summary>
    private void ItemList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        if (Keyboard.FocusedElement is not FrameworkElement { DataContext: PendingItemRow row }) return;

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

    // ---- Add item (5.6) -------------------------------------------------------------

    private void AddButton_Click(object sender, RoutedEventArgs e) => OpenAddField();

    private void OpenAddField()
    {
        AddButton.Visibility = Visibility.Collapsed;
        AddField.Visibility = Visibility.Visible;
        AddBox.Text = string.Empty;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddBox.Focus());
    }

    private void CloseAddField(bool focusButton)
    {
        AddField.Visibility = Visibility.Collapsed;
        AddButton.Visibility = Visibility.Visible;
        AddBox.Text = string.Empty;

        if (focusButton) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddButton.Focus());
    }

    /// <summary>Enter adds and leaves the field open for the next one; Escape closes it.</summary>
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
        Refresh();
        AddBox.Text = string.Empty;

        // The new item joins the end of the pending group, which can be scrolled out of
        // sight above the completed ones.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var row = VisualTree.FindDescendant<FrameworkElement>(ItemList, f =>
                f.DataContext is DetailRow r && r.Item == item);
            row?.BringIntoView();
        });
    }

    private void AddBox_TextChanged(object sender, TextChangedEventArgs e) =>
        AddHint.Visibility = AddBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Clicking away closes an empty field; one with text in it stays open.</summary>
    private void AddBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
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

    // ---- Rename (5.6) ---------------------------------------------------------------

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

    private void NameEdit_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        EndRename(commit: true);

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
    /// (5.6). v1 has no Bitácora yet, so nothing else goes, and the question does not
    /// mention one.
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
        Save();
        _shell.ShowSidequests();
    }
}
