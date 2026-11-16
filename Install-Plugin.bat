@echo off
cd /d "%~dp0"
if exist "%~dp0installer\install-plugin.bat" (
    call "%~dp0installer\install-plugin.bat"
) else (
    call "%~dp0scripts\install-plugin.bat"
)
