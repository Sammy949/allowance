# Takes focus with a throwaway window, like clicking elsewhere, then closes it.
Add-Type -AssemblyName System.Windows.Forms
$f = New-Object System.Windows.Forms.Form; $f.Width = 120; $f.Height = 80; $f.StartPosition = 'CenterScreen'; $f.TopMost = $true
$f.Show(); $f.Activate(); [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 800; [System.Windows.Forms.Application]::DoEvents(); $f.Close(); 'clicked away'
