# Definition of Done (DoD) — FLIT

## DoD-US (12 criterios) — antes de pasar a `Resolved`

| # | Criterio | Cómo verificar |
|---|----------|----------------|
| 1 | Todos los AC tienen test correspondiente | Verificar PR linkeada — cada AC = mínimo 1 test |
| 2 | PR mergeada a `develop` | Campo `Commits DEV` no vacío |
| 3 | Code Review Agent: succeeded | Status check en la PR |
| 4 | Security Agent: succeeded | Status check en la PR |
| 5 | Build pipeline: succeeded | Status check en la PR |
| 6 | Cobertura sobre código nuevo ≥ 80% | Reporte de cobertura en la PR |
| 7 | Sin TODOs/FIXMEs nuevos | Grep en el diff de la PR |
| 8 | OpenAPI actualizado (si cambió contrato) | `docs/openapi.yaml` actualizado en la PR |
| 9 | Migraciones idempotentes (si las hay) | Revisión de migraciones en la PR |
| 10 | Comentario en la US con resumen | Campo Comentarios de la US tiene resumen de implementación |
| 11 | Co-authored-by completo | Commit message tiene Co-authored-by de agentes participantes |
| 12 | 0 threads activos sin resolver | Conteo de threads en la PR |

---

## DoD-Closed-Feature (28 criterios) — antes de `Closed`

### Grupo A — Completitud del scope (8 criterios)

| # | Criterio |
|---|----------|
| A1 | Todas las US hijas en estado `Resolved` |
| A2 | Todos los TCs generados (QA Agent mode A) |
| A3 | Todos los TCs ejecutados con resultado |
| A4 | 0 TCs bloqueantes sin resolver |
| A5 | Métricas de éxito de la Feature verificadas |
| A6 | Criterios funcionales de la Feature validados contra la implementación |
| A7 | Demo o evidencia de funcionamiento disponible |
| A8 | PO ha validado el scope (sign-off) |

### Grupo B — Calidad técnica (8 criterios)

| # | Criterio |
|---|----------|
| B1 | Cobertura promedio del módulo ≥ 80% |
| B2 | 0 hallazgos Critical abiertos en Security Agent |
| B3 | 0 hallazgos High de Habeas Data abiertos |
| B4 | Deuda técnica introducida documentada y planificada |
| B5 | ADRs generados en `Aceptado` o con PR de aprobación abierta |
| B6 | Catálogo de patrones actualizado si se introdujo patrón nuevo |
| B7 | Migraciones aplicadas sin errores en todos los ambientes activos |
| B8 | 0 regressions detectadas en suite de tests |

### Grupo C — Documentación (6 criterios)

| # | Criterio |
|---|----------|
| C1 | README del módulo actualizado |
| C2 | OpenAPI actualizado y publicado |
| C3 | Runbook actualizado si la Feature afecta operaciones |
| C4 | `docs/designs/` actualizado con diseño final |
| C5 | Decisiones de implementación documentadas en US correspondientes |
| C6 | Changelog actualizado |

### Grupo D — Deploy y verificación (6 criterios)

| # | Criterio |
|---|----------|
| D1 | Feature desplegada a QA sin errores |
| D2 | Tests de aceptación en QA pasando |
| D3 | Performance: métricas dentro de SLOs (latency, error rate) |
| D4 | Monitoreo post-deploy QA: 30 min sin alertas |
| D5 | Infra Agent reportó deploy exitoso |
| D6 | PO aprobó en QA antes de `Closed` |

---

**Política**: el Closed de Feature es exclusivo del PO humano. Los agentes pueden verificar los 28 criterios y emitir `OK_TO_TRANSITION`, pero la transición la hace el PO.
