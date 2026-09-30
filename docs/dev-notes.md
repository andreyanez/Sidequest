# Dev notes

Working notes for the Sidequest rebuild. The design is in
[`design-spec.md`](design-spec.md); this file covers how to run and check the thing, and
records decisions that are not visible in the code.

Current branch: `sidequest`. Since 2026-09-30 Sidequest is installed in
`%LOCALAPPDATA%\Sidequest\` and in daily use, and WeeklyReminder is retired (see "The
switchover" under Phase 6).

**Status:** phases 0–6 are done: renames, font, palette and icons; data and import;
shell and main panel; navigation, the Sidequests list, the new-Sidequest form and the
Sidequest detail; the objective detail with its notes, files and images; the settings
window and the tray; the light-theme and keyboard pass, short of a live Tab walk; and
the switch from WeeklyReminder. v1 is complete. Next are the tweaks the user has
collected from using it, then phase 7, the Bitácora (v1.1).

---

## Run against a sandbox, never your real data

`%APPDATA%\Sidequest\` is live data: the Sidequests the installed copy works on every
day. `%APPDATA%\WeeklyReminder\` holds the retired app's files, kept for a rollback. A
dev run must not be able to reach either.

Set `SIDEQUEST_DATA` to a scratch folder and `App.RedirectDataIfSandboxed()` repoints
both stores under it:

```powershell
$env:SIDEQUEST_DATA = "$env:TEMP\sq-sandbox"
.\tools\seed-sandbox.ps1 -Sandbox $env:TEMP\sq-sandbox
dotnet build; Start-Process .\bin\Debug\net8.0-windows\Sidequest.exe
```

It is read **first** in `OnStartup`, before settings are loaded — an earlier ordering bug
had a sandboxed run reading real settings and writing them back.

A sandboxed run also has a single-instance lock of its own, named after its folder, so
it starts beside the installed copy instead of handing its launch to it. Its tray icon's
tooltip names the folder: "Sidequest (sq-sandbox)". The tools attach only to a copy
started from inside the repo, never to the installed one.

The sandbox covers "Abrir al iniciar el equipo" too: under `SIDEQUEST_DATA`, and in the
test suite, `AutoStart` keeps on/off in a file instead of the `Run` key. A dev build
registered there would start at the next sign-in without the sandbox, on real data, in
place of the installed copy, whose entry has the same name.

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
| `tools/seed-sandbox.ps1` | Fills a sandbox with the artboards' four Sidequests, six panel objectives, and `FnObjetivo`'s notes, file and images |
| `tools/run-sandbox.ps1` | Reseeds the sandbox and launches the build in a given theme and language, at a fixed position; `-Collapsed` starts on the one-row panel, `-Exe .\publish\Sidequest.exe` a Release build. It stops only the copy it started, never a trial run of the same build |
| `tools/render-artboard.ps1` | Renders an artboard to PNG at 1:1 with headless Edge; `-Light` renders any of them in the light theme |
| `tools/capture-window.ps1` | Screenshots the window rect of a copy started from the repo (never the desktop, never the installed copy); `-Untitled` takes a popup, such as the "⋯" menu |
| `tools/compare-ink.ps1` | Where each element is drawn, artboard vs app, to the pixel (or per-row, for text) |
| `tools/compare-colours.ps1` | Where the fills disagree, artboard vs app, grouped by colour pair: the wrong token, where the layout already matches |
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

Look at shapes as well as measuring boxes. Through phases 2 and 3 every pill rendered as
an ellipse, whose bounding box is the same as a pill's, so no measurement caught it; a 4x
zoom of one chip did at once.

Four traps. The first three produced confident wrong answers before; the fourth
hijacks the keyboard of whoever is using the machine:

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
- **A dialog takes the keyboard.** `ShowDialog` activates the app, so opening
  `ConfirmDialog` through UIA pulls keyboard focus from whatever the person at the
  machine is typing into, and their Enter would answer it. Cancel it straight away and
  stop the sandbox copy, which hands focus back. Captures don't have this problem:
  `capture-window.ps1` doesn't foreground a topmost window, and `run-sandbox.ps1` keeps
  the panel on top, so every window the app opens is one.

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

### Phase 4

- **The notes are a `RichTextBox`**, rebuilt from plain text: one paragraph per line, with
  every http(s) address a link in the Sidequest's colour. New addresses turn into links
  when the field is left, as §5.3 says. The counter counts a line break as one character:
  `FnObjetivo`'s "129 / 500" is exactly its three lines and two breaks.
- **Opening links.** Ctrl+click opens a link, and so does a plain click while the field
  doesn't have focus (§5.3). **Ctrl+Enter** opens the address under the caret, which is
  the keyboard's way in; the spec doesn't give one. Only http and https ever open.
- **The 500 cap has two layers.** A `RichTextBox` has no `MaxLength`. Keystrokes, Enter and
  pastes are held to the room left. The field also queues typed characters and applies
  them in batches, so fast typing got past that check by five, and whatever lands over
  500 is trimmed from just before the caret. Pastes arrive as plain text.
- **Image copies are deleted** when their image is removed, and when their objective or
  Sidequest is deleted. There is deliberately no sweep for unreferenced files. A
  quarantined `sidequests.json` loads as empty, and a sweep would then delete every image.
- **Things no artboard shows:**
  - "Quitar" on a thumbnail is a small disc in its corner, shown on hover or focus.
  - An image dragged over the panel lights up the "+" cell it will land in.
  - A file row opens its file on click but has no hover state, since `FnObjetivo` draws
    none. A file that has gone reads in Text2, with "No se encuentra el archivo" as its
    tooltip.
- **String not in §7:** `L_ImageFmt` ("Imagen 2"), the thumbnails' accessible names.
- **The panel's objective titles are buttons** now (§5.1), turning Text2 on hover as
  `FnMain`'s `a:hover` does. Back from the detail, focus returns to that title. After a
  delete or a return on the panel, focus moves to the next objective instead of dropping
  to the window.
- **The objective view uses layout rounding.** Its 19px title sits on a 24.7px line,
  which left everything below it on fractional pixels, where 1px borders smeared across
  two rows. Chrome snaps boxes to whole pixels when it paints; `UseLayoutRounding` does
  the same. Phase 6 turned it on for every window.
- **Fixed on the way:**
  - Every pill in the app rendered as an ellipse. The artboards write
    `border-radius: 999px`, which CSS scales down evenly, but WPF scales it separately
    along each side. `PillRadiusConverter` gives half the height instead.
  - A `RichTextBox` gives any document assigned to it a 5px page padding each side,
    whatever the document says. The notes set it on the document the field already
    owns.
- **Not checked live:** opening a link, a file or an image, since each would launch an
  app on the desktop; dragging an image in; and paste truncation, which would overwrite
  the clipboard. The first three are one-line shell calls. An oversized paste that got
  past its own check would meet the same trim that stopped fast typing, which is checked.

### Phase 5

- **From `FnAjustes`, where it differs from §4:**
  - "Ajustes" is 22/600.
  - The segmented options are radius 5, with the chosen one 13/600.
  - The switches have no hover state.
  - A row's title and description act as its switch's label, so clicking them flips it.
- **Language names are written in their own language** (Español, English), so they have
  no string keys.
- **"Sistema" follows Windows while the app runs.** It reads Windows' light/dark setting
  for apps, and a change to it repaints every window at once.
- **The tray icon** is §5.9's rounded square with the "S" cut out of it, in 4.2's Text.
  Its colour follows the **taskbar's** light/dark setting rather than the app's.
  Windows keeps the two apart, and a light app theme often sits on a dark taskbar,
  where a dark icon would vanish. It is drawn at the notification area's own size, so
  it isn't scaled down into a blur. The Settings window's taskbar button uses the same
  mark.
- **Settings opens centred on the panel's screen**, not on the panel: centred on a panel
  in its usual corner, a 720px window would hang off the edge. Its "‹ Panel principal"
  closes it and brings the panel back. As in the sub-panels, Escape does nothing.
- **The panel's pin and "siempre visible" are one setting**, and each follows the other
  through `App.SettingsChanged`.
- **Not checked live:** the tray menu, which WinForms draws outside the app's windows;
  a Tab walk through Settings, skipped because the machine was in use (UIA's order of
  its controls was checked instead); and a real `Run` key write, which only happens
  outside the sandbox.

### Phase 6

- **Light references are generated.** Only `FnMain` has a light artboard. The others are
  rendered light by `render-artboard.ps1 -Light`, which swaps §4.2's dark values for the
  light ones in a copy. Run on `FnMain`, it reproduces `FnMainClaro` exactly: the same
  text, and not one pixel different. `Bg` and `Inv` are one colour in dark, so the
  script tells them apart by use: text and the switch's knob are `Inv`.
- **Every screen matches its artboard in both themes**, checked with
  `compare-colours.ps1` over the fills and `compare-ink.ps1` over positions. What still
  differs is known: the darkened Amber (see "Contrast comes first"), states an artboard
  shows that the capture didn't (a hovered row, a chosen option, a switch left on), and
  the images `FnObjetivo` stands in for.
- **Every window uses layout rounding**, set in the `AppWindow` style. Matching `FnMain`'s
  date put the panel's cards on fractional pixels, and the first card's top border
  vanished. No screen can hit that again now.
- **`FnMain`'s header, measured rather than read:**
  - The list pads its top by 12, so the cards were 12px high.
  - The date sits 6 under the weekday on a 1.15 line. WPF's `LineHeight` would scale its
    baseline and set it a pixel high, so it keeps its natural line, with margins that
    centre it as CSS does.
  - The weekday sits on the counter's baseline, a pixel below where top alignment puts it.
- **The panel's gear is a 36px round button** (`RoundIconButton`), as `FnMain` draws it,
  not the title bar's 28px one.
- **Spacing that had drifted from the artboards:** the title bar's buttons sit 2 apart;
  the collapsed row spaces its date, counter and buttons 12 apart, with its buttons 2
  apart; a hovered card's actions sit 12 after the chip and 2 apart, inside the card's
  padding. The collapsed date also moved down a pixel. Centred in the row, it falls at
  17.5, which Chrome and WPF round opposite ways.
- **`ConfirmDialog` opens with focus on its default button**, as a Windows message box
  does, so the ring shows what Enter will do. It shows only after keyboard input.
- **The empty panel hides the footer's "Sidequests →" while there are no Sidequests**
  (the user's call, 2026-09-30). §5.1 puts that button under the empty-state text as
  well, and §4.4 allows one primary button per screen. With Sidequests but nothing on the
  panel, as right after the import, both show, as §5.1 says. Coming back from the list,
  focus goes to whichever of the two was used to leave.
- **The keyboard, checked through UIA:** every focusable control on every screen has a
  name, the hover-only actions come up for keyboard focus, and the focus ring is `Text`
  in both themes.
- **Not checked live:** a Tab walk, because it needs the keyboard and the foreground
  while the machine was in use; and tooltips, which need the pointer. Their style uses
  `Surface`, `Line2` and `Text` only.

#### The switchover (2026-09-30)

WeeklyReminder ran from `%LOCALAPPDATA%\WeeklyReminder\` and started from a per-user
`Run` entry of the same name. In order:

1. WeeklyReminder was stopped and its `Run` entry removed. Its tray icon lingers until
   the pointer passes over it.
2. `%APPDATA%\WeeklyReminder\` was copied to `%APPDATA%\WeeklyReminder.backup-20260930\`,
   checked file by file.
3. The Release build was installed into `%LOCALAPPDATA%\Sidequest\`, with a `Run` entry
   in the form `AutoStart` writes, and started without `SIDEQUEST_DATA`.
4. The import made "Importados" (Slate) from all 19 items, texts and ticks identical and
   in order, and renamed `week.json` to `week.json.migrated`.

`%APPDATA%\Sidequest\` had existed, empty, since the test-suite incident of 2026-09-20;
the import only checks for `sidequests.json`, so it made no difference.

**To roll back:** turn off Sidequest's "Abrir al iniciar el equipo" and quit it from the
tray; rename `week.json.migrated` back to `week.json`; start
`%LOCALAPPDATA%\WeeklyReminder\WeeklyReminder.exe` and turn its own start-with-Windows
option back on, which rewrites its `Run` entry. Anything done in Sidequest since stays in
`%APPDATA%\Sidequest\` and is not carried back.

**To update the installed copy:** quit it from the tray, publish, copy `publish\*` over
`%LOCALAPPDATA%\Sidequest\`, and start it again. Its data and `Run` entry stay as they
are.

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
- **The artboards disagree with each other on button radius.** `FnMain` and `FnSidequest`
  use pills (`999`); `FnSidequestNuevo` uses `10` and `FnAjustes` uses `5`. The built
  styles are pills, matching §4.4 and the two panel screens. That includes
  `FnSidequestNuevo`'s Cancelar and Crear, which it also draws 38 high where §4.4 says
  34 and 36, and its Cancelar with no fill, over the form's tint, where §4.4 and
  `FnSidequest`'s "Bitácora" button fill a secondary button with `Surface`.
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
- **The tray has all of 5.9's entries**, "Ajustes…" since phase 5. "Restablecer
  posición" resets the panel's position. `--sidequests` and `--settings` open those
  screens from the command line, and `tools/run-sandbox.ps1 -AppArgs` passes them.
- **Styles have no prefix.** A type with one look has an implicit style: `CheckBox`,
  `TextBox`, `ScrollBar` (both orientations, so text fields' own scroll viewers match),
  `ContextMenu` (with its entries, so text fields' Cut/Copy/Paste menu is themed too)
  and `ToolTip`. Everything with several looks is keyed and named for what it is:
  `PrimaryButton`, `SendButton`, `Heading`, `Muted` and so on.
- **`ConfirmDialog` is on 4.2's tokens.** It uses the panel's card, one primary action
  and no red, and it hides the detail box when the question already names its subject.
- **Strings and theme keys have a parity test.** A key missing from one of a pair used to
  fail silently on screen; now the suite catches it.
