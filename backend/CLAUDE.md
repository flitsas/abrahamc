# backend/CLAUDE.md — Convenciones específicas del backend FLIT

Lee también el `CLAUDE.md` raíz para convenciones generales.

## Stack

Node.js 22 + TypeScript (strict) + Fastify 4 + TypeORM 0.3 + PostgreSQL 16 + Zod + Vitest + Pino

## Estructura Clean Architecture

```
src/modules/<modulo>/
  domain/             # Puro: entities, value objects, repository INTERFACES
  application/        # Use cases: un archivo por use case (o archivo consolidado por módulo pequeño)
  infrastructure/     # TypeORM entities + repository implementations
  interfaces/         # Fastify controllers, DTOs Zod, routes
src/shared/
  config/             # env.ts (Zod validation), database.ts (DataSource)
  http/               # Error handler, middleware
  errors/             # Tipos de error compartidos
```

## Reglas críticas backend

- `domain/` NUNCA importa de `infrastructure/` ni de `interfaces/`
- `application/` NUNCA importa de `interfaces/` ni de frameworks HTTP
- Toda lógica de negocio en `application/`, NO en controllers
- Controllers: reciben request, llaman use case, envían reply — nada más
- Queries TypeORM: query builder o `.findOneBy()`, NUNCA string concatenation
- Env vars: SIEMPRE via `env` de `shared/config/env.ts` (validado con Zod)
- Logging: Pino via `request.log.info(...)` — nunca `console.log` en producción
- NUNCA loguees `password`, `token`, `jwt`, `secret`, `authorization`, `pin`

## Tests

- Framework: Vitest
- Ubicación unit: `tests/unit/<modulo>/<name>.spec.ts`
- Ubicación integration: `tests/integration/<modulo>/<name>.spec.ts`
- Patrón: AAA (Arrange / Act / Assert)
- Cobertura mínima sobre código nuevo: 80%
- Cada test: idempotente y sin dependencia de otros tests

## Naming

- Archivos: `kebab-case.ts`
- Clases: `PascalCase`
- Interfaces: `IPascalCase` (ej: `IPersonaRepository`)
- Use cases: `<verb>-<entity>.use-case.ts` (ej: `create-persona.use-case.ts`)
- Entities: `<entity>.entity.ts`
- TypeORM entity: `<entity>.typeorm-entity.ts`
- Repository: `<entity>.typeorm-repository.ts`
- Controller: `<entities>.controller.ts`
- Routes: `<entities>.routes.ts`
- DTOs: `<entities>.dto.ts`
- Migration: `<timestamp>-<PascalCaseDesc>.ts` (ej: `1700000000000-CreatePersonasTable.ts`)

## Migraciones

- SIEMPRE idempotentes: `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`
- NUNCA modificar una migración ya aplicada en otro ambiente
- Para cambios post-deploy: nueva migración con timestamp nuevo
- Nombre descriptivo en PascalCase: `AddEmailIndexToPersonas`

## Errores de dominio

Define en `domain/<entity>.entity.ts`:
- `<Entity>NotFoundError`
- `<Entity>DuplicadaError`
- `<Entity>InvalidaError`

El controller los mapea a HTTP status:
- `NotFound` → 404
- `Duplicada` → 409
- `Invalida` → 400

El error handler global maneja el resto → 500
