using System.Collections.Specialized;
using System.Windows;
using WeeklyReminder.Models;
using WeeklyReminder.Services;

// WinForms is referenced for NotifyIcon, so its implicit using collides with WPF's.
// Alias the handful of types we touch rather than fully qualifying at each use.
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WeeklyReminder.Views;

public partial class EditorWindow : Window
{
    private readonly WeekPlan _plan;
    private readonly NotifyCollectionChangedEventHandler _itemsChanged;

    public EditorWindow(WeekPlan plan)
    {
        _plan = plan;

        InitializeComponent();

        RangeText.Text = plan.RangeLabel;
        ItemList.ItemsSource = plan.Items;

        // The plan outlives this window, so keep a handle to detach on close.
        _itemsChanged = (_, _) => UpdateEmptyState();
        _plan.Items.CollectionChanged += _itemsChanged;

        UpdateEmptyState();
        Loaded += (_, _) => NewItemText.Focus();
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _plan.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void NewItemText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddItem();
        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddItem();

    private void AddItem()
    {
        var text = NewItemText.Text.Trim();
        if (text.Length == 0) return;

        _plan.Items.Add(new TaskItem { Text = text });

        NewItemText.Clear();
        NewItemText.Focus();
        Storage.SaveWeek(_plan);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItem item }) return;

        _plan.Items.Remove(item);
        Storage.SaveWeek(_plan);
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        foreach (var done in _plan.Items.Where(i => i.Done).ToList())
            _plan.Items.Remove(done);

        Storage.SaveWeek(_plan);
    }

    /// <summary>Shortcut to the schedule, which is otherwise only reachable from the tray menu.</summary>
    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _plan.Items.CollectionChanged -= _itemsChanged;
        base.OnClosed(e);
    }
}
