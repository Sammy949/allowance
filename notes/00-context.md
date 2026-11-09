# Context

## What this is

Allowance is a Windows tray icon. Click it and a drawer pops up with a clean recreation of how Claude shows plan usage: current session, weekly limits, percentages, reset times. A refresh button fetches fresh numbers from the user's own account.

## Scope

- Personal tool first. Any expansion gets decided later.
- Foundation and feel come from Spare Hand's tray drawer.

## Decisions

| Date | Decision | Why |
|---|---|---|
| 2026-11-09 | Name: Allowance | A set amount that tops back up on a schedule |
| 2026-11-09 | Build a personal tool instead of adopting an existing tray app | Wanted the Spare Hand drawer feel |
| 2026-11-09 | Proposed: get data by running the official CLI's `/usage` | Refresh must fetch on demand, which a status line can't do, and the app never has to read a token. See `02-data-source.md`. Awaiting plan approval |
| 2026-11-09 | Proposed: same stack as Spare Hand (.NET 10, WinUI 3, WinUIEx) | The drawer code already exists and is proven. Awaiting plan approval |
| 2026-11-09 | Project lives on the Windows filesystem | Windows `dotnet` can't build from a WSL path |
| 2026-11-09 | Notes are tracked in git | Keep them free of machine details and personal usage numbers |
