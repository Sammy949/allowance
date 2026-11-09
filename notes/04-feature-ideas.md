# Feature ideas (2026-11-09)

Nothing here is committed to. Ranked by value for the effort, for a personal tool.

## Worth doing, no background polling needed

1. **Pace marker on each bar.** A tick shows how much of the window has passed. Bar past the tick means you're burning faster than even pace. Computed from the reset time and the known window length. Usage Monitor for Claude and CodexBar both have a version.
2. **"Your session refilled" notification.** The reset time is already known, so a timer can fire at that moment, fetch, and notify only if usage had been high. SessionWatcher and Usage Monitor both do this.
3. **What's eating your usage.** The CLI's `/usage` already returns a breakdown (per-habit percentages, top skills, top MCP servers). We fetch it and discard it. It's approximate and covers this machine only, as the CLI itself says. None of the tools checked were seen offering this.

## Worth doing, needs light background refresh

4. **Refresh every 15 minutes.** Keeps the tray numbers honest and unlocks the items below. Costs one CLI run of about 5 seconds each time.
5. **Heads-up alerts at chosen levels**, such as 80% and 95%. The most requested item in Claude Code's own issue tracker. Polling can miss a fast climb inside one interval.
6. **Forecast line**, such as "At this pace: out around 2:30 AM". Better than a tick when the session is short, but noisy early in a window.

## Skip for now

- History charts: needs a lot of stored readings before it says anything.
- Other AI providers, cost tracking, shell hooks on events, an MCP server: real features elsewhere, but out of proportion for a personal tool.
