# Convenciones FLIT — Fuente única de verdad

Este archivo es la referencia oficial de convenciones del equipo FLIT.
**Todos los agentes deben leerlo antes de cualquier acción sobre work items, código o repositorio.**

---

## Las 18 reglas innegociables

| # | Regla | Alcance |
|---|-------|---------|
| 1 | Sprint: SIEMPRE el **siguiente al activo** (nunca el activo ni el corriente) | Work items |
| 2 | Tag `DOR` obligatorio en Features y US antes de pasar a `Active` | Work items |
| 3 | Story Points: Fibonacci estricto (1, 2, 3, 5, 8). No 4, 6, 7. | US |
| 4 | AssignedTo: SIEMPRE humano identificado. NUNCA agente, NUNCA vacío | Work items |
| 5 | Sin datos sensibles en descripción (no nombres de clientes reales, no cifras financieras embargadas) | Work items |
| 6 | Sin placeholders en work items `Active` (no `TODO`, `TBD`, `XXX`, `[]`, `...`) | Work items |
| 7 | Bugs PDN: al **Líder Técnico** (NUNCA directo al desarrollador) | Bugs |
| 8 | PRs target: SIEMPRE `develop`, nunca `main` | PRs |
| 9 | PRs: máximo 800 líneas de diff | PRs |
| 10 | Cero threads activos sin resolver antes de mergear | PRs |
| 11 | Code Review Agent + Security Agent `succeeded` antes de mergear | PRs |
| 12 | ≥1 reviewer humano aprueba antes de mergear | PRs |
| 13 | Build pipeline `succeeded` antes de mergear | PRs |
| 14 | Sin `git push --force` en branches compartidos | Git |
| 15 | ADRs: en estado `Propuesto` por agentes. `Aceptado` exclusivo del Líder Técnico humano en PR separada | ADRs |
| 16 | Co-authored-by completo en commits de merge (todos los agentes participantes) | Git |
| 17 | `Closed` de Feature: exclusivo del **PO humano** | Work items |
| 18 | Skills externas: auditadas antes de instalar (5 pasos documentados) | Skills |

---

## Campos ADO (Azure DevOps)

### Features

| Campo | Valor requerido |
|-------|----------------|
| `Title` | Prefijo `[ADOPCIÓN-IA]` + descripción clara en una frase |
| `Area Path` | `FLIT` |
| `Iteration Path` | Sprint siguiente al activo |
| `Custom.Modulo` | Módulo FLIT identificado (no vacío) |
| `Tags` | `DOR` + `adopcion-ia` + `fase-1-diseño` |
| `AssignedTo` | Humano identificado |
| `Description` | ≥ 200 chars, sin placeholders |
| `Criteria funcionales` | ≥ 3, numerados |

### User Stories

| Campo | Valor requerido |
|-------|----------------|
| `Title` | `[US #ID] [BACKEND\|FRONTEND] – <módulo> – <descripción>` |
| `Area Path` | `FLIT` |
| `Iteration Path` | Sprint siguiente al activo |
| `Story Points` | Fibonacci (1, 2, 3, 5, 8) |
| `Custom.Refinement` | `true` antes de `Active` |
| `Tags` | `DOR` (antes de Active) |
| `Parent` | Feature padre en `Active` o `Resolved` |
| `Dependencies` | Campo explícito (otras US ID) |

### Bugs

| Campo | Valor requerido |
|-------|----------------|
| `Severity` | `Critical \| High \| Medium \| Low` |
| `AssignedTo` | Líder Técnico (PDN bugs) |
| `Tags` | `bug-pdn` + módulo |
| `State inicial` | `New` |
| `Pasos para reproducir` | Numerados, específicos |
| `Resultado esperado vs obtenido` | Explícito |
| `Evidencia` | Screenshots, logs, network trace |

---

## Convenciones de branches

```
agent/<tipo>/<US-ID>-<slug-kebab-case>
```

Tipos válidos: `backend`, `frontend`, `infra`, `qa`, `docs`, `refactor`

Ejemplos:
- `agent/backend/4521-personas-registro-endpoint`
- `agent/frontend/4522-personas-list-page`
- `agent/infra/4530-backend-ci-pipeline`

---

## Convenciones de commits (Conventional Commits + US)

```
<type>(<scope>): <subject> [#US-ID]
```

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `build`, `ci`

Ejemplos:
- `feat(personas): add POST /api/v1/personas endpoint [#4521]`
- `test(personas): add E2E flow for registro [#4522]`
- `fix(auth): handle expired JWT correctly [#4530]`

---

## Convenciones de PRs

| Campo | Convención |
|-------|-----------|
| Título | `[US #ID] [BACKEND\|FRONTEND\|INFRA] – <módulo> – <descripción>` |
| Target | SIEMPRE `develop` |
| Tamaño | ≤ 800 líneas |
| Reviewers | ≥ 1 humano + Code Review Agent + Security Agent |
| Linked items | US correspondiente (campo ADO) |
| Branch | `agent/<tipo>/<US-ID>-<slug>` |

---

## Convenciones de ADRs

| Campo | Convención |
|-------|-----------|
| Ruta | `docs/decisions/ADR-NNNN-<kebab-case-title>.md` |
| Numeración | 4 dígitos zero-padded (ADR-0001, ADR-0042) |
| Estado inicial | `Propuesto` (nunca `Aceptado` al crear) |
| Fecha | ISO YYYY-MM-DD |
| Alternativas | Siempre 2-3 (regla absoluta) |
| Promotor a Aceptado | Exclusivo Líder Técnico humano en PR separada |

---

## Convenciones de archivos (paths)

```
backend/src/modules/<modulo>/domain/<Entidad>.entity.ts
backend/src/modules/<modulo>/domain/<entidad>.repository.interface.ts
backend/src/modules/<modulo>/application/<accion>-<entidad>.use-case.ts
backend/src/modules/<modulo>/infrastructure/<entidad>.typeorm-entity.ts
backend/src/modules/<modulo>/infrastructure/<entidad>.typeorm-repository.ts
backend/src/modules/<modulo>/interfaces/<entidades>.controller.ts
backend/src/modules/<modulo>/interfaces/<entidades>.routes.ts
backend/src/modules/<modulo>/interfaces/<entidades>.dto.ts

backend/tests/unit/<modulo>/<accion>-<entidad>.use-case.spec.ts
backend/tests/integration/<modulo>/<entidades>.controller.spec.ts

backend/migrations/<timestamp>-<PascalCaseDescription>.ts

frontend/src/features/<feature>/api/<feature>.api.ts
frontend/src/features/<feature>/api/<feature>.schemas.ts
frontend/src/features/<feature>/components/<NombreComponente>.tsx
frontend/src/features/<feature>/hooks/use-<feature>.ts
frontend/src/features/<feature>/pages/<NombrePagina>.tsx

docs/decisions/ADR-NNNN-<slug>.md
docs/designs/<feature-id>-<slug>.md
docs/reports/<YYYY-MM-DD>-<tipo>-<modulo>.md
```

---

## Regla 18 — Auditoría de skills externas

Antes de instalar una skill externa (no incluida en este repo):

1. **Leer** el código fuente completo de la skill
2. **Verificar** que no exfiltra datos del repo (no hace requests a dominios no autorizados)
3. **Documentar** en `docs/skills-audit/<nombre>.md` con hash del commit evaluado
4. **Obtener aprobación** del Líder Técnico
5. **Registrar** en `~/.flit-agents/skill-activity.jsonl` con `event: skill_installed`

---

## Referencia de sprint — cómo determinar "siguiente al activo"

```bash
# Listar iteraciones
az boards iteration list --team "<equipo>" --depth 3

# El sprint activo es el que tiene timeFrame: "current"
# El siguiente es el inmediatamente posterior en la lista
```

Si el CLI no está disponible, pregunta al humano cuál es el sprint siguiente antes de crear work items.
