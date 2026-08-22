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

        // Before any window is created, so nothing flashes in the wrong theme.
        ThemeService.Apply(Settings.Theme);

        // The registry is the source of truth for autostart; it can be changed
        // outside the app (Task Manager's Startup tab, for one).
        Settings.StartWithWindows = AutoStart.IsEnabled();

        BuildTrayIcon();

        _scheduler = new Scheduler(Settings);
        _scheduler.Due += ShowPopup;
        _scheduler.Start();

        OpenRequestedWindow(e.Args);
    }

    /// <summary>
    /// Opens a window straight from the command line, so a given screen can be brought
    /// up without navigating the tray menu. Only honoured by the first instance; a
    /// relaunch while running always just surfaces the popup.
    /// </summary>
    private void OpenRequestedWindow(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "--edit":
                    ShowEditor();
                    return;
                case "--settings":
                    ShowSettings();
                    return;
                case "--popup":
                    ShowPopup();
                    return;
            }
        }
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
        menu.Items.Add("Reset popup position", null, (_, _) => ResetPopupPosition());
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

        // The popup is topmost, so it steps aside while another window is in use and
        // comes back once that window is done with.
        _popup.EditRequested += () => ShowEditor(returnToPopup: true);
        _popup.SettingsRequested += () => ShowSettings(returnToPopup: true);

        _popup.Show();
    }

    /// <summary>Forgets a dragged position and shows the popup back in the corner.</summary>
    private void ResetPopupPosition()
    {
        Settings.PopupLeft = null;
        Settings.PopupTop = null;
        Storage.SaveSettings(Settings);

        _popup?.Close();
        ShowPopup();
    }

    /// <param name="returnToPopup">
    /// Reopen the reminder list once this window closes. Set when the editor was
    /// reached from the popup, so the popup is restored rather than left dismissed.
    /// </param>
    public void ShowEditor(bool returnToPopup = false)
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
            if (returnToPopup) ShowPopup();
        };
        _editor.Show();
    }

    /// <param name="returnToPopup">See <see cref="ShowEditor"/>.</param>
    public void ShowSettings(bool returnToPopup = false)
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
            if (returnToPopup) ShowPopup();
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
