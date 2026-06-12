#!/usr/bin/env bash
# Puerta pre-push FLIT — paridad con CI (frontend + backend + EF + gitleaks).
# Un solo script; no omitir pasos manuales. Ver .cursor/rules/pre-push-gate.mdc
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

run_step() {
  echo ""
  echo "==> $1"
  shift
  "$@"
}

run_step "pnpm format:check (Prettier — mismo paso que CI)" pnpm -r format:check
run_step "pnpm lint (ESLint)" pnpm -r lint
run_step "pnpm typecheck (TypeScript)" pnpm -r typecheck
run_step "pnpm test — @flit/frontend (Vitest)" pnpm --filter @flit/frontend test
run_step "pnpm build — @flit/frontend" pnpm --filter @flit/frontend build

run_step "dotnet test (Flit.Api.Tests)" \
  dotnet test services/core-api/tests/Flit.Api.Tests/Flit.Api.Tests.csproj

run_step "dotnet build (Release)" \
  dotnet build services/core-api/Flit.slnx --configuration Release

run_step "EF migrations (integridad + list)" \
  bash "$(dirname "$0")/validate-ef-migrations.sh"

echo ""
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

echo ""
echo "==> Pre-push validation OK (frontend + backend + EF + gitleaks)"
