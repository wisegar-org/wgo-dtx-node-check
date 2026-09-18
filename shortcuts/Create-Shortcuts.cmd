@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Create-Shortcuts.ps1"

if errorlevel 1 (
  echo.
  echo Errore durante la creazione degli shortcut.
  pause
  exit /b 1
)

echo.
echo Shortcut creati nella cartella:
echo %SCRIPT_DIR%
pause
