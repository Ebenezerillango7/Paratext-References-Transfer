param(
    [string]$ParatextDir = "",
    [switch]$WaitForParatextExit,
    [switch]$NotifyWhenDone
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "Uninstalling Parallel References Transfer Plugin from Paratext 9..." -ForegroundColor Cyan

# Wait for Paratext to exit if requested
if ($WaitForParatextExit) {
    $procs = Get-Process -Name "Paratext*" -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "Waiting for Paratext 9 to close..." -ForegroundColor Gray
        $procs | Wait-Process -Timeout 300 -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

if ([string]::IsNullOrWhiteSpace($ParatextDir)) {
    $candidateDirs = @(
        "D:\Transdata\Paratext 9",
        "C:\Program Files\Paratext 9",
        "C:\Program Files (x86)\Paratext 9",
        "D:\Program Files\Paratext 9"
    )

    foreach ($dir in $candidateDirs) {
        if (Test-Path (Join-Path $dir "Paratext.exe")) {
            $ParatextDir = $dir
            break
        }
    }
}

$pluginTarget = Join-Path $ParatextDir "plugins\ReferenceTransfer"
if (Test-Path $pluginTarget) {
    Remove-Item -Path $pluginTarget -Recurse -Force
    Write-Host "Removed plugin directory: $pluginTarget" -ForegroundColor Green
}

# Invalidate Paratext cache
$cacheExe = Join-Path $ParatextDir "UpdateParatextPluginCache.exe"
if (Test-Path $cacheExe) {
    Start-Process -FilePath $cacheExe -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
}

$cacheStore = Join-Path $env:APPDATA "Paratext9\AddIns.store"
if (Test-Path $cacheStore) {
    try {
        (Get-Item $cacheStore).LastWriteTime = Get-Date
    }
    catch { }
}

Write-Host "`n[OK] Parallel References Transfer plugin has been cleanly removed." -ForegroundColor Green

if ($NotifyWhenDone) {
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
        [System.Windows.Forms.MessageBox]::Show(
            "Parallel References Transfer plugin has been cleanly removed from Paratext 9.`n`nYou can now reopen Paratext.",
            "Plugin Uninstalled",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
    }
    catch { }
}
