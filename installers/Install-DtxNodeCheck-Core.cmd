@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Install-DtxNodeCheck-Core.ps1"
if errorlevel 1 (
  echo.
  echo Installazione non completata.
  pause
  exit /b 1
)
echo.
echo Installazione completata.
pause
