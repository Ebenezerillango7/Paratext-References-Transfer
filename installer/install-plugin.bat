@echo off
cd /d "%~dp0"
echo Starting Paratext 9 Plugin Installer...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-plugin.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo Installation failed with error code %ERRORLEVEL%.
    pause
)
