#!/bin/bash
# WSL: stop any running Allowance (it locks the exe), then build on Windows. Prints errors and the result only.
ROOT="$(cd -P "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT_WIN="$(wslpath -w "$ROOT")"
taskkill.exe /IM Allowance.exe /F >/dev/null 2>&1; sleep 1
cd /mnt/c && cmd.exe /c "cd /d $ROOT_WIN\\src\\Allowance && dotnet build -c Debug -p:Platform=x64 2>&1" | tr -d '\r' \
  | grep -E " error |Build succeeded|FAILED" | sed -E 's/ \[[A-Z]:.*\]$//' | sort -u | head -30
