#!/usr/bin/env bash
# Valida localmente lo mínimo que Security Scan y Core API CI exigen antes de push.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> dotnet test (Flit.Api.Tests)"
dotnet test services/core-api/tests/Flit.Api.Tests/Flit.Api.Tests.csproj

echo "==> dotnet build (Release)"
dotnet build services/core-api/Flit.slnx --configuration Release

bash "$(dirname "$0")/validate-ef-migrations.sh"

echo "==> gitleaks (archivos trackeados por git — mismo alcance que CI)"
SCAN_DIR="$(mktemp -d)"
trap 'rm -rf "$SCAN_DIR"' EXIT
git ls-files -z | tar -cf - --null -T - | tar -xf - -C "$SCAN_DIR"
cp .gitleaks.toml "$SCAN_DIR/"

run_gitleaks() {
  gitleaks detect --source "$SCAN_DIR" --config "$SCAN_DIR/.gitleaks.toml" --no-git --redact --exit-code 1
}

if command -v gitleaks >/dev/null 2>&1; then
  run_gitleaks
elif command -v docker >/dev/null 2>&1; then
  docker run --rm -v "$SCAN_DIR:/repo" ghcr.io/gitleaks/gitleaks:v8.21.2 \
    detect --source /repo --config /repo/.gitleaks.toml --no-git --redact --exit-code 1
else
  echo "ERROR: instala gitleaks o Docker para escanear secretos antes del push." >&2
  exit 1
fi

echo "==> Pre-push validation OK"
