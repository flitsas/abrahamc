# Valida localmente lo mínimo que Security Scan y Core API CI exigen antes de push.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "==> dotnet test (Flit.Api.Tests)"
dotnet test services/core-api/tests/Flit.Api.Tests/Flit.Api.Tests.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> gitleaks (solo archivos trackeados por git, mismo alcance que CI)"
$ScanDir = Join-Path $env:TEMP ("flit-gitleaks-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $ScanDir | Out-Null
try {
  $files = git ls-files
  foreach ($f in $files) {
    $dest = Join-Path $ScanDir $f
    $parent = Split-Path $dest -Parent
    if ($parent -and -not (Test-Path $parent)) {
      New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Copy-Item $f $dest -Force
  }
  Copy-Item .gitleaks.toml (Join-Path $ScanDir ".gitleaks.toml") -Force

  $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
  if ($gitleaks) {
    & gitleaks detect --source $ScanDir --config (Join-Path $ScanDir ".gitleaks.toml") --no-git --redact --exit-code 1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
  elseif (Get-Command docker -ErrorAction SilentlyContinue) {
    docker run --rm -v "${ScanDir}:/repo" ghcr.io/gitleaks/gitleaks:v8.21.2 `
      detect --source /repo --config /repo/.gitleaks.toml --no-git --redact --exit-code 1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
  else {
    $bin = Join-Path $env:TEMP "gitleaks.exe"
    if (-not (Test-Path $bin)) {
      $zip = Join-Path $env:TEMP "gitleaks.zip"
      Invoke-WebRequest -Uri "https://github.com/gitleaks/gitleaks/releases/download/v8.21.2/gitleaks_8.21.2_windows_x64.zip" -OutFile $zip
      Expand-Archive -Path $zip -DestinationPath $env:TEMP -Force
      Move-Item -Force (Join-Path $env:TEMP "gitleaks.exe") $bin
    }
    & $bin detect --source $ScanDir --config (Join-Path $ScanDir ".gitleaks.toml") --no-git --redact --exit-code 1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
}
finally {
  Remove-Item -Recurse -Force $ScanDir -ErrorAction SilentlyContinue
}

Write-Host "==> Pre-push validation OK"
