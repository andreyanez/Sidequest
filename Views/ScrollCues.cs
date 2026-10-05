using System.Windows;
using System.Windows.Controls;

namespace Sidequest.Views;

/// <summary>
/// 4.4's scroll cues: whether a scroller has content hidden above or below, for the
/// ScrollArea style to fade those edges. A ScrollViewer knows its offset but says nothing
/// a trigger can use, so this listens to ScrollChanged and keeps two flags on it.
/// </summary>
public static class ScrollCues
{
    public static readonly DependencyProperty TrackProperty = DependencyProperty.RegisterAttached(
        "Track", typeof(bool), typeof(ScrollCues), new PropertyMetadata(false, OnTrackChanged));

    private static readonly DependencyPropertyKey MoreAbovePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "MoreAbove", typeof(bool), typeof(ScrollCues), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey MoreBelowPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "MoreBelow", typeof(bool), typeof(ScrollCues), new PropertyMetadata(false));

    public static readonly DependencyProperty MoreAboveProperty = MoreAbovePropertyKey.DependencyProperty;
    public static readonly DependencyProperty MoreBelowProperty = MoreBelowPropertyKey.DependencyProperty;

    public static bool GetTrack(DependencyObject element) => (bool)element.GetValue(TrackProperty);
    public static void SetTrack(DependencyObject element, bool value) => element.SetValue(TrackProperty, value);

    public static bool GetMoreAbove(DependencyObject element) => (bool)element.GetValue(MoreAboveProperty);
    public static bool GetMoreBelow(DependencyObject element) => (bool)element.GetValue(MoreBelowProperty);

    private static void OnTrackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scroller) return;

        scroller.ScrollChanged -= Update;
        if ((bool)e.NewValue) scroller.ScrollChanged += Update;
    }

    private static void Update(object sender, ScrollChangedEventArgs e)
    {
        var scroller = (ScrollViewer)sender;

        // Half a pixel of slack: layout rounding can leave the offset a hair short of the end.
        scroller.SetValue(MoreAbovePropertyKey, scroller.VerticalOffset > 0.5);
        scroller.SetValue(MoreBelowPropertyKey, scroller.VerticalOffset < scroller.ScrollableHeight - 0.5);
    }
}
