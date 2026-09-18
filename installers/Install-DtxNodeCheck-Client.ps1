$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

function Install-DtxNodeCheckApp {
    param(
        [Parameter(Mandatory = $true)][string]$Node,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][string]$ConfigFileName
    )

    $sourceDir = $PSScriptRoot
    $installDir = Join-Path $env:ProgramFiles $DisplayName
    $dataDir = Join-Path $env:ProgramData $DisplayName
    $reportDir = Join-Path $dataDir "Reports"
    $logDir = Join-Path $dataDir "Logs"
    $exePath = Join-Path $installDir "DtxNodeCheck.exe"
    $configPath = Join-Path $installDir $ConfigFileName
    $reportPath = Join-Path $reportDir "DTX-$Node-Report.html"
    $logPath = Join-Path $logDir "DtxNodeCheck-$Node-debug.log"

    New-Item -ItemType Directory -Force -Path $installDir, $reportDir, $logDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir "DtxNodeCheck.exe") -Destination $exePath -Force
    if (Test-Path -LiteralPath (Join-Path $sourceDir "DtxNodeCheck.pdb")) {
        Copy-Item -LiteralPath (Join-Path $sourceDir "DtxNodeCheck.pdb") -Destination (Join-Path $installDir "DtxNodeCheck.pdb") -Force
    }
    Copy-Item -LiteralPath (Join-Path $sourceDir $ConfigFileName) -Destination $configPath -Force

    New-DtxShortcut -ShortcutPath (Join-Path ([Environment]::GetFolderPath("Desktop")) "$DisplayName.lnk") -ExePath $exePath -WorkingDirectory $installDir -Description $DisplayName
    New-DtxShortcut -ShortcutPath (Join-Path ([Environment]::GetFolderPath("Programs")) "$DisplayName.lnk") -ExePath $exePath -WorkingDirectory $installDir -Description $DisplayName

    Write-Host "Installato: $installDir"
    Write-Host "Report: $reportPath"
}

function New-DtxShortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ShortcutPath,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $ExePath
    $shortcut.Arguments = ""
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = $Description
    $shortcut.Save()

    $bytes = [System.IO.File]::ReadAllBytes($ShortcutPath)
    $bytes[0x15] = $bytes[0x15] -bor 0x20
    [System.IO.File]::WriteAllBytes($ShortcutPath, $bytes)
}

Install-DtxNodeCheckApp -Node "client" -DisplayName "DTX Node Check Client" -ConfigFileName "dtx-node-check-client.json"
