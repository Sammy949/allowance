#!/bin/bash
# WSL: run the parser tests with Windows dotnet.
ROOT="$(cd -P "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT_WIN="$(wslpath -w "$ROOT")"
cd /mnt/c && cmd.exe /c "cd /d $ROOT_WIN\\tests\\Allowance.Tests && dotnet test 2>&1" | tr -d '\r' | tail -25
