using System.Globalization;
using System.Windows;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
using UserControl = System.Windows.Controls.UserControl;

namespace Sidequest.Views;

public partial class PanelView : UserControl
{
    private readonly Action _refresh;

    public PanelView()
    {
        InitializeComponent();

        // A theme switch changes every Sidequest's colour, and a language switch changes
        // the weekday and date. Both are resolved in code, so both need a rebuild.
        _refresh = Refresh;
        ThemeService.Changed += _refresh;
        LanguageService.Changed += _refresh;

        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
        };
    }

    private static IEnumerable<ObjectiveRow> BuildRows()
    {
        // Order is PanelIndex and nothing reorders it (D7), so this is send order.
        return App.Current.Projects
            .SelectMany(project => project.Items
                .Where(item => item.InPanel)
                .Select(item => new ObjectiveRow(item, project)))
            .OrderBy(row => row.Item.PanelIndex ?? int.MaxValue)
            .ToList();
    }

    public void Refresh()
    {
        var rows = BuildRows().ToList();
        ObjectiveList.ItemsSource = rows;

        var done = rows.Count(r => r.Item.Done);
        WeekdayText.Text = Weekday(DateTime.Today);
        DateText.Text = FullDate(DateTime.Today);
        CounterText.Text = LanguageService.Format("L_CounterFmt", done, rows.Count);

        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CounterText.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>The collapsed row shows the same date and counter, so both are shared.</summary>
    internal static string FullDateText() => FullDate(DateTime.Today);

    internal (int Done, int Total) Counts()
    {
        var rows = BuildRows().ToList();
        return (rows.Count(r => r.Item.Done), rows.Count);
    }

    /// <summary>
    /// Upper case, because it is a section label (4.1) - the artboard renders "JUEVES"
    /// through a CSS text-transform, which the markup's own text does not show. WPF has
    /// no equivalent, so it happens here.
    /// </summary>
    private static string Weekday(DateTime date) =>
        date.ToString("dddd", CultureInfo.CurrentCulture).ToUpper(CultureInfo.CurrentCulture);

    /// <summary>
    /// The culture's long date with the weekday removed: es-ES's pattern leads with
    /// "dddd," and the line above already carries the day, as the artboard shows.
    /// </summary>
    private static string FullDate(DateTime date)
    {
        var pattern = CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern
            .Replace("dddd", string.Empty)
            .Trim()
            .TrimStart(',', ' ')
            .Trim();

        return date.ToString(pattern, CultureInfo.CurrentCulture);
    }

    private static void Save() => SidequestStore.Save(App.Current.Projects);

    private void Done_Changed(object sender, RoutedEventArgs e)
    {
        // The binding has already updated the item; this only persists and re-counts.
        Save();
        Refresh();
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        // Returning keeps Done: 1 says the item goes back to its Sidequest as it stands.
        row.Item.InPanel = false;
        row.Item.PanelIndex = null;

        Save();
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        var owner = Window.GetWindow(this);
        if (owner is null) return;

        // This removes the item from its Sidequest too, so it asks first.
        if (!ConfirmDialog.Ask(
                owner,
                LanguageService.Get("L_DeleteObjectiveQ"),
                row.Item.Text,
                confirmLabel: LanguageService.Get("L_DeleteObjective")))
            return;

        row.Project.Items.Remove(row.Item);
        Save();
        Refresh();
    }

    // Phase 3 adds the Sidequests list, phase 5 the settings window. Until then these
    // are deliberately inert rather than half-wired.
    private void Sidequests_Click(object sender, RoutedEventArgs e) { }

    private void Settings_Click(object sender, RoutedEventArgs e) { }
}
