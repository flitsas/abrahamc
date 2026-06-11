# Git hooks — puerta pre-push mecánica

Activa el hook **una vez** en tu máquina (el agente no modifica `git config` por política FLIT):

```powershell
git config core.hooksPath .githooks
```

En Windows con Git for Windows, renombra o enlaza `pre-push.ps1` → `pre-push` si el shell no ejecuta `.ps1` directamente; alternativa:

```powershell
Copy-Item .githooks/pre-push.ps1 .githooks/pre-push -Force
```

Tras activarlo, **todo** `git push` ejecuta `pnpm run validate:pre-push` automáticamente.

Regla Cursor equivalente: `.cursor/rules/pre-push-gate.mdc`.
