using System.Globalization;
using System.Windows;
using System.Windows.Media;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Brushes = System.Windows.Media.Brushes;
using InputManager = System.Windows.Input.InputManager;
using KeyboardDevice = System.Windows.Input.KeyboardDevice;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using PlacementMode = System.Windows.Controls.Primitives.PlacementMode;
using TextBlock = System.Windows.Controls.TextBlock;
using ToolTip = System.Windows.Controls.ToolTip;
using ToolTipEventArgs = System.Windows.Controls.ToolTipEventArgs;
using ToolTipService = System.Windows.Controls.ToolTipService;

namespace Sidequest.Views;

/// <summary>
/// 5.1: an objective's title cut short by its card's two lines shows whole in a tooltip,
/// under the card as FnMainOverflowB draws it, and a title that fits shows none. It comes
/// up on hovering the title, through the title's own ToolTip and <c>ToolTipOpening</c>,
/// and when the card takes keyboard focus, on the stops that carry
/// <c>TitleTip.OnKeyboardFocus</c>.
///
/// The title takes no focus itself, so WPF's tooltip on keyboard focus never shows it:
/// one tooltip of its own, shared by every card, comes up for those stops instead. The
/// card is the ancestor named "Card", and its title the descendant named "ItemText".
/// </summary>
public static class TitleTip
{
    /// <summary>
    /// FnMainOverflowB puts the box 53 in from the card's left edge and 3 below it. The
    /// implicit ToolTip's box sits 18 and 12 inside its popup, room for its shadow.
    /// </summary>
    private const double Left = 53 - 18;
    private const double Below = 3 - 12;

    /// <summary>
    /// The box is 280 wide however long the lines, where the implicit ToolTip's fits its
    /// text: 280 less its padding of 10 and border of 1 each side.
    /// </summary>
    private const double TextWidth = 280 - 2 * 10 - 2 * 1;

    public static readonly DependencyProperty OnKeyboardFocusProperty = DependencyProperty.RegisterAttached(
        "OnKeyboardFocus", typeof(bool), typeof(TitleTip), new PropertyMetadata(false, OnKeyboardFocusChanged));

    private static readonly ToolTip KeyboardTip = new()
    {
        Placement = PlacementMode.Bottom,
        HorizontalOffset = Left,
        VerticalOffset = Below,
    };

    public static bool GetOnKeyboardFocus(DependencyObject element) => (bool)element.GetValue(OnKeyboardFocusProperty);

    public static void SetOnKeyboardFocus(DependencyObject element, bool value) => element.SetValue(OnKeyboardFocusProperty, value);

    /// <summary>The title's ToolTipOpening: the whole title under its card, or nothing if it fits.</summary>
    public static void Opening(object sender, ToolTipEventArgs e)
    {
        if (sender is not TextBlock { ToolTip: ToolTip tip } title || Ancestor(title, "Card") is not { } card || !IsCut(title))
        {
            e.Handled = true;
            return;
        }

        tip.Content = Text(title);

        // The service places a tooltip by its owner's settings - here the title's - and
        // left alone it puts it under the title rather than the card.
        ToolTipService.SetPlacement(title, PlacementMode.Bottom);
        ToolTipService.SetPlacementTarget(title, card);
        ToolTipService.SetHorizontalOffset(title, Left);
        ToolTipService.SetVerticalOffset(title, Below);
    }

    /// <summary>
    /// Whether the title's text runs past what it shows. WPF does not say whether a
    /// TextBlock trimmed its text, so the text is laid out again at the title's width,
    /// with no limit on its height, and measured.
    /// </summary>
    public static bool IsCut(TextBlock title)
    {
        if (title.ActualWidth <= 0 || string.IsNullOrEmpty(title.Text)) return false;

        var text = new FormattedText(
            title.Text,
            CultureInfo.CurrentCulture,
            title.FlowDirection,
            new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch),
            title.FontSize,
            Brushes.Black,
            null,
            TextOptions.GetTextFormattingMode(title),
            VisualTreeHelper.GetDpi(title).PixelsPerDip)
        {
            MaxTextWidth = title.ActualWidth,
            LineHeight = title.LineHeight,
        };

        return text.Height > title.ActualHeight + 0.5;
    }

    private static TextBlock Text(TextBlock title) =>
        new() { Text = title.Text, Width = TextWidth, TextWrapping = TextWrapping.Wrap };

    private static void OnKeyboardFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.GotKeyboardFocus -= Got;
        element.LostKeyboardFocus -= Lost;
        if (e.NewValue is not true) return;

        element.GotKeyboardFocus += Got;
        element.LostKeyboardFocus += Lost;
    }

    /// <summary>Focus the keyboard sent, as KeyboardCue tells it, not a click's.</summary>
    private static void Got(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice) return;
        if (Ancestor((DependencyObject)sender, "Card") is not { } card) return;
        if (VisualTree.FindDescendant<TextBlock>(card, t => t.Name == "ItemText") is not { } title || !IsCut(title)) return;

        KeyboardTip.Content = Text(title);
        KeyboardTip.PlacementTarget = card;
        KeyboardTip.IsOpen = true;
    }

    private static void Lost(object sender, KeyboardFocusChangedEventArgs e) => KeyboardTip.IsOpen = false;

    private static FrameworkElement? Ancestor(DependencyObject element, string name)
    {
        for (var e = element; e is not null; e = VisualTreeHelper.GetParent(e))
            if (e is FrameworkElement f && f.Name == name) return f;
        return null;
    }
}
