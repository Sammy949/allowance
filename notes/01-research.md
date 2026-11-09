# Research (2026-11-09)

## Existing tools

Many tray apps already do this. Windows examples: claudeusagewin (handles WSL), Usage Monitor for Claude (most stars of those checked, can't see a WSL login), Claude Meter (pill above the taskbar), ClaudeMeter, SessionWatcher (paid, readout beside the clock). None were installed or run.

Most read the Claude Code OAuth token and call the undocumented usage endpoint.

## Demand

Real but crowded. Repeated Claude Code feature requests ask for usage visibility, alerts and always-on indicators. CodexBar on Mac has about 10k stars by one listing. Nothing here sizes the market or proves people pay.

## Why not the other data sources

- **Undocumented `/api/oauth/usage` with the local token.** Community reports of persistent 429s through mid 2026, current status unknown. Anthropic's legal page says OAuth is for Claude Code and native Anthropic apps, and developers may not collect, store or intermediate Claude.ai tokens. It doesn't mention read-only meters.
- **Status line `rate_limits`.** Official and token-free, but it only updates while a session is active, so it can't back a refresh button.

## Taskbar limits

As far as known (not verified against Microsoft docs), Windows 11 has no supported way to put custom text in the taskbar itself. A tray icon fits about two digits.
