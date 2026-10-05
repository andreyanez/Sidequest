# Dev notes

Working notes for the Sidequest rebuild. The design is in
[`design-spec.md`](design-spec.md); this file covers how to run and check the thing, and
records decisions that are not visible in the code.

Current branch: `main`, which the rebuild's `sidequest` branch was merged into on
2026-09-30. Since that day Sidequest is installed in `%LOCALAPPDATA%\Sidequest\` and in
daily use, and WeeklyReminder is retired (see "The switchover" under Phase 6).

**Status:** phases 0–6 are done: renames, font, palette and icons; data and import;
shell and main panel; navigation, the Sidequests list, the new-Sidequest form and the
Sidequest detail; the objective detail with its notes, files and images; the settings
window and the tray; the light-theme and keyboard pass, short of a live Tab walk; and
the switch from WeeklyReminder. v1 is complete. Of the spec's 0.2 and 0.3 changes ("0.2
and 0.3" below), A to G are done: they shipped together as 0.3.0 on 2026-10-04, and
that build is the installed one. Phase 7, the Bitácora (v1.1), is next.

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
measure — do not read the markup.** Doing that turned up four differences the source
text does not show: the weekday is uppercased by CSS, the checkbox `border-radius: 10px`
on an 18px box clamps to a circle, a bordered box is 2px larger than its CSS `height`
unless it says `box-sizing: border-box` (0.2's cards do, 0.1's did not), and a 1.5px
border is drawn 1px wide at 100 %.

| Script | What it is for |
| --- | --- |
| `tools/seed-sandbox.ps1` | Fills a sandbox with the artboards' four Sidequests and six panel objectives, with `FnSidequestNotas`'s notes, file and images on Mudanza; `-Many` seeds `FnSidequestsCompacto`'s fourteen Sidequests instead |
| `tools/run-sandbox.ps1` | Reseeds the sandbox and launches the build in a given theme and language, at a fixed position; `-Many` or `-Empty` choose the data, `-Height` the card's height, `-Collapsed` starts on the one-row panel, `-Corner` in the default bottom-right corner, `-Exe .\publish\Sidequest.exe` a Release build, and `-Stop` only stops the last copy, which frees the build for `dotnet build`. It stops only the copy it started, never a trial run of the same build |
| `tools/render-artboard.ps1` | Renders an artboard to PNG at 1:1 with headless Edge; `-Light` renders any of them in the light theme, and `-WithoutJournal` takes the Bitácora button out of a Sidequest's band, as the app leaves it until v1.1 |
| `tools/capture-window.ps1` | Screenshots the window rect of a copy started from the repo (never the desktop, never the installed copy); `-Untitled` takes a popup, such as the "⋯" menu, and `-BinDir` picks one build folder's copy when a trial run of `bin\Debug` is up too, as the other tools' `-BinDir` does |
| `tools/compare-ink.ps1` | Where each element is drawn, artboard vs app, to the pixel (or per-row, for text) |
| `tools/compare-colours.ps1` | Where the fills disagree, artboard vs app, grouped by colour pair: the wrong token, where the layout already matches |
| `tools/uia-do.ps1` | Invokes, focuses, selects or types into a control by its accessible name |
| `tools/send-keys.ps1` | Real keystrokes to whichever window can take them, the dialog when one is up |
| `tools/tab-walk.ps1` | Presses Tab and reports what really has focus each time |
| `tools/uia-tree.ps1` | Accessibility tree, flagging focusable controls with no name |
| `tools/make-icon.ps1` | Draws `Sidequest.ico`, the exe's icon, from the tray's mark |

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

## Releasing

`CHANGELOG.md` collects every change a user would notice under `[Unreleased]` as it is
committed. A release:

1. Moves those lines under a new `## [x.y.z] - YYYY-MM-DD` heading, updates the
   comparison links at the bottom, and sets `<Version>` in `Sidequest.csproj` to match.
   Fixes alone raise the last number, a feature the middle one; the Bitácora is 1.1.
2. Is committed as "Release x.y.z" and tagged `vx.y.z`. The user pushes, tags included.
3. Is installed by updating the installed copy (see "To update the installed copy"
   under Phase 6) with a fresh publish.

0.1.0 is the build installed on 2026-09-30, tagged after the fact. 0.3.0, the spec's
0.2 and 0.3 together, was released and installed on 2026-10-04, with the data folder
copied to `%APPDATA%\Sidequest.backup-20261004\` first.

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
- **The empty panel hides the footer's "Sidequests →"** (the user's call, 2026-09-30).
  §5.1 puts that button under the empty-state text as well, and §4.4 allows one primary
  button per screen, so only one ever shows: the empty state's while the panel is
  empty, the footer's otherwise. Coming back from the list, focus goes to that one.
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

### After 0.1.0

From the user testing the installed copy:

- **Fixed: expanding the panel left it cut short.** The window's `MinHeight` of 384 was
  switched on and off with the collapsed state. Raising it while the 76px row showed
  forced the window to exactly 384, and from then on `SizeToContent` stopped growing it,
  though it stayed set. The minimum now sits on the expanded panel's own grid (358,
  inside the card's border), so the window just fits whichever view is showing.
- **The collapse chevrons are inverted** (the user's call, 2026-10-02): down on the panel
  to collapse it, up on the collapsed row to expand it, where `FnMain` and `FnMainMini`
  draw up and down. The panel keeps its bottom edge when it collapses, so it rolls down.
- **The exe has an icon** (the user's call, 2026-10-02). §5.9 has the tray icon drawn at
  runtime so the project ships no binaries, but Windows reads an exe's icon from the file
  before the app runs. `Sidequest.ico` is drawn by the same code, through the app's own
  `--write-icon`, by `tools/make-icon.ps1`. Its colour is the tray's on the machine that
  drew it, light for a dark taskbar; run the script again after changing the mark.

## 0.2 and 0.3

The spec's 0.2 and 0.3 (2026-10-03) set no order of their own. Agreed with the user on
2026-10-04, A to E first:

- **A. Data.** Notes, files and images move from items to Sidequests (D10: a 0.1.0
  file's item-level fields are ignored and dropped on the next save; the user's real
  data had none). `MainHeight` joins the settings. §7's new strings, and the counter's
  go.
- **B. Window.** A fixed height the user drags from the bottom edge, the same on every
  screen; each view a fixed header, a scrolling middle and a fixed footer, with 4.4's
  scroll cues.
- **C. Sidequests list.** Compact buttons (D9), "Nuevo Sidequest" fixed in the footer,
  the form taking its place there.
- **D. Sidequest detail.** Items and Notas tabs, Notas taking over the objective
  detail, which goes; the new add-item field.
- **E. Rename**, on the panel and on items.
- **F. Reorder** by dragging and from the keyboard (D8). Until then the handle's space
  was reserved, as 4.4 says, with nothing drawn in it: the spec shows no dead controls.
- **G.** Both themes, both languages and the keyboard over everything, then a release.

Conflicts settled in the same exchange:

- **Compact buttons everywhere.** `FnSidequests`, `FnSidequestNuevo` and
  `FnSidequestsLargoNuevo` still draw the 64px button D9 turned down; the 42px one of
  `FnSidequestsCompacto` is built.
- **The chevrons stay inverted** (see "After 0.1.0"), though 4.4, `FnMain` and
  `FnMainMini` still draw them up to collapse.
- **Versions follow the spec:** these ship as 0.2.0 and 0.3.0, the fixes under
  `[Unreleased]` going out with 0.2.0. In the event A to F were built together, so
  there was never a build with 0.2 alone, and the user chose a single 0.3.0 for all of
  it, the fixes included (2026-10-04).

### A to E, as built

- **A. Data.** A 0.1.0 `sidequests.json` loads with its items' notes, files and images
  left out, and the next save drops them (D10); `SidequestStoreTests` holds that.
  Image copies belong to Sidequests now: deleting one deletes its copies, but removing an
  image keeps a copy another Sidequest still uses. `Settings.MainHeight` is the card's
  height, 640 by default, which a 0.1.0 settings file gets.
- **B. Window.**
  - The window is the card's height plus the 24 of shadow, the card held between 5's
    360 and the work area less the shadow. Dragging the bottom 6px moves the bottom edge
    only, as far as the work area's bottom, and the height is saved on letting go, from
    the window's actual height: near the screen's full height Windows stops a window
    short of what was asked.
  - 4.4's scroll cues are `ScrollCues` plus the `ScrollArea` style. The thumb is
    `FnSidequestsCompacto`'s `#4A4E56`, which 4.2 does not list. Light has no value
    anywhere, so it is `#B4B4AE`, just past `Line2` toward `Text2`, as the dark one is.
  - The resize grip is drawn only where the artboards draw it, in the Sidequests list's
    footer; on the other screens the edge works with nothing drawn.
  - String not in §7: `L_ResizePanel`, the edge's accessible name.
- **C. Sidequests list.** As `FnSidequestsCompacto` (D9); the counter and its strings
  (`L_ActiveFmt`, `L_ActiveOneFmt`) are gone. In the form, Cancelar is 36 and Crear 34,
  top-aligned: both say 34, but Cancelar's border is outside it (content-box). The
  buttons' and the form's tints run under their translucent borders, a rule now in
  CLAUDE.md.
- **D. Sidequest detail.**
  - The tabs are `SidequestTabControl`. Only the chosen tab is a tab stop, because a tab
    that takes focus takes the selection with it: Tab passing through Items would switch
    to it. From a focused tab the arrow keys, Home and End move between them, and
    Ctrl+Tab works from anywhere inside; Home and End pressed elsewhere stay with what has
    focus, where the stock control would jump tabs. The chosen tab is remembered per
    Sidequest until the app quits (5.6). A new Sidequest opens on Items with the add field
    open, as before.
  - A `TabItem` hands its font and colour down to its content, which here is the rest of
    the screen: every unstyled text in Items came out bold. The tab style sets them on
    the header's presenter instead, and the Items header is a template rather than an
    element, since an element header belongs to the `TabItem` and skips the presenter.
  - Notas is `NotesTab`: the objective detail's code, moved to the Sidequest, with the
    notes 176 high. The band still leaves out the Bitácora button until v1.1, so
    everything under it sits 50px higher than the artboards draw it;
    `render-artboard.ps1 -WithoutJournal` renders them to match.
  - Cards are 48 with their border, completed rows 40. Hover actions wait at rest with no
    width, no opacity and no hit-testing, rather than collapsed: unseen and taking no
    room or clicks, as the artboards' `display: none`, yet still tab stops and still
    there for a screen reader. Tab meets them in the order they are drawn, and the
    current objective, whose only action is rename, can be reached at all. The panel's
    cards do the same. Clipping them instead would clip their focus rings, which WPF
    clips to the focused control's clipping ancestors.
  - Adding (`FnSidequestAdd`): Enter adds at the end of the pending items, lights the
    card for 1.5 s (`C @ 8 %` under a `C @ 50 %` border), scrolls to it and keeps the
    field. Leaving an empty field closes it, including a click on something that cannot
    take focus. The window losing focus to another app does not: the next name may be
    on its way from there.
- **E. Rename** (`FnMainEdit`): the pencil, or F2 on a focused card. Enter, the check,
  leaving the card or a click elsewhere saves; Escape or the x cancels; a blank name keeps
  the old one (`SidequestRules.RenameItem`). Moving focus between the field and the
  card's own check and x is not leaving, and nor is the window losing focus; the
  Sidequest title's rename now waits for that too.
  - No artboard shows an item being renamed in the detail: it is `FnMainEdit`'s card
    with the detail's 6 for the panel's 8.
  - A screen reader hears the field as "Renombrar objetivo" or "Renombrar item".
    `FnMainEdit` labels it "Nombre del objetivo", which §7 has no key for.
- **Checked:** every screen of A to E against its artboard in dark and light, and in
  English: positions with `compare-ink.ps1`, fills with `compare-colours.ps1`. Then,
  with the user's go-ahead on 2026-10-04, live with the keyboard and the pointer:
  - Tab order on the panel (per card: checkbox, chip, rename, return, delete) and in
    the detail (the chosen tab only; rename, delete, send per row; only live ticks).
  - Rename on both: F2 and the pencil, text selected on entry; Enter, the check, Tab out
    of the card and a click elsewhere save; Escape and the x cancel; a blank name keeps
    the old one; F2 on a completed item does nothing; focus returns to the pencil.
  - The tabs: arrows, Home, End and Ctrl+Tab; End on a button stays put; the chosen tab
    remembered on coming back; notes saved on switching tabs.
  - Adding: Enter adds and keeps the field, the glow measured within its 1.5 s and gone
    after; Escape closes; a click elsewhere closes an empty field.
  - Delete asks first and Escape cancels; "Enviar objetivo" moves focus to the next
    row; hover on both screens, measured against the artboards' hovered rows; dragging
    the bottom edge both ways, saved on release.

  It found one bug: the actions were clipped to hide them at rest, and WPF clipped the
  pencil's focus ring with them.

### F, as built

- **The grip is a `Thumb`**, which holds the pointer for the whole drag, and a tab stop
  of its own: the first in each card, so Alt+Up and Alt+Down can reach it (5.1). It
  shows on hover and on keyboard focus anywhere in the card, like the actions.
- **The drag, shared by both lists (`DragReorder`):** past the system's drag distance
  the card lifts - Surface, a Line2 border, a shadow 10 down at 45 % and 6 to the right,
  as `FnMainDrag` draws it - and follows the pointer up and down, held within its span:
  the whole panel, or its own group of the detail (5.6). The cards between slide aside
  in 110 ms (4.4: at most 120), and a gap opens where it would land, dashed in its
  Sidequest's colour over `C @ 6 %`, drawn on a canvas behind the cards so the card
  passes over it. Letting go slides it into the gap and saves; Escape, or losing the
  pointer to another window, slides everything back. Afterwards keyboard focus is on
  the grip of the card that moved.
- **Not in the spec:** held near the top or bottom of a list that scrolls, the card
  scrolls it. Without that, a card could not reach a place out of view.
- **The orders.** The panel is numbered afresh from 0 in its new order. In a Sidequest
  the group's items trade the `SortIndex` values they hold, so the other groups keep
  theirs, and an item that changes group later - ticked, unticked, sent, returned - comes
  back near where it was. Equal indexes, which no version writes but a hand-edited file
  could hold, are spread out first.
- **The two lists that drag pad their content, not their scroll area.** `ScrollArea`'s
  `Padding` sits outside the clip, which cut off the lifted card's right edge and its
  shadow; `FnMainDrag`'s list clips at its outer edge, as CSS does. Content scrolls into
  that margin too now, as it would in CSS.
- **The shadow's blur is 36.** CSS's 24 is a Gaussian of sigma 12, and WPF's
  `BlurRadius` is about three sigmas. At 24 the shadow was dark under the card and gone
  too soon; at 36 it matches `FnMainDrag` to a level, in light, where a shadow shows.
- **Checked live**, on a copy of its own beside the user's trial run: the held drag
  against `FnMainDrag` in dark and light (positions to the pixel, shadow and gap to a
  level), dropping and the saved order, Escape, Alt+Up and Alt+Down at the ends of the
  panel and of a group, a pending item held against the current objective above it,
  scrolling at the edge, hover against `FnSidequest`'s hovered row, and the Tab order
  with the grips. Tests go from 94 to 99. The grip's dots render a little lighter than
  Chrome's, on the same pixels, as text does. **Not checked live:** losing the pointer
  mid-drag, which `Thumb` turns into a cancel itself; forcing it would have meant
  switching the user's foreground window during a drag.

### G, the final pass (2026-10-04)

- **Every screen against its artboard, dark and light**, fills with
  `compare-colours.ps1`: the panel, collapsed, being renamed and dragged; the long
  Sidequests list; the new-Sidequest form; a Sidequest's Items and Notas; Settings. What
  differs is only what the captures didn't set up: a hovered row, today's date, a
  chosen option and a switch left on in Settings, and the darkened Amber of "Contrast
  comes first". Filled in, the form matches to the pixel, its name on the artboard's
  baseline.
- **English on every screen**, by eye: nothing overflows or is cut short that the
  Spanish doesn't cut too.
- **The keyboard** on what A to F had not walked: the Sidequests list, the form (Escape
  closes it, Enter from the swatches creates), Settings (its own Tab cycle; Escape does
  nothing) and the collapsed row. On every screen every focusable control has a name.
- **Fixed on the way:** `render-artboard.ps1 -Light` refused `FnSidequestsCompacto`,
  whose scroll fades write Bg as `rgba()`.

### After 0.3.0

- **Fixed: a card kept its grip, actions and hover fill after a drag or a click on its
  checkbox** (the user, 2026-10-04). They showed on `IsKeyboardFocusWithin`, which a
  click sets as surely as Tab does, and both leave focus in the card. 4.4 says keyboard
  focus, so they now follow `KeyboardCue`: CSS's `:focus-visible`, true only while the
  focus inside arrived by keyboard. The image cells' "Quitar" follows it too. Checked
  live after a drop; Tab revealing a card could not be, as the machine was in use.

## Known deviations from the spec

- **The import does not carry over WeeklyReminder's settings** (§3 step 3). Accepted on
  2026-09-29 as not worth fixing: theme, language and startup get set again in Settings.
  `LegacyMigration` copies them into `AppSettings`, but `App.OnStartup` applies the
  language before the import runs, overwrites `StartWithWindows` from the registry, and
  never saves afterwards. Only the week's items survive the import.
- **The grip's cursor is `SizeNS`, not `grab`.** 4.4 asks for `grab` and `grabbing`,
  which browsers draw themselves and Windows does not have. Drawing a hand at runtime, as
  the tray icon is drawn, is possible but was not worth it; the up-and-down arrows say
  what the grip does.
- **Letter-spacing is not implemented.** §4.1 asks for −0.02em on 22/700 headings and
  +0.06em on section labels; measured on the artboard that is −0.44px and +0.66px. WPF has
  no letter-spacing property, and the workaround is an attached behaviour that rebuilds
  text as per-character `Run`s.
- **Button radii.** Every artboard now draws pills except `FnAjustes`, whose segmented
  options are radius 5, built as drawn. `FnSidequestNuevo`'s Cancelar has no fill, over
  the form's tint, where §4.4 fills a secondary button with `Surface`; it is built as
  drawn.
- **Values the artboards take from outside §4.2**, all built as drawn:
  - The Sidequests list draws its progress tracks at `C @ 20 %`; the header band keeps
    §4.2's 25.
  - `FnSidequestNuevo` tints its form at 7 % with a 40 % border.
  - 5.6's just-added item, `C @ 8 %` under `C @ 50 %`, is in §5 but not in 4.2's list.

  All of them are in `SidequestPalette.Alpha` or at the element, each with a comment
  naming its artboard.
- **1.5px borders are drawn 1px** (changed 2026-10-04). Chrome floors a border to whole
  device pixels, so at 100 % the artboards' 1.5px borders ("Enviar objetivo", the dashed
  buttons, the checkbox, the add field, a card being renamed) render 1px. The earlier
  "keep 1.5" stopped being true when phase 6 turned on layout rounding, which drew them
  2px. 1 matches Chrome from 100 to 175 %. The dashed outlines are a `Rectangle`'s
  stroke, which layout rounding leaves alone, so at 1.5 they blurred across two rows.
- **The scroll areas follow `FnSidequestsCompacto`** (4.4). Text fields keep the older
  implicit `ScrollBar` style, which no artboard shows, held to §4.2's tokens.

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
