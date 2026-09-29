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

    public AppSettings Settings { get; private set; } = new();

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

        ThemeService.Apply(Settings.Theme);

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

    /// <summary>
    /// 5.9's menu. "Ajustes…" joins it with the settings window in phase 5; until then it
    /// is left out rather than shown doing nothing.
    /// </summary>
    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(LanguageService.Get("L_TrayShow"), null, (_, _) => ShowPanel());
        menu.Items.Add(LanguageService.Get("L_TraySidequests"), null, (_, _) => ShowSidequests());
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

    protected override void OnExit(ExitEventArgs e)
    {
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
