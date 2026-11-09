# Data source

## Command

Run from Windows:

```
wsl.exe -e bash -lc "claude -p /usage --output-format json --no-session-persistence < /dev/null"
```

The official CLI handles its own login and token refresh. This app never reads a credential.

## Measured 2026-11-09 (CLI 2.1.319, WSL login, Windows 11)

| Check | Result |
|---|---|
| Runs through PowerShell to `wsl.exe` | 5 of 5 succeeded, `is_error: false` |
| Cost reported | `total_cost_usd: 0` on all 5. Zero tokens where the full usage object was read |
| Time, stdin left open | 7.2 to 7.6 s (3 runs) |
| Time, stdin closed and no session persistence | 5.3 to 5.6 s (2 runs) |

The usage text is in the JSON `result` field:

```
Current session: NN% used · resets Mon D, H:MMam (Region/City)
Current week (all models): NN% used · resets Mon D, H:MMpm (Region/City)
```

## Not verified yet

- Whether each run gets fresh numbers or a cached value. Runs agreed, but usage may simply not have moved.
- Cold start when WSL isn't running.
- What the text looks like when logged out, near a limit, or with per-model weekly lines.

## Risks

- The text is not a documented contract, so any CLI update can change it. The parser must fail visibly and keep the last good numbers marked stale.
- The reset strings have no year.
