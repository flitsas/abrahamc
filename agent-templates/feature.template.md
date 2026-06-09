# Plantilla: Feature (Azure DevOps)

Usa esta plantilla exacta al crear Features en ADO.

---

## Title
```
[ADOPCIÓN-IA] – <Módulo> – <Descripción en una frase>
```
Ejemplos:
- `[ADOPCIÓN-IA] – Personas – Registro de personas con adjuntos`
- `[ADOPCIÓN-IA] – Auth – Autenticación JWT para APIs internas`

## Type
`Feature`

## Area Path
`FLIT`

## Iteration Path
`FLIT\<Sprint siguiente al activo>` ← NUNCA el sprint activo

## Custom Fields

| Campo | Valor |
|-------|-------|
| `Custom.Modulo` | `<módulo>` (ej: Personas, Auth, Pagos) |
| `Tags` | `DOR`, `adopcion-ia`, `fase-1-diseño` |
| `AssignedTo` | `<Nombre del humano responsable>` |
| `Story Points` | N/A (Features no tienen SP, las US hija sí) |

## Description (≥ 200 chars)

### Problema / Necesidad
<Qué problema resuelve esta Feature. Qué le falta al sistema hoy.>

### Objetivo
<Qué debe lograr esta Feature al completarse. En 1-3 frases.>

### Contexto técnico
<Stack relevante, módulo al que pertenece, dependencias con otros módulos.>

## Criterios funcionales (≥ 3, numerados)

1. <Criterio funcional 1 — verificable>
2. <Criterio funcional 2>
3. <Criterio funcional 3>
4. (más según sea necesario)

## Criterios no funcionales / restricciones

- Performance: <si aplica>
- Seguridad: <si aplica, ej. datos PII, Habeas Data>
- Regulatorio: <si aplica>
- Compatibilidad: <si aplica>

## Métricas de éxito

- <Métrica cuantificable 1> (ej: "El endpoint responde < 200ms p95")
- <Métrica cuantificable 2>

## DoR — Checklist antes de pasar a Active

- [ ] Módulo FLIT identificado
- [ ] Objetivo claro en una frase
- [ ] Descripción ≥ 200 chars
- [ ] ≥ 3 criterios funcionales numerados
- [ ] Sprint asignado = siguiente al activo
- [ ] Area Path = FLIT
- [ ] Tag `DOR` presente
- [ ] AssignedTo = persona humana
- [ ] Sin placeholders en el contenido
- [ ] Sin datos sensibles

## Historias hijas (completa al descomponer)

Máximo 8 historias | Máximo 40 SP totales

- [ ] US 1 — [BACKEND] — <título>
- [ ] US 2 — [FRONTEND] — <título>
- ...
