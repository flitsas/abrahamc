# ADR-NNNN: [Título de la decisión]

**Fecha**: YYYY-MM-DD
**Status**: Propuesto
**Deciders**: [Líder Técnico FLIT, otros stakeholders relevantes]
**Tags**: [arquitectura, backend|frontend|infra|seguridad, modulo-X]

---

## Contexto

¿Qué problema motiva esta decisión? Describe el contexto, constraints, requirements no funcionales, regulaciones. Máximo 200 palabras. No incluyas la solución aquí.

## Decisión

La opción seleccionada en una frase clara y directa.

---

## Alternativas consideradas

*(Regla absoluta: mínimo 2, máximo 3 alternativas antes de decidir)*

### Opción 1: [Nombre descriptivo]

**Descripción**: una frase.

**Pros:**
- ...
- ...
- ...

**Cons:**
- ...
- ...
- ...

**Esfuerzo estimado**: S | M | L
**Riesgos principales**: ...

---

### Opción 2: [Nombre descriptivo]

**Descripción**: una frase.

**Pros:**
- ...

**Cons:**
- ...

**Esfuerzo estimado**: S | M | L
**Riesgos principales**: ...

---

### Opción 3 (opcional): [Nombre descriptivo]

...

---

## Tradeoff aceptado

**POR QUÉ se eligió esta opción sobre las demás.** Concreto y específico — no genérico ("es mejor" no es suficiente).

Ejemplo: "Elegimos TypeORM sobre Prisma porque la codebase ya tiene TypeScript decorators establecidos, el equipo tiene experiencia previa con el patrón ActiveRecord, y el overhead de migración de 3 modelos existentes con Prisma client sería de ~5 días vs ~1 día con TypeORM."

---

## Consecuencias

### Lo que se gana
- ...

### Lo que se pierde / costo aceptado
- ...

### Lo que cambia operacionalmente
- ...

---

## ADRs relacionados

- [ADR-XXXX] — Decisión relacionada / precedente
- [ADR-YYYY] — Supersede (si este ADR reemplaza otro)

---

## Notas operativas para otros agentes

> Completa estas notas para que los agentes de implementación sepan qué cambiar.

- **Backend Agent**: ...
- **Frontend Agent**: ...
- **QA Agent**: ...
- **Security Agent**: ...
- **Infra Agent**: ...

---

## Referencias externas

- [URL o RFC relevante]

---

*Creado por: Architecture Agent / Fecha: YYYY-MM-DD / Estado: Propuesto*
*Para promover a Aceptado: PR separada con aprobación del Líder Técnico humano*
