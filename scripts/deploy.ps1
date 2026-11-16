Copy-Item "dist\ReferenceTransfer\*" "installer\" -Force
Write-Host "Copied to installer."

$targetDir = "D:\Transdata\Paratext 9\plugins\ReferenceTransfer"
if (Test-Path $targetDir) {
    Get-ChildItem "dist\ReferenceTransfer" | ForEach-Object {
        $item = $_
        $dest = Join-Path $targetDir $item.Name
        try {
            Copy-Item $item.FullName $dest -Force -ErrorAction Stop
            Write-Host ("Updated " + $item.Name)
        } catch {
            $old = $dest + ".old"
            if (Test-Path $old) { Remove-Item $old -Force -ErrorAction SilentlyContinue }
            Rename-Item $dest ($item.Name + ".old") -Force -ErrorAction SilentlyContinue
            Copy-Item $item.FullName $dest -Force -ErrorAction SilentlyContinue
            Write-Host ("Updated (via rename) " + $item.Name)
        }
    }
}
