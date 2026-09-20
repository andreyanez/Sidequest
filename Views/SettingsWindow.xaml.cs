using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
// Alias the handful of types we touch rather than fully qualifying at each use.
using CheckBox = System.Windows.Controls.CheckBox;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Sidequest.Views;

public partial class SettingsWindow : Window
{
    /// <summary>Wrapper so the list can bind to a preformatted string without XAML format escaping.</summary>
    // ToString is overridden throughout: a templated ComboBoxItem takes its accessible
    // name from the bound object, and the record default reads "Choice { Label = ... }".
    public sealed record TimeEntry(TimeOnly Value)
    {
        public string Display => Format(Value);

        public override string ToString() => Display;
    }

    public sealed record Choice(string Label, int Value)
    {
        public override string ToString() => Label;
    }

    public sealed record LanguageChoice(string Label, AppLanguage Value)
    {
        public override string ToString() => Label;
    }

    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private readonly AppSettings _settings;
    private readonly ObservableCollection<TimeEntry> _times = new();
    private readonly Action _languageChanged;
    private bool _loading = true;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;

        InitializeComponent();

        foreach (var time in settings.PopupTimes.OrderBy(t => t))
            _times.Add(new TimeEntry(time));
        TimeList.ItemsSource = _times;

        // Language names are endonyms, so this list is not rebuilt on a language change.
        LanguageBox.ItemsSource = new List<LanguageChoice>
        {
            new("English", AppLanguage.English),
            new("Español", AppLanguage.Spanish),
        };
        LanguageBox.SelectedValue = settings.Language ?? LanguageService.Current;

        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        KeepOnTopBox.IsChecked = settings.KeepOnTop;
        DarkModeBox.IsChecked = settings.Theme == AppTheme.Dark;

        ApplyStrings();
        ThemeService.TrackTitleBar(this);

        _loading = false;

        _languageChanged = ApplyStrings;
        LanguageService.Changed += _languageChanged;

        // Controls receiving focus during load can scroll themselves into view, which
        // left the window opening part-way down with the first popup time cut off.
        Loaded += (_, _) => ContentScroll.ScrollToTop();
    }

    /// <summary>
    /// Rebuilds everything this window renders from code: day names and duration labels
    /// are culture- and language-dependent, so a switch has to regenerate them.
    /// </summary>
    private void ApplyStrings()
    {
        var wasLoading = _loading;
        _loading = true;

        BuildDayToggles();
        BuildDurationChoices();
        TimeHint.Text = LanguageService.Get("L_TimeHintDefault");

        _loading = wasLoading;
    }

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Keeps a stored value selectable even if it isn't one of the presets.</summary>
    private static List<Choice> WithCurrent(List<Choice> choices, int current, Func<int, string> label)
    {
        if (choices.All(c => c.Value != current))
            choices.Add(new Choice(label(current), current));

        return choices.OrderBy(c => c.Value).ToList();
    }

    private void BuildDurationChoices()
    {
        SnoozeBox.ItemsSource = WithCurrent(new List<Choice>
        {
            new(LanguageService.Format("L_MinutesFmt", 5), 5),
            new(LanguageService.Format("L_MinutesFmt", 10), 10),
            new(LanguageService.Format("L_MinutesFmt", 15), 15),
            new(LanguageService.Format("L_MinutesFmt", 30), 30),
            new(LanguageService.Get("L_OneHour"), 60),
        }, _settings.SnoozeMinutes, v => LanguageService.Format("L_MinutesFmt", v));
        SnoozeBox.SelectedValue = _settings.SnoozeMinutes;

        AutoCloseBox.ItemsSource = WithCurrent(new List<Choice>
        {
            new(LanguageService.Get("L_Never"), 0),
            new(LanguageService.Format("L_SecondsFmt", 15), 15),
            new(LanguageService.Format("L_SecondsFmt", 30), 30),
            new(LanguageService.Get("L_OneMinute"), 60),
            new(LanguageService.Get("L_TwoMinutes"), 120),
        }, _settings.AutoCloseSeconds, v => LanguageService.Format("L_SecondsFmt", v));
        AutoCloseBox.SelectedValue = _settings.AutoCloseSeconds;
    }

    private void BuildDayToggles()
    {
        DaysPanel.Children.Clear();
        var format = CultureInfo.CurrentCulture.DateTimeFormat;

        foreach (var day in WeekOrder)
        {
            var box = new CheckBox
            {
                Content = format.GetAbbreviatedDayName(day),
                Tag = day,
                IsChecked = _settings.ActiveDays.Contains(day),
                // Tight enough that all seven abbreviated day names fit on one row.
                Margin = new Thickness(0, 0, 9, 6),
                MinWidth = 36,
            };

            box.Checked += Day_Changed;
            box.Unchecked += Day_Changed;
            DaysPanel.Children.Add(box);
        }
    }

    private void Day_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var selected = DaysPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => (DayOfWeek)c.Tag!)
            .ToList();

        if (selected.Count == 0)
        {
            // A schedule with no days would silently never fire.
            DaysHint.Visibility = Visibility.Visible;
            if (sender is CheckBox box) box.IsChecked = true;
            return;
        }

        DaysHint.Visibility = Visibility.Collapsed;
        _settings.ActiveDays = selected;
    }

    private void NewTimeText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddTime();
        e.Handled = true;
    }

    private void AddTime_Click(object sender, RoutedEventArgs e) => AddTime();

    private void AddTime()
    {
        var text = NewTimeText.Text.Trim();
        if (text.Length == 0) return;

        if (!TryParseTime(text, out var time))
        {
            TimeHint.Text = LanguageService.Format("L_TimeUnreadableFmt", text);
            return;
        }

        if (_times.Any(t => t.Value == time))
        {
            TimeHint.Text = LanguageService.Format("L_TimeDuplicateFmt", Format(time));
            return;
        }

        // Insert in order so the list always reads chronologically.
        var index = _times.Count;
        for (var i = 0; i < _times.Count; i++)
        {
            if (_times[i].Value <= time) continue;
            index = i;
            break;
        }

        _times.Insert(index, new TimeEntry(time));
        NewTimeText.Clear();
        TimeHint.Text = LanguageService.Format("L_TimeAddedFmt", Format(time));
        SyncTimes();
    }

    private void RemoveTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TimeEntry entry }) return;

        if (_times.Count <= AppSettings.MinPopupTimes)
        {
            TimeHint.Text = LanguageService.Format("L_TimeMinimumFmt", AppSettings.MinPopupTimes);
            return;
        }

        _times.Remove(entry);
        TimeHint.Text = LanguageService.Format("L_TimeRemovedFmt", entry.Display);
        SyncTimes();
    }

    private void SyncTimes() =>
        _settings.PopupTimes = _times.Select(t => t.Value).OrderBy(v => v).ToList();

    private static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out time)
        || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private void SnoozeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SnoozeBox.SelectedValue is not int value) return;
        _settings.SnoozeMinutes = value;
    }

    private void AutoCloseBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AutoCloseBox.SelectedValue is not int value) return;
        _settings.AutoCloseSeconds = value;
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedValue is not AppLanguage language) return;
        if (language == _settings.Language) return;

        _settings.Language = language;
        LanguageService.Apply(language);
        Storage.SaveSettings(_settings);
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
    }

    private void KeepOnTop_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _settings.KeepOnTop = KeepOnTopBox.IsChecked == true;
        App.Current.ApplyKeepOnTop();
        Storage.SaveSettings(_settings);
    }

    private void DarkMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _settings.Theme = DarkModeBox.IsChecked == true ? AppTheme.Dark : AppTheme.Light;
        ThemeService.Apply(_settings.Theme);

        // Saved now rather than on close, so the choice survives a forced quit.
        Storage.SaveSettings(_settings);
    }

    private void Preview_Click(object sender, RoutedEventArgs e) => App.Current.ShowPopup();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        LanguageService.Changed -= _languageChanged;
        base.OnClosed(e);
    }
}
