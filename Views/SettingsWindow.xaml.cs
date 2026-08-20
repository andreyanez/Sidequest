using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WeeklyReminder.Models;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
// Alias the handful of types we touch rather than fully qualifying at each use.
using CheckBox = System.Windows.Controls.CheckBox;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WeeklyReminder.Views;

public partial class SettingsWindow : Window
{
    /// <summary>Wrapper so the list can bind to a preformatted string without XAML format escaping.</summary>
    public sealed record TimeEntry(TimeOnly Value)
    {
        public string Display => Format(Value);
    }

    private sealed record Choice(string Label, int Value);

    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private readonly AppSettings _settings;
    private readonly ObservableCollection<TimeEntry> _times = new();
    private bool _loading = true;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;

        InitializeComponent();

        foreach (var time in settings.PopupTimes.OrderBy(t => t))
            _times.Add(new TimeEntry(time));
        TimeList.ItemsSource = _times;

        BuildDayToggles();

        SnoozeBox.ItemsSource = WithCurrent(new List<Choice>
        {
            new("5 minutes", 5),
            new("10 minutes", 10),
            new("15 minutes", 15),
            new("30 minutes", 30),
            new("1 hour", 60),
        }, settings.SnoozeMinutes, v => $"{v} minutes");
        SnoozeBox.SelectedValue = settings.SnoozeMinutes;

        AutoCloseBox.ItemsSource = WithCurrent(new List<Choice>
        {
            new("Never", 0),
            new("15 seconds", 15),
            new("30 seconds", 30),
            new("1 minute", 60),
            new("2 minutes", 120),
        }, settings.AutoCloseSeconds, v => $"{v} seconds");
        AutoCloseBox.SelectedValue = settings.AutoCloseSeconds;

        StartWithWindowsBox.IsChecked = settings.StartWithWindows;

        _loading = false;

        // Controls receiving focus during load can scroll themselves into view, which
        // left the window opening part-way down with the first popup time cut off.
        Loaded += (_, _) => ContentScroll.ScrollToTop();
    }

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Keeps a stored value selectable even if it isn't one of the presets.</summary>
    private static List<Choice> WithCurrent(List<Choice> choices, int current, Func<int, string> label)
    {
        if (choices.All(c => c.Value != current))
            choices.Add(new Choice(label(current), current));

        return choices.OrderBy(c => c.Value).ToList();
    }

    private void BuildDayToggles()
    {
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
            TimeHint.Text = $"Couldn't read \"{text}\" as a time. Try 09:00 or 7:30 pm.";
            return;
        }

        if (_times.Any(t => t.Value == time))
        {
            TimeHint.Text = $"{Format(time)} is already in the list.";
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
        TimeHint.Text = $"Added {Format(time)}.";
        SyncTimes();
    }

    private void RemoveTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TimeEntry entry }) return;

        if (_times.Count <= AppSettings.MinPopupTimes)
        {
            TimeHint.Text = $"At least {AppSettings.MinPopupTimes} popup times a day are required.";
            return;
        }

        _times.Remove(entry);
        TimeHint.Text = $"Removed {entry.Display}.";
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

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
    }

    private void Preview_Click(object sender, RoutedEventArgs e) => App.Current.ShowPopup();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
