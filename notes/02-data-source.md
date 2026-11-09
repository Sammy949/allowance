# Data source

## Command

Run from Windows:

```
wsl.exe --cd ~ -e bash -lc "claude -p /usage --output-format json --no-session-persistence < /dev/null"
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

## M0 results (2026-11-09)

| Check | Result |
|---|---|
| Freshness | The session figure rose between runs and matched claude.ai's usage page on both percentages. One real movement observed |
| Project with a noisy local settings file | Plain text mode printed a settings warning first. JSON mode was clean, stdout and stderr |
| Running from the home folder | Works with `wsl.exe --cd ~`, which keeps project settings out of the run |
| Logged out (simulated with an empty config dir) | Exit 0, `is_error: false`, and no `% used` lines. Prints a cost summary starting `Total cost:` |
| CLI not found | Exit 127, `command not found` on stderr |

Parser consequences:

- `is_error` and the exit code do not detect logged-out. Treat "no `% used` lines" as the failure signal.
- Reset times come in both forms: `4:50am` and `7pm`.
- The CLI's reset minute can differ by one from run to run (`6:59pm`, then `7pm`). claude.ai showed the rounded value.

## Not verified yet

- Cold start when WSL isn't running. `wsl --shutdown` would end the session doing the testing, so this gets tested from the running app in M2.
- Output near a limit, or with per-model weekly lines. The label-agnostic parser should cover the second.

## Risks

- The text is not a documented contract, so any CLI update can change it. The parser must fail visibly and keep the last good numbers marked stale.
- The reset strings have no year.
