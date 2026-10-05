// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using TabControl = System.Windows.Controls.TabControl;
using TabItem = System.Windows.Controls.TabItem;

namespace Sidequest.Views;

/// <summary>
/// A Sidequest's Items and Notas tabs (5.6). Only the chosen tab is a tab stop (the
/// SidequestTab style says why), so from a focused tab the arrow keys, Home and End move
/// between tabs, taking the selection along. From anywhere else Home and End are left to
/// what has focus: the stock control takes them from anywhere inside it, and End on a
/// button in the Items tab would jump to Notas.
/// </summary>
public sealed class SidequestTabControl : TabControl
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.OriginalSource is TabItem && Keyboard.Modifiers == ModifierKeys.None && Next(e.Key) is int index)
        {
            SelectedIndex = index;
            (ItemContainerGenerator.ContainerFromIndex(index) as TabItem)?.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Home or Key.End) return;

        // Ctrl+Tab and Ctrl+Shift+Tab, from anywhere inside.
        base.OnKeyDown(e);
    }

    private int? Next(Key key)
    {
        var last = Items.Count - 1;
        return key switch
        {
            Key.Home => 0,
            Key.End => last,
            Key.Left => SelectedIndex <= 0 ? last : SelectedIndex - 1,
            Key.Right => SelectedIndex >= last ? 0 : SelectedIndex + 1,
            _ => null,
        };
    }
}
