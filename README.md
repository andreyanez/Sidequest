# Sidequest

A small tray-resident Windows app for side projects. Each project is a **Sidequest**
with its own list of items; the ones you mean to get to next are sent to a panel that
stays on screen. Everything is local: no accounts, no network. It replaces
WeeklyReminder, and imports its list on first start.

## Requirements

- Windows 10 or later
- .NET 8 SDK to build (`winget install --id Microsoft.DotNet.SDK.8 --source winget`)
- .NET 8 Desktop Runtime to run a framework-dependent build

## Build and run

**A development run never touches real data.** `%APPDATA%\Sidequest\` holds the real
Sidequests, and the WeeklyReminder import renames `%APPDATA%\WeeklyReminder\week.json`.
Point `SIDEQUEST_DATA` at a scratch folder and every store moves under it:

```powershell
dotnet build
$env:SIDEQUEST_DATA = "$env:TEMP\sq-sandbox"
.\tools\seed-sandbox.ps1 -Sandbox $env:SIDEQUEST_DATA   # the artboards' sample data
Start-Process .\bin\Debug\net8.0-windows\Sidequest.exe
```

`tools\run-sandbox.ps1` does all of that in one step, in a chosen theme and language.
A sandboxed copy runs beside an installed one: it has a single-instance lock of its own,
and its tray icon's tooltip names the sandbox folder.

```powershell
dotnet test tests/Sidequest.Tests
```

The tests redirect every store themselves, whatever the environment says.

## Install

Publish a framework-dependent build, copy it somewhere of its own, and start it:

```powershell
dotnet publish .\Sidequest.csproj -c Release -r win-x64 --self-contained false -o publish
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Sidequest" | Out-Null
Copy-Item .\publish\* "$env:LOCALAPPDATA\Sidequest" -Recurse -Force
$env:SIDEQUEST_DATA = $null    # left over from a dev run, it would start on the sandbox
Start-Process "$env:LOCALAPPDATA\Sidequest\Sidequest.exe"

# A Start menu entry, so it can be started without a prompt
$link = (New-Object -ComObject WScript.Shell).CreateShortcut(
    "$([Environment]::GetFolderPath('Programs'))\Sidequest.lnk")
$link.TargetPath = "$env:LOCALAPPDATA\Sidequest\Sidequest.exe"
$link.Save()
```

Starting it from the Start menu while it is already running brings the panel back.

Name the project file: publishing the solution copies the test project in as well. Turn
on **Abrir al iniciar el equipo** / **Open at startup** in Settings, which adds a
per-user `Run` entry pointing at the exe that is running. No admin rights needed.

### Coming from WeeklyReminder

Turn off WeeklyReminder's start-with-Windows option, then quit it from its tray menu.
The first time Sidequest starts with no data of its own, it imports
`%APPDATA%\WeeklyReminder\week.json` into a Sidequest called "Importados" and renames
the file to `week.json.migrated`. Nothing is deleted: to go back, quit Sidequest, rename
the file back and start WeeklyReminder again. The old settings are not carried over;
set theme, language and startup again in Settings.

## How it works

- **Panel** — today's date, and the objectives you have sent to it, each with the
  Sidequest it came from. Tick them off here; the chip opens its Sidequest. Hovering or
  focusing a row shows its ⋮⋮ handle, to drag it into another place, and *rename*,
  *return to Sidequest* and *delete*. Drag the bottom edge
  to set the panel's height, which every screen keeps; a long list scrolls. The chevron
  collapses the panel to one row, and it stays collapsed until you expand it. The pin
  keeps it above other apps.
- **Sidequests** — every Sidequest with its progress, and *New Sidequest*: a name and one
  of eight colours.
- **A Sidequest** — two tabs. *Items*: its items, grouped as the current objective,
  pending and completed. *Send objective* puts a pending item on the panel, *Add item*
  adds one after another, an item is renamed in place (the pencil, or F2), and its ⋮⋮
  handle moves it within its group. *Notes*:
  notes, where web addresses become links; up to five local files, linked by path; and
  up to five images, copied into the app's own folder so the originals can move. The
  "⋯" menu renames the Sidequest, changes its colour or deletes it.
- **Settings** — appearance (light, dark, or following Windows), language (Spanish or
  English), open at startup, and keep the panel on top. Changes apply at once.
- **Tray** — Show panel, Sidequests…, Settings…, Reset position, Quit. Double-clicking
  the icon shows the panel, and so does starting the exe again. `Sidequest.exe
  --sidequests` and `--settings` open those screens directly.
- **Keyboard** — everything is reachable with Tab, Enter, Escape and Delete; F2 renames,
  Alt+↑ and Alt+↓ on a handle move its card, and the arrow keys move between a
  Sidequest's tabs.

## Data

Human-readable JSON under `%APPDATA%\Sidequest\`:

| File | Contents |
| --- | --- |
| `sidequests.json` | The Sidequests, their items, and which items are on the panel |
| `settings.json` | Theme, language, startup, keep on top, collapsed, panel position and height |
| `media\` | Copies of images attached to Sidequests |

Both files are safe to hand-edit while the app is closed. Writes go to a temp file that
is then moved into place, so an unexpected shutdown can't leave a truncated file, and a
file that can't be read is renamed to `*.corrupt-<timestamp>` rather than discarded.

## Layout

```
Models/      SidequestProject, SidequestItem, SidequestTheme, AppSettings
Services/    stores and the import, rules, palette, theme and language, tray icon,
             autostart, attachments
Views/       MainWindow and the four views it swaps between, SettingsWindow,
             ConfirmDialog
Themes/      Light.xaml, Dark.xaml (colour tokens), Controls.xaml (styles)
Strings/     English.xaml, Spanish.xaml
Icons/       icon geometry
Fonts/       DM Sans, embedded (SIL Open Font License)
tools/       sandbox runs, artboard renders, screen comparison, UI automation
tests/       xUnit suite
docs/        design-spec.md (what to build, in Spanish), dev-notes.md (status,
             checking a screen, decisions), design/reference/ (the artboards)
```

Conventions (colours, strings, styles, keyboard and screen-reader support) are in
[`CLAUDE.md`](CLAUDE.md). Where the work stands is in
[`docs/dev-notes.md`](docs/dev-notes.md), and what changed in each version in
[`CHANGELOG.md`](CHANGELOG.md).
