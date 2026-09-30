using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// Swaps the active theme dictionary at runtime. Slot 0 of the application's merged
/// dictionaries is reserved for it, so every DynamicResource colour repaints without
/// reopening windows.
/// </summary>
public static class ThemeService
{
    // Supported from Windows 10 20H1; earlier builds used 19.
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>
    /// Raised after a switch. DynamicResource covers everything drawn from the token
    /// dictionaries, but a Sidequest's colour is resolved in code from two axes, so
    /// anything showing one has to re-resolve it here.
    /// </summary>
    public static event Action? Changed;

    // ---- Windows' own light and dark (5.8's "Sistema", 5.9's tray icon) ------------------

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Raised on the UI thread when Windows' light/dark setting for apps or the taskbar changes.</summary>
    public static event Action? SystemChanged;

    /// <summary>What "Sistema" follows: Windows' setting for apps. Light if it cannot be read.</summary>
    public static AppTheme SystemTheme() =>
        ReadPersonalize("AppsUseLightTheme", fallback: true) ? AppTheme.Light : AppTheme.Dark;

    /// <summary>
    /// The taskbar's own setting, which Windows keeps apart from the apps' one: a light
    /// app theme often sits on a dark taskbar. The tray icon has to read against this.
    /// </summary>
    public static bool TaskbarIsLight() => ReadPersonalize("SystemUsesLightTheme", fallback: false);

    /// <summary>Starts relaying Windows' theme changes as <see cref="SystemChanged"/>.</summary>
    public static void WatchSystem()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void StopWatchingSystem() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // A light/dark switch arrives as General; so does plenty else, and the handlers
        // compare before acting, so a spurious one costs nothing.
        if (e.Category != UserPreferenceCategory.General) return;

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => SystemChanged?.Invoke());
    }

    private static bool ReadPersonalize(string name, bool fallback)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(name) is int value ? value != 0 : fallback;
    }

    public static void Apply(AppTheme theme)
    {
        Current = theme;

        var source = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0) merged.Add(dictionary);
        else merged[0] = dictionary;

        foreach (Window window in System.Windows.Application.Current.Windows)
            ApplyTitleBar(window);

        Changed?.Invoke();
    }

    /// <summary>
    /// Keeps a window's non-client title bar in step with the theme. WPF does not paint
    /// the title bar, so without this a dark window keeps a white caption.
    /// </summary>
    public static void TrackTitleBar(Window window)
    {
        // Setting the attribute at SourceInitialized succeeds but the caption is not
        // repainted, so it also has to be applied once the window has actually rendered.
        window.SourceInitialized += (_, _) => ApplyTitleBar(window);
        window.ContentRendered += (_, _) => ApplyTitleBar(window);

        if (window.IsLoaded) ApplyTitleBar(window);
    }

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        var value = Current == AppTheme.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref value, sizeof(int));

        // The attribute alone does not always repaint the caption - reverting to light
        // in particular leaves the dark bar on screen. Nudge the frame to redraw.
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }
}
