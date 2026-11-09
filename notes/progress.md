# Progress

## 2026-11-09: Setup

- Named the project Allowance.
- Project folder and git repo created.
- Research and notes written.
- Confirmed the official CLI's `/usage` works from Windows through WSL at no reported cost.
- Technical plan drafted in `03-technical-plan.md`.

## 2026-11-09: M0

- Plan approved. Login stays in WSL.
- Numbers matched claude.ai after a real change in usage.
- Logged-out output, noisy project settings and missing CLI all captured. Results in `02-data-source.md`.
- Drawer layout set from a claude.ai screenshot. Tray icon shows session and weekly.
- Cold start deferred to M2.

## 2026-11-09: M1 to M3

- Parser, fetch, tray icon, drawer and two-number icon built on Spare Hand's card and tray code.
- 26 parser tests pass, using output shaped like the real CLI.
- Driven in the real app: live CLI data, refresh, tray callback open and toggle, Esc, focus loss closes, second launch opens the running app.
- States rendered and checked: first load, updating, signed out, unrecognized output with last numbers, 100%, an extra weekly row, light and dark.
- Bugs caught by rendering: the window didn't grow when content arrived, the WinUI bar track was a hairline, `100% used` was tight, a focus ring showed on every mouse open.
- Design decision: follows the claude.ai reference rather than inventing a new signature, as asked.

### Not verified

- A physical mouse click on the tray icon. Tests post the same callback Windows sends.
- The icon as it appears in the real tray, and the right-click menu, including Start with Windows.
- A real Explorer restart. Only the message handler was exercised.
- Cold start with WSL shut down. The fetch timeout is 45 s.
- 100% and 150% display scaling. Only the current 125% was run. Icon sizes 16, 24 and 32 were rendered to files.

## 2026-11-09: Corner and logos

- Drawer now anchors to the bottom-right of the work area on the cursor's monitor, 12 logical px from the edges.
- Four logo concepts in `design/logo/`: level, coin, pocket, seven. A coin-in-slot concept was dropped because it read as a hat at small sizes.
- Feature ideas in `04-feature-ideas.md`.

Next: pick a logo, then pick features.
