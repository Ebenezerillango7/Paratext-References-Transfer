Copy-Item "dist\ReferenceTransfer\*" "installer\" -Force
Write-Host "Copied to installer."

$targetDir = "D:\Transdata\Paratext 9\plugins\ReferenceTransfer"
if (Test-Path $targetDir) {
    Get-ChildItem "dist\ReferenceTransfer" | ForEach-Object {
        $dest = Join-Path $targetDir $_.Name
        try {
            Copy-Item $_.FullName $dest -Force -ErrorAction Stop
            Write-Host ("Updated " + $_.Name)
        } catch {
            $old = $dest + ".old"
            if (Test-Path $old) { Remove-Item $old -Force -ErrorAction SilentlyContinue }
            Rename-Item $dest ($_.Name + ".old") -Force
            Copy-Item $_.FullName $dest -Force
            Write-Host ("Updated (via rename) " + $_.Name)
        }
    }
}
