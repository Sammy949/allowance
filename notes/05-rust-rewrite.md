# Rust rewrite plan

Status: planned, not started. The trimmed C# build stays the shipping version until the Rust one beats it.

## Why

| | C# trimmed (measured 2026-11-09) | Rust target (estimate, to be measured) |
|---|---|---|
| Installed | 92 MB | under 10 MB |
| Idle memory | about 128 MB | under 30 MB |

## What carries over

| Part | Lines of C# | Difficulty |
|---|---|---|
| Parser, wording, pace, refill logic, 40 tests | 675 + 292 tests | Direct port |
| CLI runner, cache, log | included above | Direct port |
| Tray icon, menu, placement, notifications | 750 | Same Win32 calls through `windows-rs` |
| Installer, release workflow | | Small edits |
| Drawer layout, sizes, colours, copy | | Values carry over |
| Drawer rendering | 686 | Rebuilt |

## What WinUI gives for free and Rust won't

Text layout and wrapping, acrylic, card shadow, smooth bars, scrolling, tooltips, focus rings, hover states, light and dark, DPI changes, and screen reader support. The C# drawer code doesn't transfer. This is most of the work and most of the risk.

## UI options

| Option | Size | Memory | Effort | Look |
|---|---|---|---|---|
| `windows-rs` with Direct2D, DirectWrite and a DWM backdrop | Smallest | Lowest | Highest | Closest to native, hand-built controls |
| Slint | Small | Low with the software renderer | Medium | Good, acrylic unproven |
| Tauri (as in glance) | Small | WebView2 processes, may not beat today by much | Lowest | Easy to match in HTML and CSS |

Recommendation: `windows-rs`. The drawer is one window with few controls, and low memory is only reachable without a web view. Tauri is the fallback if the goal turns out to be download size alone.

## Phases

1. **Spike, go or no-go.** One borderless window with acrylic, rounded corners, shadow, a bar and DirectWrite text, opening from the tray. Passes if it looks right beside the C# drawer, idles under 30 MB and the installer is under 5 MB. DWM backdrops need Windows 11 22H2 or later, so decide whether to drop Windows 10.
2. **Core port.** Rust library with the parser, pace and refill logic. Port all 40 tests first.
3. **Shell.** Tray, menu, CLI runner, placement, notifications, cache.
4. **Drawer.** All states, breakdown scrolling, tooltips, keyboard, light and dark, DPI.
5. **Accessibility.** Screen reader support for the bars and buttons, likely through AccessKit (not yet evaluated).
6. **Swap.** Same AppId in the installer so it upgrades in place. Release as v2.0.0 once it passes the same real-app checks as v1.
