# Reports whether the Allowance window is visible and whether it has focus. With -Esc, sends Escape only if it has focus.
param([switch]$Esc)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class S { [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); }
"@
$h = [S]::FindWindow([NullString]::Value, 'Allowance')
$focused = ($h -ne [IntPtr]::Zero) -and ([S]::GetForegroundWindow() -eq $h)
if ($Esc) { if ($focused) { [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); Start-Sleep -Milliseconds 400; 'sent Esc' } else { 'not focused: Esc NOT sent'; } }
"visible=$([S]::IsWindowVisible($h)) focused=$([S]::GetForegroundWindow() -eq $h)"
