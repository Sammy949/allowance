# Publishes Allowance for this user: copies the build to Programs, adds a Start menu shortcut, and starts it.
param([string]$Source = (Join-Path $PSScriptRoot '..\publish'))
$target = Join-Path $env:LOCALAPPDATA 'Programs\Allowance'
Stop-Process -Name Allowance -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $Source '*') -Destination $target -Recurse -Force
$exe = Join-Path $target 'Allowance.exe'
$link = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Allowance.lnk'))
$link.TargetPath = $exe; $link.WorkingDirectory = $target; $link.IconLocation = "$exe,0"; $link.Save()
Start-Process $exe
"installed"
