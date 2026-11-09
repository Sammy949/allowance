# Opens the tray icon's right-click menu and picks an item by position, posting keys only to that menu window.
# Items: 1 Open, 2 Refresh, 3 Show numbers in tray, 4 Start with Windows, 5 Quit.
param([int]$Position)
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M { [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string cls, string title);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); }
"@
$icon = [M]::FindWindowEx([IntPtr](-3), [IntPtr]::Zero, 'AllowanceNotifyIcon', [NullString]::Value)
if ($icon -eq [IntPtr]::Zero) { 'notify window not found'; exit 1 }
$iconPid = 0; [void][M]::GetWindowThreadProcessId($icon, [ref]$iconPid)
[void][M]::PostMessage($icon, 0x8001, [IntPtr]((800 -shl 16) -bor 800), [IntPtr]((1 -shl 16) -bor 0x7B))
$menu = [IntPtr]::Zero
for ($i = 0; $i -lt 20 -and $menu -eq [IntPtr]::Zero; $i++) {
  Start-Sleep -Milliseconds 150
  $h = [IntPtr]::Zero
  while (($h = [M]::FindWindowEx([IntPtr]::Zero, $h, '#32768', [NullString]::Value)) -ne [IntPtr]::Zero) {
    $p = 0; [void][M]::GetWindowThreadProcessId($h, [ref]$p); if ($p -eq $iconPid) { $menu = $h; break }
  }
}
if ($menu -eq [IntPtr]::Zero) { 'our menu did not appear'; exit 1 }
for ($i = 0; $i -lt $Position; $i++) { [void][M]::PostMessage($menu, 0x100, [IntPtr]0x28, [IntPtr]::Zero); Start-Sleep -Milliseconds 80 }
[void][M]::PostMessage($menu, 0x100, [IntPtr]0x0D, [IntPtr]::Zero); "picked item $Position"
