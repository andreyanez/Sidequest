using System.Globalization;
using System.Windows;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
using UserControl = System.Windows.Controls.UserControl;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;

namespace Sidequest.Views;

public partial class PanelView : UserControl
{
    private readonly MainWindow _shell;
    private readonly Action _refresh;

    public PanelView(MainWindow shell)
    {
        InitializeComponent();
        _shell = shell;

        // A theme switch changes every Sidequest's colour, and a language switch changes
        // the weekday and date. Both are resolved in code, so both need a rebuild.
        //
        // Subscribed on Loaded rather than once in the constructor: navigating to a
        // sub-panel unloads this view, and it has to hear about switches again when it
        // comes back. Unsubscribing first keeps a second Loaded from doubling up.
        _refresh = Refresh;
        Loaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            ThemeService.Changed += _refresh;
            LanguageService.Changed += _refresh;
            Refresh();
        };
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

        WeekdayText.Text = Weekday(DateTime.Today);
        DateText.Text = FullDate(DateTime.Today);
        UpdateCounter();

        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CounterText.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Where keyboard focus lands on coming back: the button that left.</summary>
    internal void FocusSidequestsButton() =>
        Dispatcher.BeginInvoke(() => SidequestsButton.Focus(), System.Windows.Threading.DispatcherPriority.Loaded);

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

    /// <summary>Re-counts from the rows already on screen, without rebuilding them.</summary>
    private void UpdateCounter()
    {
        var rows = ObjectiveList.ItemsSource as IReadOnlyCollection<ObjectiveRow>
                   ?? Array.Empty<ObjectiveRow>();

        CounterText.Text = LanguageService.Format(
            "L_CounterFmt", rows.Count(r => r.Item.Done), rows.Count);
    }

    private void Done_Changed(object sender, RoutedEventArgs e)
    {
        // Deliberately not Refresh(): rebuilding the list here replaces the very row that
        // was just ticked, which threw keyboard focus back to the top of the window after
        // every tick. The strikethrough comes from a DataTrigger and SidequestItem raises
        // PropertyChanged, so the row restyles itself; only the counter needs saying.
        Save();
        UpdateCounter();
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        SidequestRules.ReturnToSidequest(row.Item);

        Save();
        Refresh();
    }

    /// <summary>5.1: the chip opens its Sidequest.</summary>
    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        _shell.ShowSidequest(row.Project);
    }

    /// <summary>
    /// Supr on a focused row deletes it, which 8 asks for: tabbing to the delete button is
    /// possible but slow, since every row carries several stops.
    /// </summary>
    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        if (Keyboard.FocusedElement is not FrameworkElement { DataContext: ObjectiveRow row }) return;

        DeleteRow(row);
        e.Handled = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ObjectiveRow row }) return;
        DeleteRow(row);
    }

    private void DeleteRow(ObjectiveRow row)
    {
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

    private void Sidequests_Click(object sender, RoutedEventArgs e) => _shell.ShowSidequests();

    // Phase 5 adds the settings window. Until then this is deliberately inert rather
    // than half-wired.
    private void Settings_Click(object sender, RoutedEventArgs e) { }
}
