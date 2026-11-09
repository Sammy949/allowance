# Technical plan

Status: draft, awaiting approval.

Goal: click tray icon, drawer shows Claude plan usage, refresh fetches fresh numbers.

## Stack

.NET 10, WinUI 3, WinUIEx, unpackaged, x64. Same as Spare Hand, without its Community Toolkit and shell code.

## Reused from Spare Hand (copied, not shared)

| File | Use |
|---|---|
| `Shell/NotifyIcon.cs` | Tray icon, click handling, Explorer restart recovery, DPI. Menu cut to Refresh and Quit |
| `Tray/PopupChrome.cs` | Frameless, tool window, always on top |
| `Tray/Placement.cs` | Near-cursor placement within the work area |
| `Backdrops/AlwaysActiveDesktopAcrylicBackdrop.cs` | Card material |
| `Native/Win32.cs`, `app.manifest`, card style | Interop, PerMonitorV2, radius 8 card |
| `Hold/HoldLibrary.xaml.cs` open/close pattern | Takes focus, closes when focus leaves |

## New code

```
src/Allowance/
  Usage/UsageSource.cs    runs the CLI, 30 s timeout, one fetch at a time
  Usage/UsageParser.cs    text to UsageSnapshot, pure and tested
  Usage/UsageStore.cs     latest snapshot, stale flag, last-good cache on disk
  Drawer/DrawerWindow.*   the card
  Drawer/UsageRow.*       label, reset line, bar, percent
  Shell/TrayNumberIcon.cs draws the percent into the tray icon
tests/Allowance.Tests/    parser fixtures
```

## Parser

Label-agnostic, one match per line, so new per-model lines appear without a code change:

`^(?<label>.+?): (?<pct>\d+)% used · resets (?<when>.+?) \((?<tz>[^)]+)\)$`

If nothing matches: keep last good numbers, mark them stale, show the error.

## When it fetches

Fetch on launch, on refresh, and when the drawer opens with data over 5 minutes old. No background polling in v1. Old numbers stay on screen with a spinner during the 5 to 6 s fetch.

## States

Loading first time, fresh, refreshing, stale ("Updated 12 min ago"), logged out, WSL or CLI missing, unrecognized output.

## Milestones

**M0: Confirm the data.** Check freshness against claude.ai, then capture cold start and logged-out output as fixtures. Done when each is written up in `02-data-source.md`.

**M1: Parser and fetch.** Done when tests pass on real fixtures and a console run prints correct numbers.

**M2: Tray and drawer with live data.** Done when click opens, click-away and Esc close, refresh works, and it's checked at 100% and 150% scaling in light and dark.

**M3: Number in the tray, start with Windows.** Done when the icon matches the drawer and survives an Explorer restart.

## Out of scope for v1

History, charts, alerts, other providers, installer, native Windows Claude login.
