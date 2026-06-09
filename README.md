# abrahamc — FLIT 2.0

Repositorio de trabajo de **Abraham Cañon** en la organización [flitsas](https://github.com/flitsas). Parte del ecosistema **FLIT 2.0**: monorepo full-stack con agentes IA integrados al pipeline de desarrollo del equipo.

## Stack

| Capa | Tecnologías |
|------|-------------|
| **Backend** | Node.js 22, TypeScript, Fastify 5, TypeORM, PostgreSQL 16, Zod, Vitest, Pino |
| **Frontend** | React 19, Vite, TypeScript, TailwindCSS, TanStack Query, Playwright |
| **Infra** | Docker, Docker Compose |
| **Calidad** | ESLint, Prettier, Gitleaks |
| **Gestión** | Azure DevOps (work items) · GitHub (código y PRs) |

## Requisitos previos

- [Node.js](https://nodejs.org/) ≥ 22 (ver `.nvmrc`)
- [npm](https://www.npmjs.com/) ≥ 10
- [Docker](https://www.docker.com/) y Docker Compose (para entorno containerizado)
- [PostgreSQL 16](https://www.postgresql.org/) (opcional si usas solo Docker)

## Inicio rápido

### 1. Clonar e instalar dependencias

```bash
git clone https://github.com/flitsas/abrahamc.git
cd abrahamc
npm install
```

### 2. Variables de entorno

**Backend** — copiar y ajustar:

```bash
cp backend/.env.example backend/.env
```

**Frontend** — copiar y ajustar:

```bash
cp frontend/.env.example frontend/.env.local
```

**Identidad Azure DevOps** (opcional, para agentes de integración):

```bash
cp .env.user-identity.example env.user-identity
# Editar con tus datos — este archivo NO se commitea
```

### 3. Desarrollo local (sin Docker)

Levantar PostgreSQL localmente y luego:

```bash
# Terminal 1 — API (puerto 3030 por defecto)
npm run dev -w backend

# Terminal 2 — Frontend (puerto 5173)
npm run dev -w frontend
```

O ambos en paralelo desde la raíz:

```bash
npm run dev
```

| Servicio | URL |
|----------|-----|
| Frontend | http://localhost:5173 |
| Backend API | http://localhost:3030/api/v1 |
| Health check | http://localhost:3030/health |

### 4. Desarrollo con Docker

```bash
docker compose -f infra/docker-compose.yml up --build
```

Levanta PostgreSQL, backend y frontend con la configuración de `infra/docker-compose.yml`.

## Scripts disponibles

Desde la raíz del monorepo:

| Comando | Descripción |
|---------|-------------|
| `npm run dev` | Backend + frontend en paralelo |
| `npm run build` | Build de producción de ambos workspaces |
| `npm run test` | Tests unitarios (backend + frontend) |
| `npm run lint` | ESLint en ambos workspaces |
| `npm run format:check` | Verificar formato Prettier |

Scripts por workspace (`npm run <script> -w backend|frontend`):

| Workspace | Destacados |
|-----------|------------|
| **backend** | `dev`, `build`, `test`, `migration:run`, `migration:revert` |
| **frontend** | `dev`, `build`, `test`, `test:e2e` |

## Estructura del proyecto

```
.
├── backend/           # API REST — Clean Architecture (Fastify + TypeORM)
├── frontend/          # SPA React — arquitectura feature-sliced
├── infra/             # Docker Compose para desarrollo local
├── agent-templates/   # Plantillas FLIT (US, ADR, DoR, DoD, convenciones)
├── .cursor/           # Agentes, skills, rules y workflows para Cursor
├── CLAUDE.md          # Fuente de verdad para agentes IA y convenciones
└── AGENTS.md          # Punto de entrada alternativo → CLAUDE.md
```

## Arquitectura

### Backend (Clean Architecture)

```
backend/src/modules/<modulo>/
  domain/          → Entidades y contratos (sin frameworks)
  application/     → Casos de uso
  infrastructure/  → TypeORM, repositorios
  interfaces/      → Controllers, rutas y DTOs Zod
```

Detalle en [`backend/CLAUDE.md`](backend/CLAUDE.md).

### Frontend (Feature-sliced)

```
frontend/src/features/<feature>/
  api/             → Schemas Zod + hooks TanStack Query
  components/      → UI del feature
  hooks/           → Lógica reutilizable
  pages/           → Componentes de ruta
```

Detalle en [`frontend/CLAUDE.md`](frontend/CLAUDE.md).

## Flujo de trabajo Git

| Rama | Propósito |
|------|-----------|
| `develop` | Integración diaria (**rama predeterminada**) |
| `main` | Producción |
| `agent/<tipo>/<US-ID>-<slug>` | Ramas de trabajo por historia de usuario |

- Las **PRs** siempre apuntan a `develop`.
- Convención de commits: `feat(modulo): descripción [#US-ID]`
- Reglas completas en [`agent-templates/conventions.md`](agent-templates/conventions.md).

## Agentes IA

El proyecto incluye agentes especializados en `.cursor/agents/` para orquestar el ciclo completo: arquitectura, backend, frontend, base de datos, QA, seguridad, infra y code review.

Punto de entrada para cualquier herramienta de IA: [`CLAUDE.md`](CLAUDE.md).

## Producción

El archivo `docker-compose.prod.yml` define el despliegue en VPS (PostgreSQL en el host, backend y frontend en contenedores). Ver comentarios en el archivo para las variables requeridas.

## Seguridad

- No commitear archivos `.env`, `env.user-identity` ni credenciales.
- Gitleaks configurado en `.gitleaks.toml` para detectar secretos en el historial.
- Variables sensibles solo vía archivos de entorno o secretos del CI/CD.

## Licencia

Proyecto privado de la organización **flitsas**. Uso interno del equipo FLIT.
