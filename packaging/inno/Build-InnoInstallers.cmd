@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "ROOT_DIR=%SCRIPT_DIR%..\.."
set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"

if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
  echo Inno Setup non trovato.
  echo Installa Inno Setup e rilancia questo script.
  echo Esempio: winget install --id JRSoftware.InnoSetup.7 -e
  exit /b 1
)

pushd "%ROOT_DIR%" >nul

set "DOTNET=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"

"%DOTNET%" publish src\Wisegar.DTXInspector.App\Wisegar.DTXInspector.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 goto failed

if not exist installers mkdir installers

"%ISCC%" packaging\inno\Wisegar.DTXInspector.Setup.iss
if errorlevel 1 goto failed

popd >nul

echo.
echo Installer versionato generato nella cartella installers; percorso completo indicato da Inno Setup.
exit /b 0

:failed
popd >nul
exit /b 1
