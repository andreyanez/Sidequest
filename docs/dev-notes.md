# Dev notes

Working notes for the Sidequest rebuild. The design is in
[`design-spec.md`](design-spec.md); this file covers how to run and check the thing, and
records decisions that are not visible in the code.

Current branch: `sidequest`. WeeklyReminder stays installed and in daily use until
phase 6 completes.

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
redirects both stores from a `[ModuleInitializer]`, and parallelism is disabled in
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
| `tools/capture-window.ps1` | Screenshots the app's own window rect (never the whole desktop) |
| `tools/tab-walk.ps1` | Presses Tab and reports what really has focus each time |
| `tools/uia-tree.ps1` | Accessibility tree, flagging focusable controls with no name |

Two traps, both of which produced confident wrong answers before:

- **Foreground stealing.** `SetForegroundWindow` fails silently when another process owns
  the last input, and the keystrokes go to that app instead. `tab-walk.ps1` borrows the
  foreground thread's input queue, and skips a step rather than reporting a bogus result.
- **UIA does not always see a modal dialog.** Querying `RootElement` children for the
  process missed an open `ConfirmDialog` entirely, which read as "the Delete key does
  nothing". `capture-window.ps1`'s `EnumWindows` found it immediately. Confirm a window
  exists with `EnumWindows`, not UIA.

## Decisions

Artboards win wherever they conflict with the spec's prose — with the one exception
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

`ContrastTests` enforces this over all eight themes × both app themes × the five surfaces
a theme colour carries text on (`Bg`, `Surface`, the 14 % header band, the 9 % list tint,
and `Inv` reversed out of a solid fill). It reads the surface tokens straight from
`Themes/*.xaml`, so editing a colour there is what it measures. All 80 combinations pass;
the tightest is Teal light on its own header band at **4.51**, so treat that one as having
no headroom. If a new colour fails, darken the light value or lighten the dark one — do
not lower the threshold.

## Known deviations from the spec

- **Letter-spacing is not implemented.** §4.1 asks for −0.02em on 22/700 headings and
  +0.06em on section labels; measured on the artboard that is −0.44px and +0.66px. WPF has
  no letter-spacing property, and the workaround is an attached behaviour that rebuilds
  text as per-character `Run`s.
- **The notes field has no line-height.** §5.3's artboard uses a 1.55 line box. A WPF
  `TextBox` cannot carry one (`LineHeight` is a `Block` property). §5.3 already suggests a
  `RichTextBox` for URL detection, which can, so phase 4 should use that.
- **`ConfirmDialog` still uses the WeeklyReminder palette**, including a red that appears
  nowhere in §4. It survives the rewrite (§6) so it needs restyling to the §4.2 tokens.
- **The artboards disagree with each other on button radius.** `FnMain` and `FnSidequest`
  use pills (`999`); `FnSidequestNuevo` uses `10` and `FnAjustes` uses `5`. The built
  styles are pills, matching §4.4 and the two panel screens.
- **No artboard shows a scrollbar**, so `Sq.ScrollBar` has no reference and follows the
  §4.2 tokens only. It takes layout width, so cards narrow by 12px once the list overflows.

## Phase 3 cleanup

The old WeeklyReminder screens, the `Sq.` style prefix and the legacy brushes in
`Themes/*.xaml` all come out at the end of phase 3. Until then both palettes coexist:
the `Sq.*` styles are keyed rather than implicit precisely so the old screens keep
building.
