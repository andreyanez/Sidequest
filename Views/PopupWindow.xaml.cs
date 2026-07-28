using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WeeklyReminder.Models;
using WeeklyReminder.Services;

namespace WeeklyReminder.Views;

public partial class PopupWindow : Window
{
    /// <summary>How much of the window must stay reachable for a saved position to be reused.</summary>
    private const double MinVisible = 120;

    private readonly WeekPlan _plan;
    private readonly AppSettings _settings;
    private DispatcherTimer? _countdown;
    private int _secondsLeft;
    private bool _userPositioned;

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

        // Known up front so the SizeChanged handler below doesn't yank a dragged
        // window back to the corner while the content is still being measured.
        _userPositioned = settings.PopupLeft is not null && settings.PopupTop is not null;

        Loaded += OnLoaded;
        SizeChanged += (_, _) =>
        {
            if (!_userPositioned) PositionBottomRight();
        };

        // Any interaction means it has been seen; don't yank it away mid-read.
        MouseEnter += (_, _) => StopCountdown();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateProgress();
        RestorePosition();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        StartCountdown();
    }

    /// <summary>
    /// Drags the whole window. Interactive controls handle the event themselves, so
    /// this only fires on the card's empty areas, the header and the labels.
    /// </summary>
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try
        {
            // Blocks until the button is released.
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was let go between the event and this call; nothing to do.
            return;
        }

        _userPositioned = true;
        _settings.PopupLeft = Left;
        _settings.PopupTop = Top;
        Storage.SaveSettings(_settings);
    }

    private void RestorePosition()
    {
        if (_settings.PopupLeft is double left && _settings.PopupTop is double top && IsReachable(left, top))
        {
            Left = left;
            Top = top;
            _userPositioned = true;
            return;
        }

        // Saved spot is gone — a monitor was unplugged or the resolution changed.
        _userPositioned = false;
        PositionBottomRight();
    }

    /// <summary>True if enough of the window would land on the virtual desktop to grab it.</summary>
    private bool IsReachable(double left, double top)
    {
        var vLeft = SystemParameters.VirtualScreenLeft;
        var vTop = SystemParameters.VirtualScreenTop;
        var vRight = vLeft + SystemParameters.VirtualScreenWidth;
        var vBottom = vTop + SystemParameters.VirtualScreenHeight;

        return left + ActualWidth - MinVisible > vLeft
            && left + MinVisible < vRight
            && top + ActualHeight - MinVisible > vTop
            && top + MinVisible < vBottom;
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
