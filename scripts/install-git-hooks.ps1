# Activa .githooks para bloquear push si falla validate-pre-push.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "Configurando core.hooksPath = .githooks (solo este repositorio)..."
git config core.hooksPath .githooks

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: no se pudo configurar core.hooksPath" -ForegroundColor Red
    exit 1
}

$hooksPath = git config --get core.hooksPath
Write-Host "OK: core.hooksPath = $hooksPath"
Write-Host ""
Write-Host "A partir de ahora, todo 'git push' ejecutará scripts/validate-pre-push.ps1 (vía .githooks/pre-push)."
Write-Host "Prueba manual: pnpm run validate:pre-push:win"
