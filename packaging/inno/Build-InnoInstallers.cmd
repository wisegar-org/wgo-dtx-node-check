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

dotnet publish src\DtxNodeCheck.CoreApp\DtxNodeCheck.CoreApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 exit /b 1

dotnet publish src\DtxNodeCheck.WorkstationApp\DtxNodeCheck.WorkstationApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 exit /b 1

dotnet publish src\DtxNodeCheck.ClientApp\DtxNodeCheck.ClientApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 exit /b 1

if not exist artifacts\inno mkdir artifacts\inno

pushd packaging\inno >nul

"%ISCC%" DtxNodeCheck-Core.iss
if errorlevel 1 exit /b 1

"%ISCC%" DtxNodeCheck-Workstation.iss
if errorlevel 1 exit /b 1

"%ISCC%" DtxNodeCheck-Client.iss
if errorlevel 1 exit /b 1

popd >nul
popd >nul

echo.
echo Installer generati in artifacts\inno
