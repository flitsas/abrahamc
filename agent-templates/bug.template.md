# Plantilla: Bug — FLIT

## Title
```
[BUG] [PDN|QA|DEV] – <Módulo> – <Descripción breve del problema>
```
Ejemplos:
- `[BUG] [PDN] – Personas – Búsqueda retorna vacío para cédulas con puntos`
- `[BUG] [QA] – Auth – Token JWT no expira correctamente`

## Type
`Bug`

## Area Path
`FLIT`

## Severity
`Critical | High | Medium | Low`

| Nivel | Definición |
|-------|-----------|
| Critical | El sistema no funciona, sin workaround, afecta a todos los usuarios |
| High | Funcionalidad principal no funciona, workaround tedioso o inexistente |
| Medium | Funcionalidad parcialmente degradada, hay workaround |
| Low | Comportamiento inesperado pero menor, hay workaround fácil |

## AssignedTo
**REGLA 7 FLIT**: **Líder Técnico** — NUNCA directo al desarrollador.
El Líder Técnico decide asignación y prioridad.

## Tags
`bug-pdn` (si es producción), `bug-qa` (si es QA), `<modulo-afectado>`

## Estado inicial
`New`

---

## Descripción

### Ambiente donde se reproduce
`Producción | QA | DEV | Local` + versión/build

### Pasos para reproducir
1. {Paso exacto}
2. {Siguiente paso}
3. {Resultado obtenido}

### Resultado esperado
{Qué DEBERÍA pasar}

### Resultado obtenido
{Qué ESTÁ pasando}

### Datos de prueba
| Campo | Valor |
|-------|-------|
| {campo} | {valor — datos anonimizados si son PII} |

### Frecuencia
`Siempre | Intermitente (N% de las veces) | Solo con datos específicos`

## Evidencia (obligatorio)

- [ ] Screenshot o grabación de pantalla
- [ ] Logs relevantes (sin datos sensibles — redactar PII)
- [ ] Network trace (si es error HTTP)
- [ ] Stack trace (si hay error 500)

## Impacto en usuarios
{Cuántos usuarios/procesos se ven afectados}
