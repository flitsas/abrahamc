# CLAUDE.md — Equipo FLIT

> **Todas las herramientas de IA deben leer este archivo al inicio de cada sesión.**
> Agentes disponibles en `.cursor/agents/` (Cursor) y `agent-templates/` (plantillas compartidas).

## Descripción del repositorio

**Repositorio**: `flitsas/abrahamc` — espacio de trabajo de Abraham Cañon en el equipo FLIT.

Boilerplate del equipo FLIT para desarrollo full-stack con agentes IA integrados al pipeline de desarrollo.

**Stack**:
- Backend: Node.js 22 + TypeScript strict + Fastify 5 + TypeORM + PostgreSQL 16 + Zod + Vitest + Pino
- Frontend: React 19 + Vite + TypeScript + TailwindCSS + TanStack Query + Playwright
- CI/CD: GitHub Actions
- Work items: Azure DevOps Boards (ADO)
- Código: GitHub

**Rama predeterminada**: `develop` (las PRs apuntan aquí; `main` es producción).

## Estructura del repositorio

```
.
├── .cursor/
│   ├── agents/             # Agentes especializados FLIT (Cursor)
│   ├── rules/              # Reglas persistentes (.mdc)
│   ├── skills/             # Skills reutilizables por agentes
│   └── workflows/          # Flujos guiados (implement, review, deploy…)
├── agent-templates/        # Plantillas FLIT (conventions, DoR, DoD, etc.)
├── backend/                # API Node.js (Clean Architecture)
├── frontend/               # App React (Feature-sliced)
├── infra/                  # Docker Compose local + init PostgreSQL
├── CLAUDE.md               # ← Este archivo (fuente de verdad)
├── AGENTS.md               # Redirige a CLAUDE.md
├── docker-compose.prod.yml # Compose de producción (VPS)
└── package.json            # Monorepo npm workspaces (backend + frontend)
```

> Carpetas previstas a medida que el proyecto crezca: `docs/` (ADRs, diseños, runbooks) y `scripts/` (utilidades).

## Agentes disponibles

Lee el agente completo en `.cursor/agents/<name>.md` para instrucciones detalladas.

| Agente | Archivo | Cuándo invocar |
|--------|---------|----------------|
| **Orchestrator** | `.cursor/agents/orchestrator-agent.md` | Flujos completos sin invocar agentes uno por uno |
| **Tech Lead** | `.cursor/agents/tech-lead-agent.md` | Features, descomposición US, DoR/DoD, calidad de código |
| **Architecture** | `.cursor/agents/architecture-agent.md` | Decisiones técnicas, ADRs, diseño de módulos |
| **Backend** | `.cursor/agents/backend-agent.md` | Implementar US [BACKEND] |
| **Frontend** | `.cursor/agents/frontend-agent.md` | Implementar US [FRONTEND] |
| **Database** | `.cursor/agents/database-agent.md` | Migraciones, schemas, RLS, índices, repositorios |
| **Code Review** | `.cursor/agents/code-review-agent.md` | Review de PRs (auto en pipeline) |
| **Integration** | `.cursor/agents/integration-agent.md` | Merge de PRs con copiloto |
| **QA** | `.cursor/agents/qa-agent.md` | Test cases, tests automáticos, bugs PDN |
| **Security** | `.cursor/agents/security-agent.md` | SAST, SCA, secrets, Habeas Data |
| **Infra** | `.cursor/agents/infra-agent.md` | Docker, pipelines, deploys, rollback |

## Comandos rápidos (slash commands)

| Comando | Descripción |
|---------|-------------|
| `/flit:full-flow <feature>` | Pipeline completo Feature → DEV |
| `/flit:implement <US>` | Implementar una US end-to-end |
| `/flit:review <PR>` | Code Review + Security de una PR |
| `/flit:deploy <env> <build>` | Deploy a DEV/QA/PDN |
| `/flit:decompose <feature>` | Descomponer Feature en US |
| `/flit:health` | Health check diario del repo |
| `/flit:validate dor/dod <item>` | Validar DoR o DoD |


## Las 18 reglas innegociables FLIT

Ver `agent-templates/conventions.md` para la lista completa. Las más críticas:

1. Sprint: SIEMPRE el siguiente al activo, NUNCA el activo
2. Tag `DOR` obligatorio antes de Active
3. Story Points: Fibonacci (1, 2, 3, 5, 8)
4. Asignación: humano (NUNCA al agente)
5. Sin datos sensibles en descriptions
6. Sin placeholders en Active (no TODO, TBD, XXX)
7. Bugs PDN: al Líder Técnico (NUNCA directo al dev)
8. PRs: target = `develop`, máx 800 líneas
9. Cero threads activos antes de mergear
10. Code Review + Security succeeded antes de mergear
11. ≥1 reviewer humano antes de mergear
12. Sin force push en branches compartidos
13. ADRs en `Propuesto`; `Aceptado` es exclusivo del Líder Técnico humano
14. Co-authored-by completo en commits de merge
15. Closed de Feature: exclusivo del PO humano
16. Skills externas: auditadas antes de instalar

## Multi-source story intake

Todos los agentes aceptan historias/features de múltiples fuentes. Si no especificas, el agente preguntará:

1. **ID de Azure DevOps** — consulta directa vía CLI
2. **Archivo local** — ruta relativa al repo
3. **URL pública** — Confluence, Notion, GitHub Issue, etc.
4. **Texto directo** — pega el contenido en el chat
5. **Sin historia** — el agente explica sus capacidades

## Arquitectura del backend (Clean Architecture)

```
src/modules/<modulo>/
  domain/
    <Entidad>.entity.ts           # Clase pura, sin decoradores ORM
    <entidad>.repository.interface.ts  # Interface del puerto
  application/
    <accion>-<entidad>.use-case.ts     # Un caso de uso por archivo
  infrastructure/
    <entidad>.typeorm-entity.ts        # Decoradores TypeORM
    <entidad>.typeorm-repository.ts    # Implementación del repositorio
  interfaces/
    <entidades>.controller.ts          # Fastify route handler
    <entidades>.routes.ts              # Registro de rutas
    <entidades>.dto.ts                 # Schemas Zod (request + response)
```

**Regla de dependencias**: domain ← application ← infrastructure → interfaces. Las capas internas nunca importan las externas.

Convenciones detalladas: `backend/CLAUDE.md`.

## Arquitectura del frontend (Feature-sliced)

```
src/features/<feature>/
  api/
    <feature>.api.ts          # fetch + TanStack Query
    <feature>.schemas.ts      # Zod schemas (valida respuestas backend)
  components/                 # UI del feature
  hooks/                      # Lógica reusable (useFeature, useMutation...)
  pages/                      # Route-level components

src/shared/
  components/ui/              # Primitivos UI reutilizables
  api/client.ts               # Axios/fetch base con interceptors
  lib/                        # Utilidades
  types/                      # Tipos globales
```

Convenciones detalladas: `frontend/CLAUDE.md`.

## Naming conventions

| Tipo | Convención | Ejemplo |
|------|-----------|---------|
| Archivos | `kebab-case.ts` | `create-persona.use-case.ts` |
| Clases | `PascalCase` | `PersonaRepository` |
| Interfaces | `IPascalCase` | `IPersonaRepository` |
| Variables/funciones | `camelCase` | `findById` |
| Constantes | `UPPER_SNAKE_CASE` | `MAX_RETRIES` |
| Tests | `<nombre>.spec.ts` | `create-persona.use-case.spec.ts` |
| Componentes React | `PascalCase.tsx` | `PersonaList.tsx` |

## Branches y commits

- Branches: `agent/<tipo>/<US-ID>-<slug>` (ej: `agent/backend/4521-personas-registro`)
- Commits: `feat(personas): add POST endpoint [#4521]`
- PRs target: SIEMPRE `develop`, nunca `main`

## Variables de entorno

- Backend: `backend/.env` (ver `backend/.env.example`)
- Frontend: `frontend/.env.local` (ver `frontend/.env.example`) — solo variables `VITE_*`
- Identidad ADO (local, no commitear): copiar `.env.user-identity.example` → `env.user-identity`
- NUNCA hardcodear en código, Dockerfile ni docker-compose

## Para contribuir

1. Lee este archivo (`CLAUDE.md`)
2. Lee `agent-templates/conventions.md` (18 reglas innegociables)
3. Usa el agente o comando relevante para tu tarea
4. Sigue los DoR/DoD antes de cambiar estados en ADO
