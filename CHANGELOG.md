# Changelog

What changed in each version of Sidequest, newest first. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and version numbers follow
[Semantic Versioning](https://semver.org/): a fix raises the last number, a new feature
the middle one. The version is `<Version>` in `Sidequest.csproj`, and Settings shows it
in its footer.

## [Unreleased]

### Added

- Sidequest has its own icon, the same mark as in the tray, in the Start menu, on the
  taskbar and in File Explorer.

### Changed

- The collapse and expand buttons point the way the panel moves: down to collapse it,
  since it keeps its bottom edge in place, and up to expand it again.

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

[Unreleased]: https://github.com/andreyanez/Sidequest/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/andreyanez/Sidequest/releases/tag/v0.1.0
