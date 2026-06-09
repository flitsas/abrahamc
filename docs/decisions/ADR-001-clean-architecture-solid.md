# ADR-001: Clean Architecture y principios SOLID en el backend .NET

**Fecha**: 2026-06-09
**Fecha de aprobación**: 2026-06-09
**Status**: Aceptado
**Deciders**: Abraham Cañon
**Tags**: arquitectura, backend, clean-architecture, solid, dotnet

---

## Contexto

El backend del monorepo (`backend/dotnet/`) es una API modular con múltiples dominios (Auth, Users, RBAC, Companies, Procedures, Integrations, IdentityVerification, etc.). Se requiere una arquitectura que:

- Aísle la lógica de negocio de frameworks (ASP.NET Core, EF Core).
- Permita evolucionar módulos sin efectos colaterales.
- Facilite tests unitarios de casos de uso sin base de datos.
- Sea verificable en code review y por agentes IA.

Sin una decisión explícita, el riesgo es mezclar capas (lógica en endpoints, `DbContext` en Domain, repositorios acoplados a EF en Application).

## Decisión

Adoptar **Clean Architecture estricta** modular en `backend/dotnet/`, con cumplimiento obligatorio de los **principios SOLID** en todo código nuevo y en refactors tocados por una HU.

**Dirección de dependencias:**

```
Domain ← Application ← Infrastructure / Adapters
Flit.Api → Application (solo orquestación HTTP y DI)
Flit.Infrastructure → módulos (persistencia compartida)
```

---

## Alternativas consideradas

### Opción 1: Clean Architecture modular + SOLID (seleccionada)

**Descripción**: Módulos `Flit.Modules.*` con capas Domain / Application / Ports / Adapters; persistencia centralizada en `Flit.Infrastructure`.

**Pros:**
- Alineado con la estructura actual del repo.
- Tests de Application sin EF Core ni HTTP.
- Escalable por dominio sin monolito acoplado.

**Cons:**
- Más carpetas y archivos por feature.
- Curva de aprendizaje para devs nuevos.

**Esfuerzo estimado**: M (ya parcialmente implementado)
**Riesgos principales**: Violaciones de capas en PRs sin revisión estricta.

---

### Opción 2: Vertical Slice Architecture (sin capas horizontales estrictas)

**Descripción**: Organizar por feature/caso de uso (`Features/CreateEmployee/`) mezclando handler, DTO y persistencia en la misma carpeta.

**Pros:**
- Menos navegación entre carpetas.
- Implementación más rápida para features pequeñas.

**Cons:**
- Acoplamiento gradual a EF Core y ASP.NET.
- Difícil reutilizar reglas de dominio entre slices.
- Contradice la inversión de dependencias para tests puros.

**Esfuerzo estimado**: L (refactor completo)
**Riesgos principales**: Deuda técnica y regresiones en módulos existentes.

---

### Opción 3: Arquitectura en capas clásica (Controller → Service → Repository)

**Descripción**: Tres capas horizontales sin Domain explícito ni módulos por bounded context.

**Pros:**
- Patrón familiar para equipos .NET tradicionales.

**Cons:**
- Services anémicos con lógica dispersa.
- Domain rules mezcladas con ORM.
- No escala bien con 10+ módulos de dominio.

**Esfuerzo estimado**: L
**Riesgos principales**: God-services y violación de SRP a mediano plazo.

---

## Tradeoff aceptado

Se elige **Opción 1** porque el repo ya está estructurado en módulos `Flit.Modules.*`, los agentes FLIT (`backend-agent`, `code-review-agent`) y la documentación (`backend/CLAUDE.md`) están alineados a este modelo, y SOLID refuerza las reglas de capas sin ambigüedad en review.

## Consecuencias

### Lo que se gana
- Reglas claras para implementación y revisión de PRs.
- Handlers testeables con mocks de `Ports/`.
- Evolución independiente por módulo de dominio.

### Lo que se pierde / costo aceptado
- Más ceremonia al crear un caso de uso nuevo (handler + port + adapter + endpoint).
- Rechazo de PRs que mezclen capas aunque "funcionen".

### Lo que cambia operacionalmente
- `code-review-agent` bloquea violaciones de capas citando `ADR-001` y `backend/CLAUDE.md`.
- `backend-agent` implementa siempre siguiendo SOLID.
- Nuevos módulos deben seguir la estructura documentada antes de mergear.

---

## ADRs relacionados

- *(Ninguno — ADR fundacional de arquitectura backend)*

---

## Notas operativas para otros agentes

- **Backend Agent**: Implementar handlers en `Application/`, contratos en `Ports/`, EF Core solo en `Infrastructure/` o `Adapters/`. Un handler = un caso de uso (SRP).
- **Architecture Agent**: Diseñar nuevas features respetando capas; no proponer bypass de Domain.
- **Code Review Agent**: Violación de dependencias entre capas → **BLOQUEANTE** (citar ADR-001).
- **Database Agent**: Persistencia y migraciones en `Flit.Infrastructure/`; Domain sin referencias a EF Core.
- **Frontend Agent**: Sin impacto directo; consumir contratos HTTP estables.
- **QA Agent**: Tests de integración contra API; tests unitarios de handlers son responsabilidad del backend-agent.

---

## Referencias externas

- [Clean Architecture — Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID principles](https://en.wikipedia.org/wiki/SOLID)
- `backend/CLAUDE.md` — reglas de implementación
- `agent-templates/code-style-guide.md` — ejemplos de código

---

*Creado por: Architecture Agent / Fecha: 2026-06-09*
*Aprobado por: Abraham Cañon (Líder Técnico FLIT) / Fecha: 2026-06-09 / Estado: Aceptado*
