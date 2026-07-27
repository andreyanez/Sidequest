using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WeeklyReminder.Models;

namespace WeeklyReminder.Views;

public partial class PopupWindow : Window
{
    private readonly WeekPlan _plan;
    private readonly AppSettings _settings;
    private DispatcherTimer? _countdown;
    private int _secondsLeft;

    public event Action? SnoozeRequested;
    public event Action? EditRequested;

    public PopupWindow(WeekPlan plan, AppSettings settings)
    {
        _plan = plan;
        _settings = settings;

        InitializeComponent();

        RangeText.Text = plan.RangeLabel;
        SnoozeButton.Content = $"Snooze {settings.SnoozeMinutes}m";
        ItemList.ItemsSource = plan.Items;

        Loaded += OnLoaded;
        SizeChanged += (_, _) => PositionBottomRight();

        // Any interaction means it has been seen; don't yank it away mid-read.
        MouseEnter += (_, _) => StopCountdown();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateProgress();
        PositionBottomRight();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        StartCountdown();
    }

    private void UpdateProgress()
    {
        var total = _plan.Items.Count;
        var done = _plan.Items.Count(i => i.Done);

        EmptyState.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProgressText.Visibility = total == 0 ? Visibility.Collapsed : Visibility.Visible;
        ProgressText.Text = $"{done} of {total} done";
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth;
        Top = area.Bottom - ActualHeight;
    }

    private void StartCountdown()
    {
        if (_settings.AutoCloseSeconds <= 0)
        {
            CountdownText.Text = string.Empty;
            return;
        }

        _secondsLeft = _settings.AutoCloseSeconds;
        CountdownText.Text = $"Closes in {_secondsLeft}s";

        _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdown.Tick += (_, _) =>
        {
            _secondsLeft--;
            if (_secondsLeft <= 0)
            {
                StopCountdown();
                Close();
                return;
            }
            CountdownText.Text = $"Closes in {_secondsLeft}s";
        };
        _countdown.Start();
    }

    private void StopCountdown()
    {
        _countdown?.Stop();
        _countdown = null;
        CountdownText.Text = string.Empty;
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        SnoozeRequested?.Invoke();
        Close();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke();
        Close();
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        StopCountdown();
        base.OnClosed(e);
    }
}
