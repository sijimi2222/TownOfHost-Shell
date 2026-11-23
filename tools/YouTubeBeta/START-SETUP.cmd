@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Setup-YouTubeTester.ps1"
echo.
echo Setup finished. No game or Google login was started.
pause
endlocal
