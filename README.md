# Allowance

A small Windows tray tool. Click the icon and a drawer shows how much of your Claude plan is used and when it resets.

- Current session and weekly limits, with reset times
- A tick on each bar showing how much of that window has passed
- A notification when a window you'd nearly used up refills
- What's been using your allowance, from Claude Code's own breakdown
- Refresh on demand
- The logo in your tray, or your two percentages if you prefer (right-click to switch)

Allowance is an independent personal tool. It isn't made by or affiliated with Anthropic.

## Install

**[Download Allowance-Setup.exe](https://github.com/Sammy949/allowance/releases/latest/download/Allowance-Setup.exe)** and run it. No admin rights needed.

It isn't code-signed, so Windows SmartScreen will ask first. Click **More info**, then **Run anyway**.

## Requirements

Windows 10 or 11, WSL, and Claude Code signed in to a Claude plan inside WSL.

Allowance gets its numbers by running `claude -p /usage` in WSL. It never reads your login or token. The output of that command isn't a documented format, so a Claude Code update can break it. If that happens the drawer says so and keeps your last numbers.

## Build

From WSL, with the .NET 10 SDK on Windows:

```
tools/test.sh
tools/build.sh
```

Pushing a tag like `v1.0.1` builds the installer on GitHub and publishes a release.

Working notes are in [`notes/`](notes/).
