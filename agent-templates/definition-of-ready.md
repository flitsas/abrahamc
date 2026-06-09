# Definition of Ready (DoR) — FLIT

## DoR-Feature (10 criterios) — antes de pasar a `Active`

| # | Criterio | Cómo verificar |
|---|----------|----------------|
| 1 | Módulo FLIT identificado | Campo `Custom.Modulo` no vacío |
| 2 | Objetivo claro en una frase | Descripción con ≥ 50 chars sin placeholder |
| 3 | Descripción extendida | ≥ 200 chars |
| 4 | Criterios funcionales numerados | ≥ 3 criterios explícitos |
| 5 | Sprint = siguiente al activo | `Iteration Path` ≠ sprint activo |
| 6 | Area Path = FLIT | Campo `Area Path` = `FLIT` |
| 7 | Tag `DOR` presente | Campo `Tags` contiene `DOR` |
| 8 | AssignedTo = humano | Campo `AssignedTo` tiene persona humana (no agente, no vacío) |
| 9 | Sin placeholders | No `TODO`, `TBD`, `XXX`, `[]`, `...` en ningún campo |
| 10 | Sin datos sensibles | No nombres de clientes, no cifras embargadas |

**Política**: TODOS los 10 criterios deben ser PASS para aprobar DoR. Si uno falla, la historia NO puede pasar a Active.

---

## DoR-US (10 criterios) — antes de pasar a `Active`

| # | Criterio | Cómo verificar |
|---|----------|----------------|
| 1 | Parent (Feature) presente y activo/resolved | Campo `Parent` apunta a Feature en `Active` o `Resolved` |
| 2 | Título en formato FLIT | `[US #ID] [BACKEND\|FRONTEND] – módulo – desc` |
| 3 | AC: ≥1 positivo + ≥1 negativo | Sección AC tiene al menos 2 escenarios: uno positivo, uno de error |
| 4 | Story Points asignados | Campo SP tiene valor Fibonacci (1, 2, 3, 5, 8) |
| 5 | Refinement = true | Campo `Custom.Refinement` = `true` |
| 6 | Dependencies explícitas | Campo o sección de dependencias no vacío (o explícitamente "Ninguna") |
| 7 | Sprint = siguiente al activo | `Iteration Path` ≠ sprint activo |
| 8 | AssignedTo = humano | Campo `AssignedTo` tiene persona humana |
| 9 | Tag `DOR` presente | Campo `Tags` contiene `DOR` |
| 10 | Sin placeholders en AC | AC no tiene `TODO`, `TBD`, `XXX`, `[]`, `...` |

---

## Notas del validador

- **NA**: si un criterio genuinamente no aplica (documentar por qué), cuenta como PASS
- **FAIL**: cualquier criterio FAIL bloquea la transición a Active
- El Tech Lead Agent (mode C) con skill `flit-dor-dod-validator` ejecuta esta validación automáticamente
