# Mac Master Control Pro - dezinstalare completa (Windows).
#
# Regula permanenta ecosistem GDC: sterge ABSOLUT TOT ce a creat aplicatia
# pe sistem - nu doar folderul din Program Files.
#
# Ruleaza normal (fara admin) pentru curatarea per-user; foloseste
# -RemoveProgramFiles pentru a incerca si stergerea din Program Files.

param(
    [switch]$RemoveProgramFiles
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "Mac Master Control Pro - dezinstalare completa" -ForegroundColor Cyan
Write-Host "================================================"

Write-Host "[1/3] Opresc orice instanta ramasa in fundal..."
Stop-Process -Name "MacMasterControlPro" -Force

Write-Host "[2/3] Sterg datele din AppData..."
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Mac Master Control Pro"
Remove-Item -Recurse -Force "$env:APPDATA\MacMasterControlPro"

if ($RemoveProgramFiles) {
    Write-Host "[3/3] Sterg folderul din Program Files (poate cere elevare)..."
    Remove-Item -Recurse -Force "${env:ProgramFiles}\GDC\Mac Master Control Pro"
    Remove-Item -Recurse -Force "${env:ProgramFiles(x86)}\GDC\Mac Master Control Pro"
} else {
    Write-Host "[3/3] Sarit (ruleaza cu -RemoveProgramFiles pentru a sterge si Program Files)."
}

Write-Host ""
Write-Host "Curatare completa finalizata." -ForegroundColor Green
