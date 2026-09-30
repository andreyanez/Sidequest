using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using Sidequest.Models;
using Sidequest.Services;
using Sidequest.Views;
using Forms = System.Windows.Forms;

namespace Sidequest;

/// <summary>
/// Tray-resident host. The main panel is a fixture that opens on launch (D1), but closing
/// it only hides it: the app lives in the notification area until Quit.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\Sidequest.SingleInstance.v1";
    private const string ShowEventName = @"Local\Sidequest.ShowPopup.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;

    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Raised after a setting changes from anywhere, so two places showing the same one -
    /// the panel's pin and Settings' "siempre visible" switch - stay in step.
    /// </summary>
    public event Action? SettingsChanged;

    public void NotifySettingsChanged() => SettingsChanged?.Invoke();

    /// <summary>The theme to show: the one chosen, or Windows' own under "Sistema" (5.8).</summary>
    public AppTheme EffectiveTheme() => Settings.FollowSystemTheme ? ThemeService.SystemTheme() : Settings.Theme;

    public ObservableCollection<SidequestProject> Projects { get; private set; } = new();

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

        // First, before anything reads a file: settings were otherwise loaded from the
        // real folder and only then redirected, so a sandboxed run silently started from
        // the wrong settings.
        RedirectDataIfSandboxed();

        Settings = Storage.LoadSettings();

        // Before any window is created, so nothing flashes in the wrong theme or language.
        // Left null in settings deliberately: until a language is picked explicitly, the
        // app keeps following the Windows display language rather than freezing today's.
        LanguageService.Apply(Settings.Language ?? LanguageService.DetectFromSystem());

        // D3: one-time import of WeeklyReminder's data, after the language is known so
        // the imported Sidequest gets a name the user can read. It is a no-op once
        // Sidequest has data of its own, so it cannot overwrite anything.
        LegacyMigration.Run(Settings, LanguageService.Get("L_ImportedSidequest"));
        Projects = SidequestStore.Load();

        ThemeService.Apply(EffectiveTheme());

        // Windows' light/dark can change while the app runs: "Sistema" follows it, and the
        // tray icon has to stay readable on the taskbar either way.
        ThemeService.WatchSystem();
        ThemeService.SystemChanged += OnSystemThemeChanged;

        // The registry is the source of truth for autostart; it can be changed
        // outside the app (Task Manager's Startup tab, for one).
        Settings.StartWithWindows = AutoStart.IsEnabled();

        BuildTrayIcon();

        // D1: nothing surfaces the app on a schedule any more. The panel is a fixture, so
        // it opens on launch and restores the collapsed state it was left in.
        _mainWindow = new MainWindow();
        _mainWindow.ShowPanel();

        OpenRequestedView(e.Args);
    }

    /// <summary>
    /// Opens a screen straight from the command line, so it can be checked without
    /// clicking through to it. Only honoured by the first instance; a relaunch while
    /// running always just brings the panel back.
    /// </summary>
    private void OpenRequestedView(string[] args)
    {
        if (args.Any(a => a.Equals("--sidequests", StringComparison.OrdinalIgnoreCase)))
            ShowSidequests();

        if (args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            ShowSettings();
    }

    private void OnSystemThemeChanged()
    {
        if (Settings.FollowSystemTheme && ThemeService.Current != ThemeService.SystemTheme())
            ThemeService.Apply(ThemeService.SystemTheme());

        if (_tray is null) return;
        var previous = _tray.Icon;
        _tray.Icon = TrayIconFactory.Create();
        previous?.Dispose();
    }

    /// <summary>
    /// Points both stores at a sandbox when SIDEQUEST_DATA is set. WeeklyReminder is
    /// still the app in daily use and the import renames its week.json, so a dev run
    /// must not be able to reach the real folders. Unset in normal use.
    /// </summary>
    private static void RedirectDataIfSandboxed()
    {
        var sandbox = Environment.GetEnvironmentVariable("SIDEQUEST_DATA");
        if (string.IsNullOrWhiteSpace(sandbox)) return;

        SidequestStore.Directory = Path.Combine(sandbox, "Sidequest");
        Storage.Directory = Path.Combine(sandbox, "Sidequest");
        LegacyMigration.LegacyDirectory = Path.Combine(sandbox, "WeeklyReminder");

        // Settings' "Abrir al iniciar el equipo" must not register a dev build to run at
        // sign-in - it would start without this sandbox, on real data.
        AutoStart.SandboxFile = Path.Combine(sandbox, "autostart");
    }

    /// <summary>Relaunching the exe brings the panel back rather than starting a second copy.</summary>
    private void ListenForShowRequests()
    {
        var listener = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
                Dispatcher.BeginInvoke(ShowPanel);
        })
        {
            IsBackground = true,
            Name = "Sidequest.ShowRequestListener",
        };
        listener.Start();
    }

    private void BuildTrayIcon()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "Sidequest",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.DoubleClick += (_, _) => ShowPanel();

        // WinForms menu labels are plain strings, so the menu is rebuilt wholesale
        // rather than tracking a resource.
        LanguageService.Changed += () =>
        {
            if (_tray is null) return;

            var previous = _tray.ContextMenuStrip;
            _tray.ContextMenuStrip = BuildTrayMenu();
            previous?.Dispose();
        };
    }

    /// <summary>5.9's menu, in 5.9's order.</summary>
    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(LanguageService.Get("L_TrayShow"), null, (_, _) => ShowPanel());
        menu.Items.Add(LanguageService.Get("L_TraySidequests"), null, (_, _) => ShowSidequests());
        menu.Items.Add(LanguageService.Get("L_TraySettings"), null, (_, _) => ShowSettings());
        menu.Items.Add(LanguageService.Get("L_TrayResetPosition"), null, (_, _) => _mainWindow?.ResetPosition());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(LanguageService.Get("L_TrayQuit"), null, (_, _) => Shutdown());
        return menu;
    }

    /// <summary>The app's front door: the tray, a relaunch, a double-click on the icon.</summary>
    public void ShowPanel() => _mainWindow?.ShowPanel();

    /// <summary>The tray's "Sidequests…": the panel, opened on the list (5.4).</summary>
    private void ShowSidequests()
    {
        ShowPanel();
        _mainWindow?.ShowSidequests();
    }

    /// <summary>
    /// Settings (5.8), one window at a time. Owned by the panel (6), so it closes with the
    /// app and stays above a panel that is kept on top.
    /// </summary>
    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow { Owner = _mainWindow };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>Pushes the keep-on-top setting to the panel that is already open.</summary>
    public void ApplyKeepOnTop() => _mainWindow?.SyncKeepOnTop();

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.StopWatchingSystem();

        if (_tray is not null)
        {
            // Hide first, or the icon lingers in the tray until hovered.
            _tray.Visible = false;
            _tray.Icon?.Dispose();
            _tray.Dispose();
        }

        _showEvent?.Dispose();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
