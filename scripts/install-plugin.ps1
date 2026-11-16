param(
    [string]$ParatextDir = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Installing Parallel References Transfer Plugin for Paratext 9" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Discover Paratext 9 path
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

    if ([string]::IsNullOrWhiteSpace($ParatextDir)) {
        Write-Error "Could not automatically locate Paratext 9 installation folder."
        exit 1
    }
}

Write-Host "Found Paratext 9 at: $ParatextDir" -ForegroundColor Green

# 2. Check if Paratext is running
$running = Get-Process -Name "Paratext*" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "`nNotice: Paratext 9 is currently running." -ForegroundColor Yellow
    Write-Host "Please save your translation work and close Paratext 9..." -ForegroundColor Cyan
    Write-Host "Waiting for Paratext 9 to close before continuing..." -ForegroundColor Gray
    $running | Wait-Process -Timeout 300 -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "Paratext 9 closed. Resuming installation..." -ForegroundColor Green
}

# 3. Create target plugin directory
$targetPluginDir = Join-Path $ParatextDir "plugins\ReferenceTransfer"
if (-not (Test-Path $targetPluginDir)) {
    New-Item -ItemType Directory -Force -Path $targetPluginDir | Out-Null
    Write-Host "Created plugin directory: $targetPluginDir" -ForegroundColor Green
}

# 4. Resolve source files
$candidateSources = @(
    (Join-Path $PSScriptRoot "..\src\ReferenceTransfer.Plugin\bin\Release\net48"),
    (Join-Path $PSScriptRoot "..\src\ReferenceTransfer.Plugin\bin\Debug\net48"),
    $PSScriptRoot,
    (Join-Path $PSScriptRoot "dist\ReferenceTransfer")
)

$sourceDir = ""
foreach ($cand in $candidateSources) {
    if (Test-Path (Join-Path $cand "ReferenceTransfer.ptxplg")) {
        $sourceDir = $cand
        break
    }
}

if ([string]::IsNullOrWhiteSpace($sourceDir)) {
    Write-Error "Could not find 'ReferenceTransfer.ptxplg'."
    exit 1
}

Write-Host "Copying plugin binaries from: $sourceDir" -ForegroundColor Cyan
Copy-Item "$sourceDir\ReferenceTransfer.ptxplg" -Destination $targetPluginDir -Force
Copy-Item "$sourceDir\ReferenceTransfer.Core.dll" -Destination $targetPluginDir -Force

# Copy uninstaller scripts into plugin folder
$uninstallScript = Join-Path $PSScriptRoot "uninstall-plugin.ps1"
if (Test-Path $uninstallScript) {
    Copy-Item $uninstallScript -Destination $targetPluginDir -Force
}
$uninstallBat = Join-Path $PSScriptRoot "uninstall-plugin.bat"
if (Test-Path $uninstallBat) {
    Copy-Item $uninstallBat -Destination $targetPluginDir -Force
}

# 5. Unblock files (remove Windows Zone.Identifier security block)
try {
    Get-ChildItem -Path $targetPluginDir -File | Unblock-File -ErrorAction SilentlyContinue
    Write-Host "Unblocked plugin files for Windows security compliance." -ForegroundColor Green
}
catch { }

# 6. Invalidate & Refresh Paratext Plugin Cache
$cacheExe = Join-Path $ParatextDir "UpdateParatextPluginCache.exe"
if (Test-Path $cacheExe) {
    Write-Host "Updating Paratext plugin cache..." -ForegroundColor Cyan
    try {
        Start-Process -FilePath $cacheExe -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
    }
    catch { }
}

try {
    [System.Reflection.Assembly]::LoadWithPartialName('System.AddIn') | Out-Null
    [System.AddIn.Hosting.AddInStore]::RebuildAddIns((Join-Path $ParatextDir 'plugins')) | Out-Null
    Write-Host "Paratext AddIn cache rebuilt successfully." -ForegroundColor Green
}
catch { }

$cacheStore = Join-Path $env:APPDATA "Paratext9\AddIns.store"
if (Test-Path $cacheStore) {
    try {
        (Get-Item $cacheStore).LastWriteTime = Get-Date
    }
    catch { }
}

Write-Host "`n[OK] Installation completed successfully!" -ForegroundColor Green
Write-Host "You can now open Paratext 9 and access the tool under:" -ForegroundColor Yellow
Write-Host "  Tools -> Transfer Parallel Passage References..." -ForegroundColor White

try {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    [System.Windows.Forms.MessageBox]::Show(
        "Parallel References Transfer plugin has been installed successfully!`n`nYou can now launch Paratext 9 and access it under:`nTools -> Transfer Parallel Passage References...",
        "Installation Complete",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
catch { }
