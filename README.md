# Weekly Reminder

A small tray-resident Windows app that pops up a reminder window showing what you
planned for the week. Runs entirely locally; no accounts, no network.

## Requirements

- Windows 10 or later
- .NET 8 SDK to build (`winget install --id Microsoft.DotNet.SDK.8 --source winget`)
- .NET 8 Desktop Runtime to run a framework-dependent build

## Build and run

```
dotnet build
dotnet run
```

The app has no main window. On launch it puts a blue tick icon in the notification
area; everything is reached from there.

A window can also be opened straight from the command line, which is handy for
checking a screen without navigating the tray menu:

```
WeeklyReminder.exe --popup
WeeklyReminder.exe --edit
WeeklyReminder.exe --settings
```

These are honoured by the first instance only. Launching the exe while it is already
running always just surfaces the popup.

Publish a standalone copy:

```
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## How it works

- **Tray menu** — Show reminders, Edit this week, Settings, Quit. Double-clicking the
  icon shows the popup.
- **Popup** — slides into the bottom-right corner with a flat list of everything on
  this week's list. Tick items off directly. Snooze, Edit, or Dismiss. Closes itself
  after a configurable delay unless you move the mouse over it. It grows to fit the
  whole list, and only scrolls once the list outgrows the screen.
- **Dragging** — grab any empty part of the card and drag. The position is remembered
  and reused for every later popup. If the saved spot is no longer on any screen (a
  monitor unplugged, a resolution change) it falls back to the bottom-right corner.
  *Reset popup position* in the tray menu forgets it deliberately.
- **Schedule** — fires at each configured time of day, at least twice, on the days you
  pick. Times are checked against the wall clock every 30 seconds, so sleep, hibernation
  and clock changes need no special handling. A slot missed while the machine was off
  surfaces once when it wakes, rather than queueing up several popups.
- **Week rollover** — every Monday the plan resets. Items flagged *Weekly* carry over
  and are unticked; everything else is cleared.
- **Dark mode** — toggled in Settings, applied live to every open window including the
  Windows title bar. The choice is saved immediately rather than on close.
- **Startup** — optional per-user `Run` key entry, toggled in Settings. No admin rights.
- **Single instance** — launching the exe again just shows the popup.

## Data

Two human-readable JSON files under `%APPDATA%\WeeklyReminder\`:

| File | Contents |
| --- | --- |
| `week.json` | The current week's items |
| `settings.json` | Popup times, active days, snooze and auto-close, last fired |

Both are safe to hand-edit while the app is closed. Writes go to a temp file and are
then moved into place, so an unexpected shutdown can't leave a truncated file. If a
file is ever unreadable it's renamed to `*.corrupt-<timestamp>` rather than discarded.

## Layout

```
Models/      TaskItem, WeekPlan, AppSettings
Services/    Storage, Scheduler, AutoStart, TrayIconFactory, ThemeService
Views/       PopupWindow, EditorWindow, SettingsWindow, ConfirmDialog
Themes/      Light.xaml, Dark.xaml — colours only, swapped at runtime
App.xaml     Control styles and templates, shared by both themes
App.xaml.cs  Tray host, single-instance, window lifetime
```

Colours live only in the two theme dictionaries and are referenced with
`DynamicResource`, so slot 0 of the merged dictionaries can be swapped while windows
are open. Adding a colour means adding it to **both** files.

WPF's stock `TextBox`, `CheckBox`, `ComboBox` and `ScrollBar` paint themselves with
fixed light chrome that ignores `Background`/`Foreground`, so `App.xaml` retemplates
them. That is the bulk of the file.

WinForms is referenced only for `NotifyIcon`, which WPF has no equivalent for. The tray
icon is drawn with GDI+ at runtime, so the project ships no binary assets.
