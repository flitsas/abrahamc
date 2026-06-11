#!/usr/bin/env bash
# Verifica que cada migración EF Core tenga .Designer.cs y aparezca en dotnet ef migrations list.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

MIGRATIONS_DIR="$ROOT/services/core-api/src/Flit.Infrastructure/Migrations"
failed=0

mapfile -t cs_files < <(
  find "$MIGRATIONS_DIR" -maxdepth 1 -name '*.cs' \
    ! -name '*.Designer.cs' \
    ! -name 'FlitDbContextModelSnapshot.cs' \
    ! -name 'MigrationSql.cs' \
    ! -name 'SqlMigrationHelper.cs' \
    -printf '%f\n' | sort
)

for base in "${cs_files[@]}"; do
  id="${base%.cs}"
  designer="$MIGRATIONS_DIR/${id}.Designer.cs"
  if [[ ! -f "$designer" ]]; then
    echo "ERROR: $base no tiene ${id}.Designer.cs — EF Core no la aplicará al arrancar core-api." >&2
    failed=1
  fi
done

if [[ "$failed" -ne 0 ]]; then
  exit 1
fi

echo "==> dotnet ef migrations list (registro EF)"
list_output="$(
  dotnet ef migrations list \
    --project services/core-api/src/Flit.Infrastructure/Flit.Infrastructure.csproj \
    --startup-project services/core-api/src/Flit.Api/Flit.Api.csproj 2>&1
)"

for base in "${cs_files[@]}"; do
  id="${base%.cs}"
  if ! grep -Fq "$id" <<<"$list_output"; then
    echo "ERROR: $id no aparece en dotnet ef migrations list — falta Designer.cs o atributo [Migration]." >&2
    failed=1
  fi
done

if [[ "$failed" -ne 0 ]]; then
  exit 1
fi

echo "==> EF migrations OK (${#cs_files[@]} migraciones registradas)"
