# Verifica que cada migracion EF Core tenga .Designer.cs y aparezca en dotnet ef migrations list.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$MigrationsDir = Join-Path $Root "services/core-api/src/Flit.Infrastructure/Migrations"
$Exclude = @("FlitDbContextModelSnapshot.cs", "MigrationSql.cs", "SqlMigrationHelper.cs")

$csFiles = Get-ChildItem (Join-Path $MigrationsDir "*.cs") | Where-Object {
  $_.Name -notmatch "\.Designer\.cs$" -and $Exclude -notcontains $_.Name
}

$failed = $false
foreach ($f in $csFiles) {
  $designer = Join-Path $MigrationsDir ($f.BaseName + ".Designer.cs")
  if (-not (Test-Path $designer)) {
    Write-Host "ERROR: $($f.Name) no tiene $($f.BaseName).Designer.cs. EF Core no la aplicara al arrancar core-api."
    $failed = $true
  }
}

if ($failed) { exit 1 }

Write-Host "==> dotnet ef migrations list (registro EF)"
$listOutput = dotnet ef migrations list `
  --project services/core-api/src/Flit.Infrastructure/Flit.Infrastructure.csproj `
  --startup-project services/core-api/src/Flit.Api/Flit.Api.csproj 2>&1 | Out-String

if ($LASTEXITCODE -ne 0) {
  Write-Host $listOutput
  exit $LASTEXITCODE
}

foreach ($f in $csFiles) {
  $id = $f.BaseName
  if ($listOutput -notmatch [regex]::Escape($id)) {
    Write-Host "ERROR: $id no aparece en dotnet ef migrations list. Falta Designer.cs o atributo [Migration]."
    $failed = $true
  }
}

if ($failed) { exit 1 }

Write-Host "==> EF migrations OK ($($csFiles.Count) migraciones registradas)"
