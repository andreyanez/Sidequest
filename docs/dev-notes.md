# Dev notes

Working notes for the Sidequest rebuild. The design is in
[`design-spec.md`](design-spec.md); this file covers how to run and check the thing, and
records decisions that are not visible in the code.

Current branch: `sidequest`. WeeklyReminder stays installed and in daily use until
phase 6 completes.

**Status:** phases 0–3 are done: renames, font, palette and icons; data and import;
shell and main panel; navigation, the Sidequests list, the new-Sidequest form and the
Sidequest detail. WeeklyReminder's screens are gone from this branch. Next is phase 4,
the objective detail (§5.3).

---

## Run against a sandbox, never your real data

`%APPDATA%\Sidequest\` is live data, and the WeeklyReminder import (**D3**) renames
`week.json` in `%APPDATA%\WeeklyReminder\` as a side effect. A dev run must not be able
to reach either.

Set `SIDEQUEST_DATA` to a scratch folder and `App.RedirectDataIfSandboxed()` repoints
both stores under it:

```powershell
$env:SIDEQUEST_DATA = "$env:TEMP\sq-sandbox"
.\tools\seed-sandbox.ps1 -Sandbox $env:TEMP\sq-sandbox
dotnet build; Start-Process .\bin\Debug\net8.0-windows\Sidequest.exe
```

It is read **first** in `OnStartup`, before settings are loaded — an earlier ordering bug
had a sandboxed run reading real settings and writing them back.

The test suite does not rely on the variable. `tests/Sidequest.Tests/TestDataGuard.cs`
redirects every store (Sidequests, settings, the import's source) from a
`[ModuleInitializer]`, and parallelism is disabled in
`AssemblyInfo.cs`, because mutable static paths plus xUnit parallelism once let a test
run migrate live data. Two regression tests cover it. Do not remove either guard.

## Checking a screen

The artboards in `design/reference/` are the done-criterion: a screen is finished when it
matches its HTML at 1:1 in both themes and both languages. **Render them in a browser and
measure — do not read the markup.** Doing that turned up three differences the source
text does not show: the weekday is uppercased by CSS, the checkbox `border-radius: 10px`
on an 18px box clamps to a circle, and every bordered box is 2px larger than its CSS
`height` because the artboards use `content-box`.

| Script | What it is for |
| --- | --- |
| `tools/seed-sandbox.ps1` | Fills a sandbox with the artboards' four Sidequests and six panel objectives |
| `tools/run-sandbox.ps1` | Reseeds the sandbox and launches the build in a given theme and language, at a fixed position |
| `tools/render-artboard.ps1` | Renders an artboard to PNG at 1:1 with headless Edge |
| `tools/capture-window.ps1` | Screenshots the app's own window rect (never the whole desktop) |
| `tools/compare-ink.ps1` | Where each element is drawn, artboard vs app, to the pixel (or per-row, for text) |
| `tools/uia-do.ps1` | Invokes, focuses, selects or types into a control by its accessible name |
| `tools/send-keys.ps1` | Real keystrokes to whichever window can take them, the dialog when one is up |
| `tools/tab-walk.ps1` | Presses Tab and reports what really has focus each time |
| `tools/uia-tree.ps1` | Accessibility tree, flagging focusable controls with no name |

A typical check:

```powershell
.\tools\run-sandbox.ps1 -Theme Dark -Language Spanish
.\tools\uia-do.ps1 -Name 'Sidequests'             # navigate the way a user would
.\tools\capture-window.ps1 -ProcessName Sidequest -OutPath $env:TEMP\app.png
.\tools\render-artboard.ps1 -Name FnSidequests -OutPath $env:TEMP\art.png
.\tools\compare-ink.ps1 -Art $env:TEMP\art.png -App $env:TEMP\app.png -Rects 'first:4,103,396,175'
```

`.claude/launch.json` also serves the artboards on `127.0.0.1:8765` for the desktop app's
browser pane, where `getBoundingClientRect` gives a box's exact CSS size.

Measuring text needs care. WPF's Display text mode hints DM Sans to whole pixels, so at
11–14px its glyphs come out about 1px taller than Chrome's: the x-height and cap height
round up, while the baselines land on the same row. An ink bounding box reads that as
"the text is 1px high". Compare per-row ink profiles instead: the heaviest bottom row is
the baseline. Text is also 1–3px wider per string, because Display mode rounds every
advance to a whole pixel and WPF has no letter-spacing. A string that exactly fits in
Chrome can therefore ellipsize in the app ("Cambiar dirección de servicios" in
`FnSidequest` does). None of this is a layout error, and chasing it cost a phase 3
session an hour.

Three traps, all of which produced confident wrong answers before:

- **Foreground stealing.** `SetForegroundWindow` fails silently when another process owns
  the last input, and the keystrokes go to that app instead. `tab-walk.ps1` borrows the
  foreground thread's input queue, and skips a step rather than reporting a bogus result.
- **UIA does not always see a modal dialog.** Querying `RootElement` children for the
  process missed an open `ConfirmDialog` entirely, which read as "the Delete key does
  nothing". `capture-window.ps1`'s `EnumWindows` found it immediately. Confirm a window
  exists with `EnumWindows`, not UIA.
- **Forcing the foreground can bypass a modal dialog.** Borrowing the input queue lets
  `SetForegroundWindow` activate even a window that `ShowDialog` has disabled, and then
  keystrokes reach a window a real user cannot type into. That is how a test sent an
  item to the panel while its delete confirmation was still open. Send keys to the
  enabled window, which is the dialog whenever one is up.

## Decisions

Artboards win wherever they conflict with the spec's prose, with the two exceptions
below. Flag each conflict as it comes up rather than resolving it silently.

**Chip names — full name, truncated.** `FnMain.html` shows abbreviated names
(`Portafolio`, `Curso TS`). Decided against: the Sidequest's real name stays and is
truncated with an ellipsis when it does not fit, so the model keeps one name and no
second short-name field is introduced. This is what is built (`MaxWidth` plus
`TextTrimming` on the chip). **The artboards lose this one** — the only place other than
contrast where that is true.

**Contrast comes first.** §4.2's rule — every theme colour carrying text clears 4.5:1 —
governs over the colour table and over the artboards. Amber light `#9A6B1F` measures
**4.32** on `Bg` and **3.64** on its own 14 % header band, and Olive `#5F6B2A` measures
**4.45** on that band, so both are darkened in `Services/SidequestPalette.cs` to
`#815A1A` and `#5C6829` — the smallest move that clears the rule with the hue kept.

`ContrastTests` enforces this over all eight themes × both app themes × the seven
surfaces a theme colour carries text on: `Bg`, `Surface`, `Hover` (a pending row under
the pointer keeps its "Enviar objetivo" in the theme colour), the 14 % header band, the
9 % list tint, the 7 % new-Sidequest form, and `Inv` reversed out of a solid fill. It
reads the surface tokens straight from `Themes/*.xaml`, so editing a colour there is what
it measures. The tightest is Teal light on its own header band at **4.51**, so treat that
one as having no headroom. If a new colour fails, darken the light value or lighten the
dark one. Do not lower the threshold.

### Phase 3

Where the artboards and §5 left something open, or disagree:

- **"n activos" counts every Sidequest.** The model has no finished or archived state, so
  there is no other reading of "active".
- **A ticked objective still in the panel is listed with the completed items** in the
  detail, not as "Objetivo actual". §5.6's groups leave that case open; `FnSidequest`
  settles it ("Inventario de cajas").
- **Pending items have no checkbox in the detail.** §5.6: Done is edited where its
  checkbox is shown, and `FnSidequest` shows none on pending rows, so an item gets ticked
  from the panel. A completed item that is not in the panel can be unticked from the
  detail, where its tick is live. In the panel, the tick is shown but disabled.
- **Sub-panels have no window buttons.** The artboards draw only "‹ back" and the app
  name, so the panel is collapsed or closed from the main panel alone.
- **After creating a Sidequest, its add-item field is already open.** It is empty, and
  adding items is the only thing left to do there. The spec doesn't say this.
- **"Cambiar tema" is an inline picker in the band**, the same control as §5.5's. No
  artboard shows it. The arrow keys preview live, Enter or a click keeps the choice, and
  Escape reverts.
- **The "⋯" menu has no artboard**, so it is held to §4.2's tokens and §4.3's radii.
- **The chip is a button**, so the keyboard can reach it, but it has no hover state:
  `FnMain` gives it none, because its inline colour outranks `a:hover`.
- **Every control gets a focus ring** in `Text`, shaped to its own corners, in place of
  WPF's dotted rectangle. This covers the phase 2 buttons too. §4.4 defines one only for
  the checkbox.
- **Strings not in §7:** `L_ActiveOneFmt`, `L_DeleteSidequestOneQ` and
  `L_DeleteSidequestEmptyQ` avoid "1 activos" and "sus 1 items", and `L_DeleteItemQ` asks
  before deleting an item. `L_DeleteSidequestQ` drops "y su Bitácora" for v1, since
  there is none yet. Put it back with phase 7.
- **Fixed on the way:** the title bar's 1px `Line` under every artboard's title bar was
  missing from phase 2's panel. Separately, every TextBox template bound its content
  margin to `Padding`, which TextBox already applies itself, so field text was indented
  twice. Nothing used those styles until now.

## Known deviations from the spec

- **The import does not carry over WeeklyReminder's settings** (§3 step 3). Accepted on
  2026-09-29 as not worth fixing: theme, language and startup get set again in Settings.
  `LegacyMigration` copies them into `AppSettings`, but `App.OnStartup` applies the
  language before the import runs, overwrites `StartWithWindows` from the registry, and
  never saves afterwards. Only the week's items survive the import.
- **Letter-spacing is not implemented.** §4.1 asks for −0.02em on 22/700 headings and
  +0.06em on section labels; measured on the artboard that is −0.44px and +0.66px. WPF has
  no letter-spacing property, and the workaround is an attached behaviour that rebuilds
  text as per-character `Run`s.
- **The notes field has no line-height.** §5.3's artboard uses a 1.55 line box. A WPF
  `TextBox` cannot carry one (`LineHeight` is a `Block` property). §5.3 already suggests a
  `RichTextBox` for URL detection, which can, so phase 4 should use that.
- **The artboards disagree with each other on button radius.** `FnMain` and `FnSidequest`
  use pills (`999`); `FnSidequestNuevo` uses `10` and `FnAjustes` uses `5`. The built
  styles are pills, matching §4.4 and the two panel screens. That includes
  `FnSidequestNuevo`'s Cancelar and Crear, which it also draws 38 high where §4.4 says
  34 and 36.
- **Values the artboards take from outside §4.2**, all built as drawn:
  - `FnSidequests` draws its progress tracks at `C @ 20 %`; the header band keeps §4.2's
    25.
  - `FnSidequestNuevo` tints its form at 7 % with a 40 % border.
  - `FnSidequests` sets "Sidequests" at 22/600, where §4.1 says 700.
  - The dashed "Nuevo Sidequest" is 1px, not §4.4's 1.5.

  All of them are in `SidequestPalette.Alpha` or at the element, each with a comment
  naming its artboard.
- **1.5px borders stay 1.5px.** At 100 % scaling Chrome draws the artboards' 1.5px
  borders ("Enviar objetivo", the dashed "Añadir item", the checkbox) as 1px. WPF keeps
  the stated value, which is also what a HiDPI screen shows.
- **No artboard shows a scrollbar**, so the `ScrollBar` style has no reference and follows the
  §4.2 tokens only. It takes layout width, so cards narrow by 12px once the list overflows.

## After the phase 3 cleanup

The cleanup is done, and the phases after it build on what it left:

- **WeeklyReminder is gone from this branch**: `PopupWindow`, `EditorWindow`, the old
  `SettingsWindow`, `WeekPlan` and `TaskItem`, along with their brushes, styles and
  strings. The schedule fields left `AppSettings` too. A settings file that still
  carries them loads fine, since unknown JSON properties are skipped, and `StorageTests`
  holds that. `LegacyMigration` never used the old classes, so the import is untouched.
- **There is no settings screen until phase 5.** Theme and language come from
  `settings.json`, which `tools/run-sandbox.ps1` writes. The panel's gear stays inert, and
  the tray leaves out "Ajustes…" rather than showing an entry that does nothing.
- **The tray has 5.9's other entries.** Mostrar panel, Sidequests… and Restablecer
  posición, which now resets the panel's position. `--sidequests` opens the list from the
  command line.
- **Styles have no prefix.** A type with one look has an implicit style: `CheckBox`,
  `TextBox`, `ScrollBar` (both orientations, so text fields' own scroll viewers match),
  `ContextMenu` (with its entries, so text fields' Cut/Copy/Paste menu is themed too)
  and `ToolTip`. Everything with several looks is keyed and named for what it is:
  `PrimaryButton`, `SendButton`, `Heading`, `Muted` and so on.
- **`ConfirmDialog` is on 4.2's tokens.** It uses the panel's card, one primary action
  and no red, and it hides the detail box when the question already names its subject.
- **Strings and theme keys have a parity test.** A key missing from one of a pair used to
  fail silently on screen; now the suite catches it.
