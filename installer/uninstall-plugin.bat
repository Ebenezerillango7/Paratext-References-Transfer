@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-plugin.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo Uninstallation encountered an issue.
    pause
)
