# Git hooks — puerta pre-push mecánica

Bloquea **`git push`** si falla la validación completa (Prettier, lint, typecheck, tests, build FE, backend, EF, gitleaks).

## Activar (una vez por máquina)

```powershell
pnpm run hooks:install
```

Equivalente manual:

```powershell
git config core.hooksPath .githooks
```

En Windows con Git Bash, el hook `.githooks/pre-push` invoca `scripts/validate-pre-push.sh`. En PowerShell directo, usa `pnpm run validate:pre-push:win` antes de push.

## Qué valida

Paridad con CI — ver `.cursor/rules/pre-push-gate.mdc` y `scripts/validate-pre-push.ps1`.

**Importante:** sin `core.hooksPath`, solo la regla del agente aplica; con hooks activos, el push **no puede** omitir Prettier ni lint.

Regla Cursor: `.cursor/rules/pre-push-gate.mdc`.
