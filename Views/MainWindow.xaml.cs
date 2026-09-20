using System.Windows;
using Sidequest.Services;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;

namespace Sidequest.Views;

/// <summary>
/// The compact 400px shell. Panels 1-4 swap inside it; the Bitacora and Settings open as
/// their own windows.
///
/// It is created once and hidden rather than closed, so position and collapsed state
/// survive, and so a window owned by it cannot be closed out from under the user.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>How much must stay on screen for a saved position to be reused.</summary>
    private const double MinVisible = 120;

    /// <summary>5.1: the panel grows until this much of the work area is left.</summary>
    private const double ScreenPadding = 48;

    private readonly PanelView _panel = new();
    private readonly Action _refresh;
    private bool _restoring;

    public MainWindow()
    {
        InitializeComponent();

        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - ScreenPadding);
        ViewHost.Content = _panel;

        _restoring = true;
        PinButton.IsChecked = App.Current.Settings.KeepOnTop;
        Topmost = App.Current.Settings.KeepOnTop;
        ApplyCollapsed(App.Current.Settings.Collapsed, pinBottomEdge: false);
        _restoring = false;

        _refresh = RefreshCollapsedRow;
        ThemeService.Changed += _refresh;
        LanguageService.Changed += _refresh;

        Loaded += (_, _) => RestorePosition();
        SizeChanged += (_, _) => ClampToVirtualScreen();
        Closed += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
        };
    }

    /// <summary>Brings the panel back without rebuilding it, so nothing is lost.</summary>
    public void ShowPanel()
    {
        _panel.Refresh();
        RefreshCollapsedRow();

        if (!IsVisible) Show();
        Activate();
    }

    // ---- Title bar ----------------------------------------------------------------

    private void Drag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return; // released between the event and the call
        }

        SavePosition();
    }

    private void Pin_Changed(object sender, RoutedEventArgs e)
    {
        if (_restoring) return;

        App.Current.Settings.KeepOnTop = PinButton.IsChecked == true;
        Topmost = App.Current.Settings.KeepOnTop;
        Storage.SaveSettings(App.Current.Settings);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
    {
        ApplyCollapsed(!App.Current.Settings.Collapsed, pinBottomEdge: true);

        Storage.SaveSettings(App.Current.Settings);
    }

    /// <summary>5.1: closing hides the panel. The app lives on in the tray.</summary>
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    // ---- Collapse -----------------------------------------------------------------

    private void ApplyCollapsed(bool collapsed, bool pinBottomEdge)
    {
        var bottom = Top + ActualHeight;

        App.Current.Settings.Collapsed = collapsed;
        ExpandedRoot.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedRoot.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;

        // The collapsed row is a fixed 50; the expanded panel sizes to its content.
        MinHeight = collapsed ? 0 : 384;
        RefreshCollapsedRow();

        if (!pinBottomEdge) return;

        // Roll up in place: a panel resting on the bottom of the screen should not jump.
        UpdateLayout();
        Top = bottom - ActualHeight;
        ClampToVirtualScreen();
    }

    private void RefreshCollapsedRow()
    {
        var (done, total) = _panel.Counts();
        CollapsedDate.Text = PanelView.FullDateText();
        CollapsedCounter.Text = LanguageService.Format("L_CounterFmt", done, total);
    }

    // ---- Position -----------------------------------------------------------------

    private void RestorePosition()
    {
        var settings = App.Current.Settings;

        if (settings.MainLeft is double left && settings.MainTop is double top && IsReachable(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth;
            Top = area.Bottom - ActualHeight;
        }

        ClampToVirtualScreen();
    }

    private void SavePosition()
    {
        App.Current.Settings.MainLeft = Left;
        App.Current.Settings.MainTop = Top;
        Storage.SaveSettings(App.Current.Settings);
    }

    /// <summary>True when enough of the window would land on the desktop to grab it.</summary>
    private bool IsReachable(double left, double top)
    {
        var vLeft = SystemParameters.VirtualScreenLeft;
        var vTop = SystemParameters.VirtualScreenTop;
        var vRight = vLeft + SystemParameters.VirtualScreenWidth;
        var vBottom = vTop + SystemParameters.VirtualScreenHeight;

        return left + ActualWidth - MinVisible > vLeft
            && left + MinVisible < vRight
            && top + ActualHeight - MinVisible > vTop
            && top + MinVisible < vBottom;
    }

    /// <summary>
    /// Pulls the window back inside the desktop. The panel's height follows its contents,
    /// so a position that fitted yesterday can hang off an edge today.
    /// </summary>
    private void ClampToVirtualScreen()
    {
        var vLeft = SystemParameters.VirtualScreenLeft;
        var vTop = SystemParameters.VirtualScreenTop;
        var vRight = vLeft + SystemParameters.VirtualScreenWidth;
        var vBottom = vTop + SystemParameters.VirtualScreenHeight;

        if (Left + ActualWidth > vRight) Left = Math.Max(vLeft, vRight - ActualWidth);
        if (Top + ActualHeight > vBottom) Top = Math.Max(vTop, vBottom - ActualHeight);
    }
}
