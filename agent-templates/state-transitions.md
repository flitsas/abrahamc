# State Transitions — Work Items FLIT

## Features

```
New → Active       Requiere: DoR-Feature PASS (10 criterios)
                   Quién: Líder Técnico humano
Active → Resolved  Requiere: Todas las US hijas en Resolved
                   Quién: Líder Técnico humano
Resolved → Closed  Requiere: DoD-Closed-Feature PASS (28 criterios) + PO sign-off
                   Quién: PO humano (EXCLUSIVO)
Any → Removed      Nunca por agentes. Solo Líder Técnico con justificación.
```

## User Stories

```
New → Active       Requiere: DoR-US PASS (10 criterios)
                   Quién: Líder Técnico humano
Active → Resolved  Requiere: DoD-US PASS (12 criterios) + PR mergeada
                   Quién: Agente puede recomendar, humano ejecuta
Resolved → Closed  Hereda del cierre de la Feature padre
                   Quién: Automático al cerrar Feature (PO)
Active → New       Regresión: solo Líder Técnico humano con comentario de razón
```

## Bugs

```
New → Active       Requiere: Asignado + Severity definida + Pasos para reproducir
                   Quién: Líder Técnico humano
Active → Resolved  Requiere: PR mergeada con fix + Tests que lo reproducen y pasan
                   Quién: Líder Técnico confirma
Resolved → Closed  Requiere: Validación en QA por QA Agent o QA humano
                   Quién: PO o Líder Técnico
New → Closed       Solo si es duplicado o no reproducible (documentar razón)
                   Quién: Líder Técnico humano
```

## Regla de oro

> **Ningún agente puede cerrar work items directamente.**
> Los agentes validan (DoR/DoD), proponen la transición, y el humano la ejecuta.

## Referencia de sprints

```bash
# Listar sprints disponibles
az boards iteration list --team "<equipo>" --depth 3 --output table

# Ver sprint activo (timeFrame: "current")
az boards iteration list --team "<equipo>" --depth 3 | grep current

# El sprint "siguiente al activo" es el inmediatamente posterior en el output
```
