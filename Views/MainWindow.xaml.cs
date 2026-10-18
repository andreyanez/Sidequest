using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using Sidequest.Models;
using Sidequest.Services;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using DragCompletedEventArgs = System.Windows.Controls.Primitives.DragCompletedEventArgs;
using DragDeltaEventArgs = System.Windows.Controls.Primitives.DragDeltaEventArgs;
using DragStartedEventArgs = System.Windows.Controls.Primitives.DragStartedEventArgs;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WindowInteropHelper = System.Windows.Interop.WindowInteropHelper;
using FocusNavigationDirection = System.Windows.Input.FocusNavigationDirection;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using TextBlock = System.Windows.Controls.TextBlock;
using TraversalRequest = System.Windows.Input.TraversalRequest;

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

    private readonly PanelView _panel;
    private readonly Action _refresh;
    private Action? _back;
    private bool _restoring;

    public MainWindow()
    {
        InitializeComponent();

        _panel = new PanelView(this);
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

    /// <summary>
    /// The app's front door (tray, relaunch). Brings the window back without navigating:
    /// it is only ever hidden from the main panel, since sub-panels have no close button,
    /// and a visible sub-panel may be holding something half-typed.
    /// </summary>
    public void ShowPanel()
    {
        _panel.Refresh();
        RefreshCollapsedRow();

        if (!IsVisible) Show();
        Activate();
    }

    // ---- Navigation (5) --------------------------------------------------------------

    /// <summary>
    /// Back to the main panel, rebuilt from the model since a sub-panel may have sent,
    /// deleted or ticked things. Every way back arrives from the Sidequests list, so focus
    /// lands on the button that leads there.
    /// </summary>
    public void ShowMainPanel()
    {
        Navigate(_panel, backKey: null, back: null);
        _panel.Refresh();
        _panel.FocusSidequestsButton();
    }

    /// <param name="focus">The Sidequest to put keyboard focus on, when coming back from it.</param>
    public void ShowSidequests(SidequestProject? focus = null)
    {
        // The tray's "Sidequests…" can arrive while the panel is rolled up, and the list
        // lives in the expanded panel.
        if (App.Current.Settings.Collapsed)
        {
            ApplyCollapsed(false, pinBottomEdge: true);
            Storage.SaveSettings(App.Current.Settings);
        }

        Navigate(new SidequestsView(this, focus), "L_MainPanel", ShowMainPanel);
    }

    /// <param name="justCreated">Opens the add-item field straight away, since an empty
    /// Sidequest's next step is always its first item.</param>
    public void ShowSidequest(SidequestProject project, bool justCreated = false) =>
        Navigate(new SidequestView(this, project, justCreated), "L_Sidequests", () => ShowSidequests(project));

    private void Navigate(FrameworkElement view, string? backKey, Action? back)
    {
        _back = back;
        ViewHost.Content = view;

        var isSubPanel = backKey is not null;
        PanelBar.Visibility = isSubPanel ? Visibility.Collapsed : Visibility.Visible;
        SubBar.Visibility = isSubPanel ? Visibility.Visible : Visibility.Collapsed;

        if (isSubPanel)
        {
            BackText.SetResourceReference(TextBlock.TextProperty, backKey);
            BackButton.SetResourceReference(AutomationProperties.NameProperty, backKey);
        }

        // Keyboard focus would otherwise stay on a control that has just left the tree,
        // and the next Tab would start from the top of the window. A view that wants a
        // particular control focused does so on Loaded, which runs before this.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (ViewHost.Content == view && !view.IsKeyboardFocusWithin)
                view.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        });
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _back?.Invoke();

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

        // Settings shows the same choice as a switch.
        App.Current.NotifySettingsChanged();
    }

    /// <summary>Settings changed "siempre visible" (5.8): the panel follows, pin included.</summary>
    public void SyncKeepOnTop()
    {
        _restoring = true;
        PinButton.IsChecked = App.Current.Settings.KeepOnTop;
        Topmost = App.Current.Settings.KeepOnTop;
        _restoring = false;
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
        ResizeEdge.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        RefreshCollapsedRow();

        // 5: the height the user chose, the same on every screen. The collapsed row
        // ignores it, and expanding brings it back.
        Height = collapsed ? CollapsedWindowHeight : ClampCard(App.Current.Settings.MainHeight) + Shadow;

        if (!pinBottomEdge) return;

        // Roll up in place: a panel resting on the bottom of the screen should not jump.
        Top = bottom - Height;
        ClampToVirtualScreen();
    }

    // ---- Height (5) ---------------------------------------------------------------

    /// <summary>5's least height for the card.</summary>
    private const double MinCardHeight = 360;

    /// <summary>The shadow's margin, which the window adds around the card.</summary>
    private double Shadow => Card.Margin.Top + Card.Margin.Bottom;

    /// <summary>
    /// 5's greatest: the work area's height. Less the shadow, so the whole window, shadow
    /// and all, still fits on the screen.
    /// </summary>
    private double MaxCardHeight => Math.Max(MinCardHeight, SystemParameters.WorkArea.Height - Shadow);

    private double ClampCard(double height) => Math.Clamp(height, MinCardHeight, MaxCardHeight);

    private double CollapsedWindowHeight =>
        CollapsedRoot.Height + Card.BorderThickness.Top + Card.BorderThickness.Bottom + Shadow;

    // The drag on the top edge, in the device pixels Windows places windows in: the
    // window's rectangle and the pointer's height when it began, and the screen's scale.
    private Native.Rect _resizeFrom;
    private int _resizePointerY;
    private double _resizeScale = 1;

    private void ResizeEdge_DragStarted(object sender, DragStartedEventArgs e)
    {
        Native.GetWindowRect(new WindowInteropHelper(this).Handle, out _resizeFrom);
        Native.GetCursorPos(out var pointer);
        _resizePointerY = pointer.Y;
        _resizeScale = VisualTreeHelper.GetDpi(this).DpiScaleY;
    }

    /// <summary>
    /// The top edge follows the pointer and the bottom stays put, as it does when the
    /// panel collapses, so the card grows upward, as far as the top of the work area. The
    /// top and the height change in one call: set one after the other, as Top and Height
    /// would be, the bottom edge would jump for a frame between them.
    /// </summary>
    private void ResizeEdge_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Native.GetCursorPos(out var pointer);
        var from = _resizeFrom;
        var scale = _resizeScale;

        var cardBottom = from.Bottom / scale - Card.Margin.Bottom;
        var roomAbove = cardBottom - SystemParameters.WorkArea.Top;
        var card = (from.Bottom - from.Top) / scale - Shadow + (_resizePointerY - pointer.Y) / scale;
        card = Math.Clamp(card, MinCardHeight, Math.Max(MinCardHeight, Math.Min(MaxCardHeight, roomAbove)));

        var height = (int)Math.Round((card + Shadow) * scale);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
            from.Left, from.Bottom - height, from.Right - from.Left, height,
            Native.NoZOrder | Native.NoActivate);
    }

    /// <summary>
    /// 5: the height is saved once, on letting go, and the position with it, since the
    /// top moved. ActualHeight rather than Height: Windows can stop a window short of
    /// what was asked, near the screen's full height.
    /// </summary>
    private void ResizeEdge_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        Height = ActualHeight;
        App.Current.Settings.MainHeight = ActualHeight - Shadow;
        SavePosition();
    }

    private static class Native
    {
        public const uint NoZOrder = 0x0004;
        public const uint NoActivate = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X, Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }

    private void RefreshCollapsedRow()
    {
        var (done, total) = _panel.Counts();
        CollapsedDate.Text = PanelView.FullDateText();
        CollapsedCounter.Text = LanguageService.Format("L_CounterFmt", done, total);
    }

    // ---- Position -----------------------------------------------------------------

    /// <summary>The tray's "Restablecer posición": forget the dragged spot, back to the corner.</summary>
    public void ResetPosition()
    {
        App.Current.Settings.MainLeft = null;
        App.Current.Settings.MainTop = null;
        Storage.SaveSettings(App.Current.Settings);

        RestorePosition();
        ShowPanel();
    }

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
