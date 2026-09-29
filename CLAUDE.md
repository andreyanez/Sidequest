# Sidequest

A tray-resident Windows app (WPF, .NET 8) that replaces WeeklyReminder. The rebuild
happens on the `sidequest` branch; `main` still holds WeeklyReminder.

## Read before starting work

1. **`docs/dev-notes.md`**: where the work stands (Status), how to run and check the
   app, and decisions the code cannot show. Keep its Status line current at each phase
   boundary.
2. **`docs/design-spec.md`**: what to build. It is written in Spanish. §0 holds the
   resolved decisions (D1–D7), the v1 scope and the phase order. The list in §8 is the
   older v0.1 order with different numbering, so go by §0.
3. **`docs/design/reference/*.html`**: the artboards. A screen is done when it matches
   its artboard 1:1 in both themes and both languages, and every §5 interaction works
   from the keyboard (Tab, Enter, Escape, Delete) as well as the mouse.

## Rules

- **Never run the app against real data.** WeeklyReminder is still in daily use, and
  the import renames its `week.json`. Every dev run sets `SIDEQUEST_DATA` (see Commands).
- **Keep the test guards.** `TestDataGuard` and the disabled parallelism in
  `AssemblyInfo.cs` exist because the suite once migrated live data. Every test that
  touches storage goes through `TempStore`.
- **Artboards beat the spec's prose**, except for contrast (4.5:1 governs everything,
  enforced by `ContrastTests`) and chip names (full name, truncated). Record each new
  conflict in dev-notes rather than resolving it silently.
- **Render artboards in a browser and measure them; don't read values off the markup.**
  They use CSS `content-box`, so a bordered box renders 2px larger than its CSS
  `height`, and some text is transformed by CSS.

## Commands

```powershell
dotnet build
dotnet test tests/Sidequest.Tests

# Sandboxed run with the artboards' sample data
$env:SIDEQUEST_DATA = "$env:TEMP\sq-sandbox"
.\tools\seed-sandbox.ps1 -Sandbox $env:SIDEQUEST_DATA
Start-Process .\bin\Debug\net8.0-windows\Sidequest.exe
```

`tools/` also has window capture, a Tab-order walker and a UIA tree dump, all described
in dev-notes. They attach only to a build from this repo.

## Conventions

- **Colours.** 4.2's tokens (`Bg`, `Surface`, `Hover`, `Line`, `Line2`, `Text`, `Text2`,
  `Inv`, `Canvas`) live in `Themes/Light.xaml` and `Themes/Dark.xaml`, and every key must
  exist in both. Merged dictionary slot 0 is swapped at runtime, so always reference them
  with `DynamicResource`.
- **Sidequest colours** depend on two axes, the Sidequest's theme and the app's theme,
  so they are resolved in code rather than in a dictionary:
  `SidequestPalette.Brush(theme, ThemeService.Current, alpha)`. Anything showing one
  rebuilds on `ThemeService.Changed`.
- **Strings.** `Strings/English.xaml` and `Strings/Spanish.xaml` (slot 1) use the key
  names from spec §7, and every key must exist in both. Text built in code goes through
  `LanguageService.Get`/`Format` and is rebuilt on `LanguageService.Changed`.
- **Styles.** Styles live in `Themes/Controls.xaml`. A type with one look has an
  implicit style (`CheckBox`, `TextBox`, `ScrollBar`, `ContextMenu`, `ToolTip`). The
  rest are keyed and named for what they are (`PrimaryButton`, `SendButton`, `Heading`,
  `Muted`, and so on). Style keys are only checked when a view loads, so after renaming
  one, load every screen. Icons are geometry in `Icons/Icons.xaml`, drawn with the
  `IconPath` style, which takes its stroke from `Foreground`.
- **TextBox templates** must not bind their content host's `Margin` to `Padding`:
  TextBox applies `Padding` itself, so binding it indents the text twice.
- **Windows** set `Style="{StaticResource AppWindow}"` explicitly. They are frameless
  (`WindowStyle="None"` with `AllowsTransparency`) with a radius-12 root `Border`.
- **Keyboard and screen readers.**
  - Every interactive control gets an `AutomationProperties.Name`.
  - Host containers (`ContentControl`, `ItemsControl`, `ScrollViewer`) set
    `Focusable="False"` so they don't take tab stops.
  - Actions that appear on hover also appear on `IsKeyboardFocusWithin`.
  - Row classes override `ToString()`, since UIA reads it.
- **Model.** The type is `SidequestProject`, not `Sidequest`, because of a clash with the
  root namespace. An objective is the same `SidequestItem` with `InPanel = true`, never a
  copy. `SortIndex` and `PanelIndex` only ever grow, since nothing reorders (D7).
- **WinForms** is referenced only for `NotifyIcon`. Its implicit usings collide with
  WPF's, so code files alias what they use (for example
  `using UserControl = System.Windows.Controls.UserControl;`).
- **Commit messages** follow the existing history: an imperative subject, and a body
  that explains why and cites the spec section (for example 5.4 or D3).
