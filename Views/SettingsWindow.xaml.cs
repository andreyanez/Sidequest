using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using Sidequest.Models;
using Sidequest.Services;
using Matrix = System.Windows.Media.Matrix;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Point = System.Windows.Point;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

namespace Sidequest.Views;

/// <summary>
/// Settings (5.8), a window of its own. Every change applies and saves at once - there is
/// no OK button - and the panel's own pin and this window's switch stay in step.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Action _languageChanged;
    private readonly Action _settingsChanged;
    private bool _loading;

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = TrayIconFactory.WindowIcon();

        // Language names are written in their own language, so they need no strings.
        LanguagePicker.ItemsSource = new[]
        {
            new SegmentOption("Español", AppLanguage.Spanish),
            new SegmentOption("English", AppLanguage.English),
        };

        LoadAppearanceOptions();
        LoadSelections();

        // The appearance options are labelled in the current language, so a language switch
        // rebuilds them; the rest of the window is DynamicResource. Any other change only
        // moves selections - rebuilding the options then would destroy the one that has
        // keyboard focus while the arrow keys are moving through them.
        _languageChanged = () =>
        {
            LoadAppearanceOptions();
            LoadSelections();
        };
        _settingsChanged = LoadSelections;
        LanguageService.Changed += _languageChanged;
        App.Current.SettingsChanged += _settingsChanged;
        Closed += (_, _) =>
        {
            LanguageService.Changed -= _languageChanged;
            App.Current.SettingsChanged -= _settingsChanged;
        };

        SourceInitialized += (_, _) => CenterOnPanelsScreen();
    }

    private static AppSettings Settings => App.Current.Settings;

    private void LoadAppearanceOptions()
    {
        _loading = true;
        try
        {
            AppearancePicker.ItemsSource = new[]
            {
                new SegmentOption(LanguageService.Get("L_Light"), AppTheme.Light),
                new SegmentOption(LanguageService.Get("L_Dark"), AppTheme.Dark),
                new SegmentOption(LanguageService.Get("L_System"), null),
            };
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Shows the settings as they stand, without that counting as a change.</summary>
    private void LoadSelections()
    {
        _loading = true;
        try
        {
            AppearancePicker.SelectedItem = AppearancePicker.Items.Cast<SegmentOption>()
                .First(o => Settings.FollowSystemTheme ? o.Value is null : Equals(o.Value, Settings.Theme));

            LanguagePicker.SelectedItem = LanguagePicker.Items.Cast<SegmentOption>()
                .First(o => Equals(o.Value, LanguageService.Current));

            // The Run key, not the settings file, is the truth: it can be changed outside the
            // app, in Task Manager's Startup tab for one.
            StartupSwitch.IsChecked = AutoStart.IsEnabled();
            KeepOnTopSwitch.IsChecked = Settings.KeepOnTop;

            var version = typeof(App).Assembly.GetName().Version ?? new Version(0, 1);
            VersionText.Text = LanguageService.Format("L_VersionFmt", $"{version.Major}.{version.Minor}");
        }
        finally
        {
            _loading = false;
        }
    }

    private static void Save()
    {
        Storage.SaveSettings(Settings);
        App.Current.NotifySettingsChanged();
    }

    /// <summary>
    /// Claro and Oscuro fix the theme; Sistema follows Windows' own setting, now and when it
    /// changes. Either way every window repaints at once.
    /// </summary>
    private void Appearance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AppearancePicker.SelectedItem is not SegmentOption option) return;

        if (option.Value is AppTheme theme)
        {
            Settings.FollowSystemTheme = false;
            Settings.Theme = theme;
        }
        else
        {
            Settings.FollowSystemTheme = true;
        }

        ThemeService.Apply(App.Current.EffectiveTheme());
        Save();
    }

    /// <summary>Only the interface changes language (5.8); what the user wrote stays as written.</summary>
    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguagePicker.SelectedItem is not SegmentOption { Value: AppLanguage language }) return;
        if (language == LanguageService.Current) return;

        Settings.Language = language;
        LanguageService.Apply(language);
        Save();
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        try
        {
            AutoStart.Set(StartupSwitch.IsChecked == true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            // Policy can lock the Run key. The switch goes back to what is really there.
        }

        Settings.StartWithWindows = AutoStart.IsEnabled();
        Save();
    }

    /// <summary>Applies to the panel that is open now (5.8), and moves its pin with it.</summary>
    private void KeepOnTop_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        Settings.KeepOnTop = KeepOnTopSwitch.IsChecked == true;
        App.Current.ApplyKeepOnTop();
        Save();
    }

    private void StartupLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        StartupSwitch.IsChecked = StartupSwitch.IsChecked != true;

    private void KeepOnTopLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        KeepOnTopSwitch.IsChecked = KeepOnTopSwitch.IsChecked != true;

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Close();
        App.Current.ShowPanel();
    }

    private void Drag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try { DragMove(); }
        catch (InvalidOperationException) { /* released between the event and the call */ }
    }

    /// <summary>
    /// Centred on the screen the panel is on. Centring on the panel itself would push a
    /// 720px window off the edge whenever the panel sits in its usual corner.
    /// </summary>
    private void CenterOnPanelsScreen()
    {
        var anchor = Owner is null ? IntPtr.Zero : new WindowInteropHelper(Owner).Handle;
        var screen = anchor == IntPtr.Zero
            ? System.Windows.Forms.Screen.PrimaryScreen!
            : System.Windows.Forms.Screen.FromHandle(anchor);

        var area = screen.WorkingArea;
        var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = toDip.Transform(new Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));

        Left = topLeft.X + (bottomRight.X - topLeft.X - Width) / 2;
        Top = topLeft.Y + (bottomRight.Y - topLeft.Y - Height) / 2;
    }
}

/// <summary>One option of a segmented control (5.8): its label, and what choosing it means.</summary>
public sealed class SegmentOption
{
    public SegmentOption(string label, object? value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    /// <summary>An AppTheme, an AppLanguage - or null, for "Sistema".</summary>
    public object? Value { get; }

    public override string ToString() => Label;
}
