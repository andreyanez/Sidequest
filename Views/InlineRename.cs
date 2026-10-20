using System.Windows;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using ItemsControl = System.Windows.Controls.ItemsControl;
using Key = System.Windows.Input.Key;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using Visual = System.Windows.Media.Visual;

namespace Sidequest.Views;

/// <summary>
/// Renaming in place, as 5.1 sets it out for the panel and 5.6 repeats for a Sidequest's
/// items: the row becomes a field with its text selected; Enter, the check or leaving the
/// row saves, Escape or the x cancels, and a blank name is not saved. F2 on a focused row
/// starts it, as the pencil does.
///
/// The panel's cards and the detail's rows share it. Their templates name the parts it
/// looks for: "Card", the row; "NameEdit", the field; and "RenameButton", the pencil,
/// where keyboard focus goes back to.
/// </summary>
internal sealed class InlineRename
{
    private readonly ItemsControl _list;
    private ItemRow? _row;

    public InlineRename(ItemsControl list)
    {
        _list = list;
    }

    public void Begin(ItemRow row)
    {
        if (_row == row || !row.CanRename) return;
        Commit(focusRow: false);

        _row = row;
        row.IsEditing = true;

        // The field is only shown once the template has applied the change.
        _list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_row != row || Part<TextBox>(row, "NameEdit") is not { } field) return;

            field.Text = row.Item.Text;
            field.Focus();
            field.SelectAll();
        });
    }

    /// <summary>Keeps the field's text when it is a name, and ends editing.</summary>
    public void Commit(bool focusRow)
    {
        if (_row is not { } row) return;

        if (Part<TextBox>(row, "NameEdit") is { } field && SidequestRules.RenameItem(row.Item, field.Text))
            SidequestStore.Save(App.Current.Projects);

        End(row, focusRow);
    }

    public void Cancel(bool focusRow)
    {
        if (_row is { } row) End(row, focusRow);
    }

    /// <summary>F2 on a focused row, from the list's KeyDown.</summary>
    public bool TryBeginFromKey(KeyEventArgs e)
    {
        if (e.Key != Key.F2) return false;
        if (e.OriginalSource is not FrameworkElement { DataContext: ItemRow row } || row.IsEditing || !row.CanRename)
            return false;

        Begin(row);
        return true;
    }

    /// <summary>The field's Enter and Escape.</summary>
    public void FieldKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Commit(focusRow: true);
        else if (e.Key == Key.Escape) Cancel(focusRow: true);
        else return;

        e.Handled = true;
    }

    /// <summary>
    /// 5.1: line breaks in pasted text become spaces, since a name is one line, however
    /// many the field wraps it over. Left alone, a TextBox that doesn't accept Return
    /// keeps only the first line of what is pasted.
    /// </summary>
    public static void Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string text) return;

        var line = SidequestRules.OneLine(text);
        if (line == text) return;

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, line);
        e.DataObject = data;
        e.FormatToApply = DataFormats.UnicodeText;
    }

    /// <summary>
    /// Focus leaving the row saves (5.1). Moving between the field and the row's own
    /// check and x is not leaving: those buttons have to be reachable to be pressed. Nor
    /// is focus going nowhere, which is the window losing it to another app: the new name
    /// may be being copied from there.
    /// </summary>
    public void FocusLeaving(KeyboardFocusChangedEventArgs e)
    {
        if (_row is not { } row || Part<FrameworkElement>(row, "Card") is not { } card) return;
        if (e.NewFocus is null || (e.NewFocus is Visual next && card.IsAncestorOf(next))) return;

        Commit(focusRow: false);
    }

    /// <summary>
    /// A click anywhere else saves too. Clicking a spot that cannot take focus, such as
    /// another row's text, leaves keyboard focus in the field, so focus alone misses it.
    /// </summary>
    public void PointerDown(MouseButtonEventArgs e)
    {
        if (_row is not { } row || Part<FrameworkElement>(row, "Card") is not { } card) return;
        if (e.OriginalSource is Visual target && card.IsAncestorOf(target)) return;

        Commit(focusRow: false);
    }

    private void End(ItemRow row, bool focusRow)
    {
        _row = null;
        row.IsEditing = false;

        // The field still has focus at this point, so the row still counts as focused and
        // its hover actions are back already: the pencil that started this can take it.
        if (focusRow) Part<Button>(row, "RenameButton")?.Focus();
    }

    private T? Part<T>(ItemRow row, string name) where T : FrameworkElement =>
        VisualTree.FindDescendant<T>(_list, e => e.Name == name && e.DataContext == row);
}
