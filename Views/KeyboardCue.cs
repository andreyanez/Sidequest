using System.Windows;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using InputManager = System.Windows.Input.InputManager;
using KeyboardDevice = System.Windows.Input.KeyboardDevice;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Visual = System.Windows.Media.Visual;

namespace Sidequest.Views;

/// <summary>
/// Keyboard focus that the keyboard put there: CSS's :focus-visible. 4.4 shows a card's
/// grip, its actions and its hover fill on hover or on keyboard focus, but WPF's
/// IsKeyboardFocusWithin is just as true after a click - a ticked checkbox, a dropped
/// card - so they stayed after the pointer had gone. On an element with
/// <c>KeyboardCue.Track</c>, <c>KeyboardCue.Within</c> is true only while the focus inside
/// it arrived by keyboard, and goes false as soon as the pointer presses inside it.
/// </summary>
public static class KeyboardCue
{
    public static readonly DependencyProperty TrackProperty = DependencyProperty.RegisterAttached(
        "Track", typeof(bool), typeof(KeyboardCue), new PropertyMetadata(false, OnTrackChanged));

    private static readonly DependencyPropertyKey WithinKey = DependencyProperty.RegisterAttachedReadOnly(
        "Within", typeof(bool), typeof(KeyboardCue), new PropertyMetadata(false));

    public static readonly DependencyProperty WithinProperty = WithinKey.DependencyProperty;

    public static bool GetTrack(DependencyObject element) => (bool)element.GetValue(TrackProperty);

    public static void SetTrack(DependencyObject element, bool value) => element.SetValue(TrackProperty, value);

    public static bool GetWithin(DependencyObject element) => (bool)element.GetValue(WithinProperty);

    private static void OnTrackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.GotKeyboardFocus -= Got;
        element.LostKeyboardFocus -= Lost;
        element.PreviewMouseDown -= Pressed;
        if (e.NewValue is not true) return;

        element.GotKeyboardFocus += Got;
        element.LostKeyboardFocus += Lost;
        element.PreviewMouseDown += Pressed;
    }

    /// <summary>
    /// Focus arriving anywhere inside: shown if the keyboard sent it - Tab, the arrows, or
    /// an Enter whose action moved it - and not if a click or the pointer's drag did.
    /// </summary>
    private static void Got(object sender, KeyboardFocusChangedEventArgs e) =>
        ((UIElement)sender).SetValue(WithinKey, InputManager.Current.MostRecentInputDevice is KeyboardDevice);

    /// <summary>Moving within is not leaving; Got decides again for the new place.</summary>
    private static void Lost(object sender, KeyboardFocusChangedEventArgs e)
    {
        var element = (UIElement)sender;
        if (e.NewFocus is Visual next && element.IsAncestorOf(next)) return;

        element.SetValue(WithinKey, false);
    }

    private static void Pressed(object sender, MouseButtonEventArgs e) =>
        ((UIElement)sender).SetValue(WithinKey, false);
}
