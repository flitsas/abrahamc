# Bloquea push si falla la puerta local (Windows / Git for Windows).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
& (Join-Path $Root "scripts/validate-pre-push.ps1")
