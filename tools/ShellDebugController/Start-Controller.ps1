$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$pointer=Join-Path $root 'artifacts\debug-controller\latest-app.local.txt'
if(-not(Test-Path -LiteralPath $pointer)){throw 'Run Build-Controller.ps1 first, or launch the prepared app EXE directly.'}
$exe=(Get-Content -LiteralPath $pointer -Raw).Trim()
if(-not(Test-Path -LiteralPath $exe)){throw 'Controller EXE is missing. Nothing was started.'}
# This is the interactive controller UI requested by the user, never Among Us.
Start-Process -FilePath $exe -WindowStyle Normal
