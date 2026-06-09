# Templates de comentarios en PRs — FLIT

## Template: Code Review Agent — Comentario consolidado

```markdown
## 🔍 Code Review Agent Report — PR !{N}

**US vinculada**: #{ID} | **Agente**: code-review-agent v1.0 | **Líneas revisadas**: {N}

---

### ✓ Lo que está bien
- {descripción concisa de aspecto bien implementado}
- ...

---

### 🚨 Bloqueantes — Seguridad inline ({N})

> Detectado por `flit-inline-security-detector`. Security Agent corre en paralelo con SAST/SCA/gitleaks.

**[CRITICAL] SQL Injection — `{archivo}:{línea}`**
```{lenguaje}
// Código problemático aquí
```
**Riesgo**: {una frase}.
**Recomendación**:
```typescript
// Código correcto sugerido
```
**Referencia**: [OWASP A03:2021 Injection](https://...) | CWE-89

---

### 🚫 Bloqueantes — Calidad/Convenciones ({N})

**[BLOQUEANTE] {Regla}** — `{archivo}:{línea}`
- Contexto: {qué está violado y por qué}
- Acción: {qué debe cambiar}
- Referencia: {CLAUDE.md sección X | ADR-XXXX | convenciones.md regla Y}

---

### 💡 Observaciones (no bloquean)

- `{archivo}:{línea}` — {sugerencia}
- ...

---

### 📊 Métricas

| Métrica | Valor | Umbral |
|---------|-------|--------|
| Líneas de diff | {N} | ≤ 800 |
| Cobertura código nuevo | {N}% | ≥ 80% |
| AC cubiertos por tests | {N}/{Total} | 100% |
| TODOs nuevos | {N} | 0 |

---

### Veredicto final

{pass | fail}

{Si fail}: {N} bloqueantes. Resolver antes de re-solicitar review.
{Si pass}: ✅ Sin bloqueantes. Listo para aprobación humana e Integration Agent.

---
*Tiempo de análisis: {X}s | {timestamp}*
```

---

## Template: Integration Agent — Propuesta de merge

```markdown
## 🔀 Integration Agent — Propuesta de Merge

**PR**: !{N} | **US**: #{ID} | **Agente**: integration-agent v1.0

### Checklist de 9 pre-condiciones

| # | Condición | Estado |
|---|-----------|--------|
| 1 | PR activa | ✓ |
| 2 | Branch convention | ✓ `agent/backend/4521-...` |
| 3 | Target = develop | ✓ |
| 4 | ≥1 reviewer humano | ✓ @{reviewer} |
| 5 | Code Review Agent | ✓ succeeded |
| 6 | Security Agent | ✓ succeeded |
| 7 | Build pipeline | ✓ succeeded |
| 8 | 0 threads activos | ✓ |
| 9 | US con DoR válido | ✓ |

**Estrategia**: squash

### Commit message propuesto

```
[US #{ID}] [BACKEND] – {módulo} – {descripción}

- {cambio 1}
- {cambio 2}

Co-authored-by: Backend Agent <backend-agent@flit.local>
Co-authored-by: Code Review Agent <code-review-agent@flit.local>
Co-authored-by: {Reviewer Humano} <{email}>
```

### Acciones post-merge

1. Actualizar `Commits DEV` en US #{ID}
2. Disparar pipeline `backend-cd-dev`
3. Comentario de auditoría en la US

---

**¿Procedo?** Responde **sí** para ejecutar el merge.
```

---

## Template: Security Agent — Reporte

```markdown
## 🔒 Security Agent Report — PR !{N}

**Agente**: security-agent v1.0 | **Timestamp**: {timestamp}

| Layer | Hallazgos | Critical | High | Medium | Low |
|-------|-----------|----------|------|--------|-----|
| SAST (Semgrep) | {N} | {N} | {N} | {N} | {N} |
| SCA (pnpm audit) | {N} | {N} | {N} | {N} | {N} |
| Secrets (gitleaks) | {N} | — | — | — | — |
| Habeas Data | {N} | — | {N} | {N} | — |

### 🚨 Bloqueantes ({N})

{Lista de hallazgos bloqueantes con CVE/CWE y recomendación}

### ⚠️ Observaciones ({N})

{Lista de hallazgos no bloqueantes}

---

**Status check**: {pass | fail}
*El Code Review Agent ha hecho la detección inline de primera línea en paralelo.*
```
