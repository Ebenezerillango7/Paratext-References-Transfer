param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$distDir = Join-Path $root "dist\ReferenceTransfer"
$binDir = Join-Path $root "src\ReferenceTransfer.Plugin\bin\$Configuration\net48"

Write-Host "Packaging ReferenceTransfer Native Plugin ($Configuration)..." -ForegroundColor Cyan

if (-not (Test-Path (Join-Path $binDir "ReferenceTransfer.ptxplg"))) {
    Write-Error "Could not find built plugin in '$binDir'. Please run 'dotnet build -c $Configuration' first."
    exit 1
}

if (Test-Path $distDir) {
    Remove-Item -Path $distDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

# 1. Copy plugin binaries
Copy-Item "$binDir\ReferenceTransfer.ptxplg" -Destination $distDir -Force
Copy-Item "$binDir\ReferenceTransfer.Core.dll" -Destination $distDir -Force

# 2. Copy turnkey install & uninstall scripts
Copy-Item "$PSScriptRoot\install-plugin.bat" -Destination $distDir -Force
Copy-Item "$PSScriptRoot\install-plugin.ps1" -Destination $distDir -Force
Copy-Item "$PSScriptRoot\uninstall-plugin.bat" -Destination $distDir -Force
Copy-Item "$PSScriptRoot\uninstall-plugin.ps1" -Destination $distDir -Force

# 3. Create zip distribution
$zipFile = Join-Path $root "dist\ReferenceTransfer-Paratext9-Plugin-v2.0.0.zip"
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($distDir, $zipFile)

Write-Host "`n[OK] Packaged successfully to:" -ForegroundColor Green
Write-Host "  Folder: $distDir" -ForegroundColor White
Write-Host "  ZIP:    $zipFile" -ForegroundColor Yellow
