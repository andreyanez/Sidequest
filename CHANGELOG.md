# Changelog

What changed in each version of Sidequest, newest first. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and version numbers follow
[Semantic Versioning](https://semver.org/): a fix raises the last number, a new feature
the middle one. The version is `<Version>` in `Sidequest.csproj`, and Settings shows it
in its footer.

## [Unreleased]

### Changed

- Notes hold up to 2000 characters, up from 500.
- The panel's height is set from its top edge instead of its bottom. The bottom stays
  where it is, as it does when the panel collapses, so a panel resting on the bottom of
  the screen grows upward. The bar at the foot of the Sidequests list, which marked the
  old edge, is gone.

## [0.3.1] - 2026-10-04

### Fixed

- After dragging a card, or ticking or unticking its checkbox, its drag handle, buttons
  and highlight stayed on screen until something else was clicked. They now show only
  while the pointer is over the card, or when you reach it with the keyboard.

## [0.3.0] - 2026-10-04

After a few days of use: notes move to the Sidequest, the panel keeps the height you
give it, and objectives and items can be renamed and put in order.

### Added

- Sidequest has its own icon, the same mark as in the tray, in the Start menu, on the
  taskbar and in File Explorer.
- Rename objectives and items in place: the pencil that appears on hover, or F2. Enter
  saves, Escape cancels.
- The panel's height is yours to set: drag its bottom edge. Every screen keeps it, and
  long lists scroll, with a fade showing there is more.
- A Sidequest has two tabs: Items, and Notes, which holds its notes, links, files and
  images.
- Adding items: Enter adds one and leaves the field ready for the next, and the new item
  lights up for a moment.
- Put objectives and items in the order you want: drag a card by the ⋮⋮ handle that
  appears on hover, or press Alt+↑ and Alt+↓ on the handle. In a Sidequest, items move
  within their own group, and completed ones stay put.

### Changed

- The collapse and expand buttons point the way the panel moves: down to collapse it,
  since it keeps its bottom edge in place, and up to expand it again.
- Notes, files and images belong to a Sidequest now, not to each objective, and an
  objective's title no longer opens a page of its own. Any that 0.1.0 kept on an
  objective are not carried over.
- The Sidequests list is more compact, with "New Sidequest" always at the bottom.

### Fixed

- Expanding the panel after collapsing it left it cut short, hiding objectives and the
  buttons at the bottom.

## [0.1.0] - 2026-09-30

The first version, replacing WeeklyReminder.

### Added

- A panel that stays on screen with the objectives you send to it. Tick them off there,
  open one, or return it to its Sidequest. The panel collapses to a single row, and
  remembers where you left it.
- Sidequests: side projects, each with a name, one of eight colours, its own list of
  items and its progress. Any pending item can be sent to the panel as an objective.
- Objectives keep notes, where web addresses become links, up to five linked files and
  up to five images. Images are copied into Sidequest's own folder, so the originals can
  move.
- Light and dark themes, or following Windows; Spanish and English.
- Settings: appearance, language, open at startup, and keep the panel on top.
- A tray menu: show the panel, Sidequests, Settings, reset the panel's position, quit.
- Every screen works from the keyboard (Tab, Enter, Escape, Delete) and with a screen
  reader.
- On first start, WeeklyReminder's list is imported into a Sidequest of its own.

### Removed

- WeeklyReminder's scheduled pop-ups, snooze and auto-close: the panel stays open
  instead.
- The weekly rollover. Objectives stay until you remove them.
- Reordering by dragging. Lists keep the order items were added or sent in.

[Unreleased]: https://github.com/andreyanez/Sidequest/compare/v0.3.1...HEAD
[0.3.1]: https://github.com/andreyanez/Sidequest/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/andreyanez/Sidequest/compare/v0.1.0...v0.3.0
[0.1.0]: https://github.com/andreyanez/Sidequest/releases/tag/v0.1.0
