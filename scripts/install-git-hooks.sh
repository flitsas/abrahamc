#!/usr/bin/env bash
# Activa .githooks para bloquear push si falla validate-pre-push.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "Configurando core.hooksPath = .githooks (solo este repositorio)..."
git config core.hooksPath .githooks

echo "OK: core.hooksPath = $(git config --get core.hooksPath)"
echo ""
echo "A partir de ahora, todo 'git push' ejecutará scripts/validate-pre-push.sh (vía .githooks/pre-push)."
echo "Prueba manual: pnpm run validate:pre-push"
