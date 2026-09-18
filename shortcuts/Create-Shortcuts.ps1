$ErrorActionPreference = "Stop"

$folder = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $folder "DtxNodeCheck.exe"
$configPath = Join-Path $folder "dtx-node-check.json"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "DtxNodeCheck.exe non trovato in: $folder"
}

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "dtx-node-check.json non trovato in: $folder"
}

$shortcuts = @(
    @{
        Name = "Run DtxNodeCheck Core.lnk"
        Node = "core"
        Report = "DTX-Core-Report.txt"
        Log = "DtxNodeCheck-Core-debug.log"
    },
    @{
        Name = "Run DtxNodeCheck Workstation.lnk"
        Node = "workstation"
        Report = "DTX-Workstation-Report.txt"
        Log = "DtxNodeCheck-Workstation-debug.log"
    },
    @{
        Name = "Run DtxNodeCheck Client.lnk"
        Node = "client"
        Report = "DTX-Client-Report.txt"
        Log = "DtxNodeCheck-Client-debug.log"
    },
    @{
        Name = "Run DtxNodeCheck Inventory.lnk"
        Node = $null
        Report = "DTX-Inventory.json"
        Log = "DtxNodeCheck-Inventory-debug.log"
        Inventory = $true
    }
)

$shell = New-Object -ComObject WScript.Shell

foreach ($item in $shortcuts) {
    $shortcutPath = Join-Path $folder $item.Name
    $reportPath = Join-Path $folder $item.Report
    $logPath = Join-Path $folder $item.Log
    $isInventory = $item.ContainsKey("Inventory") -and $item.Inventory

    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    if ($isInventory) {
        $shortcut.Arguments = "--inventory --report `"$reportPath`" --format json --log `"$logPath`" --open-report"
    } else {
        $shortcut.Arguments = "--config `"$configPath`" --node $($item.Node) --report `"$reportPath`" --log `"$logPath`" --open-report"
    }
    $shortcut.WorkingDirectory = $folder
    $shortcut.Description = if ($isInventory) { "Esegue inventario PC DtxNodeCheck" } else { "Esegue DtxNodeCheck per nodo $($item.Node)" }
    $shortcut.Save()

    # Set "Run as administrator" on the .lnk. Byte 0x15, bit 0x20 is the RunAs flag.
    $bytes = [System.IO.File]::ReadAllBytes($shortcutPath)
    $bytes[0x15] = $bytes[0x15] -bor 0x20
    [System.IO.File]::WriteAllBytes($shortcutPath, $bytes)

    Write-Host "Creato: $shortcutPath"
}
