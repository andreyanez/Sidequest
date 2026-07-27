using System.Threading;
using System.Windows;
using WeeklyReminder.Models;
using WeeklyReminder.Services;
using WeeklyReminder.Views;
using Forms = System.Windows.Forms;

namespace WeeklyReminder;

/// <summary>
/// Tray-resident host. There is no main window: the app lives in the notification
/// area and opens windows on demand.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\WeeklyReminder.SingleInstance.v1";
    private const string ShowEventName = @"Local\WeeklyReminder.ShowPopup.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Forms.NotifyIcon? _tray;
    private Scheduler? _scheduler;

    private PopupWindow? _popup;
    private EditorWindow? _editor;
    private SettingsWindow? _settingsWindow;

    public AppSettings Settings { get; private set; } = new();
    public WeekPlan Plan { get; private set; } = new();

    public static new App Current => (App)System.Windows.Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!isFirstInstance)
        {
            // Hand the request to the instance already running, then get out of the way.
            _showEvent.Set();
            Shutdown();
            return;
        }

        ListenForShowRequests();

        Settings = Storage.LoadSettings();
        Plan = Storage.LoadWeek();

        // The registry is the source of truth for autostart; it can be changed
        // outside the app (Task Manager's Startup tab, for one).
        Settings.StartWithWindows = AutoStart.IsEnabled();

        BuildTrayIcon();

        _scheduler = new Scheduler(Settings);
        _scheduler.Due += ShowPopup;
        _scheduler.Start();
    }

    /// <summary>Relaunching the exe surfaces the popup rather than starting a second copy.</summary>
    private void ListenForShowRequests()
    {
        var listener = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
                Dispatcher.BeginInvoke(ShowPopup);
        })
        {
            IsBackground = true,
            Name = "WeeklyReminder.ShowRequestListener",
        };
        listener.Start();
    }

    private void BuildTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show reminders", null, (_, _) => ShowPopup());
        menu.Items.Add("Edit this week…", null, (_, _) => ShowEditor());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "Weekly Reminder",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowPopup();
    }

    public void ShowPopup()
    {
        if (Plan.RollForwardToCurrentWeek())
            Storage.SaveWeek(Plan);

        if (_popup is { IsLoaded: true })
        {
            _popup.Activate();
            return;
        }

        _popup = new PopupWindow(Plan, Settings);
        _popup.Closed += (_, _) =>
        {
            Storage.SaveWeek(Plan);
            _popup = null;
        };
        _popup.SnoozeRequested += () => _scheduler?.Snooze();
        _popup.EditRequested += ShowEditor;
        _popup.Show();
    }

    public void ShowEditor()
    {
        if (_editor is { IsLoaded: true })
        {
            _editor.Activate();
            return;
        }

        _editor = new EditorWindow(Plan);
        _editor.Closed += (_, _) =>
        {
            Storage.SaveWeek(Plan);
            _editor = null;
        };
        _editor.Show();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(Settings);
        _settingsWindow.Closed += (_, _) =>
        {
            Storage.SaveSettings(Settings);
            AutoStart.Set(Settings.StartWithWindows);
            _scheduler?.ClearSnooze();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            // Hide first, or the icon lingers in the tray until hovered.
            _tray.Visible = false;
            _tray.Icon?.Dispose();
            _tray.Dispose();
        }

        _scheduler?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
