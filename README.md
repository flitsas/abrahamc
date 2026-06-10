# abrahamc — FLIT 2.0

Repositorio de trabajo de **Abraham Cañon** en la organización [flitsas](https://github.com/flitsas). Monorepo full-stack con agentes IA integrados al pipeline de desarrollo del equipo FLIT.

**Repositorio GitHub:** `flitsas/abrahamc` · **Rama predeterminada:** `develop`

---

## Stack tecnológico

| Capa | Tecnologías |
|------|-------------|
| **Backend** | .NET 10, C# 14, ASP.NET Core (Minimal APIs), EF Core, PostgreSQL 16, Serilog, OpenTelemetry |
| **Frontend** | React 19, Vite 5, TypeScript, TailwindCSS, TanStack Query 5, Zod, Axios, Playwright |
| **Package manager** | pnpm 10 (workspace monorepo) |
| **Infra local** | Docker, Docker Compose |
| **Servicio auxiliar** | Python 3.13 + FastAPI (`services/python-ml`) |
| **Calidad** | ESLint, Prettier, Vitest, xUnit, Gitleaks |
| **Gestión** | Azure DevOps (work items) · GitHub (código y PRs) |

---

## Requisitos previos

| Herramienta | Versión mínima | Notas |
|-------------|----------------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.300 | Ver `backend/dotnet/global.json` |
| [Node.js](https://nodejs.org/) | ≥ 24 | Solo para frontend; ver `.nvmrc` |
| [pnpm](https://pnpm.io/) | ≥ 10 | `corepack enable` o instalación global |
| [Docker](https://www.docker.com/) | — | Opcional, recomendado para PostgreSQL |
| [PostgreSQL](https://www.postgresql.org/) | 16 | Local o vía Docker |
| [Git](https://git-scm.com/) | — | Para clonar y versionar |

---

## Contenido del proyecto

### Backend — API .NET (`backend/dotnet/`)

Solución **Flit.slnx** con arquitectura **Clean Architecture** modular:

| Proyecto | Responsabilidad |
|----------|-----------------|
| `Flit.Api` | Host HTTP, endpoints Minimal API, middleware, DI |
| `Flit.Infrastructure` | EF Core, DbContext, repositorios, migraciones |
| `Flit.Gateway` | API Gateway (YARP) — opcional en DEV |
| `Flit.SharedKernel` | Tipos y abstracciones compartidas |
| `Flit.SharedKernel.Pdf` | Utilidades PDF |
| `Flit.Modules.Auth` | Autenticación, MFA, sesiones |
| `Flit.Modules.Identity` | Identidad, credenciales, perfiles |
| `Flit.Modules.Users` | Gestión de usuarios |
| `Flit.Modules.Rbac` | Roles, permisos, menús |
| `Flit.Modules.Companies` | Empresas, OT, integración RUNT |
| `Flit.Modules.Procedures` | Instancias de trámites |
| `Flit.Modules.ProceduresConfig` | Parametrización de trámites, reglas, documentos |
| `Flit.Modules.IdentityVerification` | Verificación de identidad (IdSecure) |
| `Flit.Modules.Integrations` | Consultas externas (Verifik, circuit breaker) |
| `Flit.Modules.Notifications` | Notificaciones y entregas |

**Grupos de endpoints en `Flit.Api`:** Auth, Users, RBAC, Companies, Procedures, ProceduresConfig, Integrations, IdentityVerification, IdSecure (público, interno, backoffice, operador, analytics, Habeas Data), OT, Trámites (admin, auth, onboarding, profile, support, RBAC), DevSeed.

### Frontend — SPA React (`frontend/`)

Arquitectura **feature-sliced** conectada a la API Trámites 2.0:

| Ruta | Feature | Descripción |
|------|---------|-------------|
| `/login` | `features/auth` | Login con cookies de sesión (Zod + TanStack Query) |
| `/` | `features/home` | Dashboard: sesión activa + health de la API |
| `/tramites` | `features/procedures` | Listado de tipos de trámite del tenant |
| — | `shared/components/ui` | `DashboardLayout`, `LoadingSkeleton`, `ErrorState`, `EmptyState` |
| — | `shared/api/client.ts` | Cliente Axios (`withCredentials`) |

**Tests:** Vitest (`src/**/*.test.ts`) + Playwright (`e2e/smoke.spec.ts`).

### Infraestructura (`infra/`)

- `docker-compose.yml` — PostgreSQL 16, core-api (.NET), frontend (nginx)
- `postgres-init.sql` — Script de inicialización de la base de datos

### Servicios auxiliares (`services/`)

- `python-ml/` — Microservicio Python 3.13 + FastAPI (OCR / ML, health stub en puerto 4012)

### Agentes IA y plantillas

| Carpeta | Contenido |
|---------|-----------|
| `.cursor/agents/` | 11 agentes especializados (backend, frontend, database, QA, security, infra, etc.) |
| `.cursor/skills/` | Skills reutilizables (dev-tester, playwright-runner, db-schema-validator, etc.) |
| `.cursor/workflows/` | Flujos guiados (implement-story, review-pr, deploy-env, etc.) |
| `.cursor/rules/` | Reglas persistentes para Cursor |
| `agent-templates/` | Plantillas FLIT (DoR, DoD, convenciones, ADR, bugs, test cases) |
| `CLAUDE.md` | Fuente de verdad para agentes IA |

---

## Estructura del repositorio

```
.
├── backend/
│   ├── dotnet/                    # Solución .NET 10
│   │   ├── src/
│   │   │   ├── Flit.Api/          # API principal
│   │   │   ├── Flit.Infrastructure/
│   │   │   ├── Flit.Gateway/
│   │   │   ├── Flit.SharedKernel/
│   │   │   └── Flit.Modules.*/    # Módulos de dominio
│   │   ├── tests/Flit.Api.Tests/  # xUnit (handlers + dominio)
│   │   ├── Flit.slnx
│   │   ├── global.json
│   │   └── Dockerfile
│   └── CLAUDE.md                  # Convenciones backend
├── frontend/
│   ├── src/
│   │   ├── features/              # Módulos por feature
│   │   └── shared/                # Componentes y API compartidos
│   ├── Dockerfile
│   └── CLAUDE.md                  # Convenciones frontend
├── infra/
│   ├── docker-compose.yml         # Stack local (postgres + api + frontend)
│   └── postgres-init.sql
├── services/
│   └── python-ml/                 # Microservicio OCR/ML (Python)
├── agent-templates/               # Plantillas FLIT
├── .cursor/                       # Agentes, skills, rules, workflows
├── docs/
│   └── decisions/                 # ADRs (ADR-001: Clean Architecture + SOLID)
├── package.json                   # Scripts del monorepo
├── pnpm-workspace.yaml            # Workspace pnpm (frontend)
├── pnpm-lock.yaml
├── docker-compose.prod.yml        # Compose de producción (VPS)
└── CLAUDE.md                      # Fuente de verdad para agentes
```

---

## Cómo ejecutar el proyecto

### 1. Clonar e instalar dependencias

```bash
git clone https://github.com/flitsas/abrahamc.git
cd abrahamc
pnpm install
dotnet restore backend/dotnet/Flit.slnx
```

> **Importante:** este proyecto usa **pnpm**, no npm. Si ejecutas `npm install`, el script `preinstall` lo bloqueará.

### 2. Levantar la base de datos

**Opción A — Docker (recomendado):**

```bash
# Primera vez o tras cambiar puerto/credenciales: recrear contenedor + volumen
docker compose -f infra/docker-compose.yml down -v
docker compose -f infra/docker-compose.yml up postgres -d
```

El contenedor expone PostgreSQL en el puerto **5433** del host (no 5432) para evitar conflictos con una instalación local de PostgreSQL en Windows.

Comprueba que el mapeo sea `5433->5432` antes de migrar:

```bash
docker compose -f infra/docker-compose.yml ps
# Debe mostrar: 0.0.0.0:5433->5432/tcp
```

**Opción B — PostgreSQL local:**

Crear base de datos con:

| Parámetro | Valor |
|-----------|-------|
| Host | `localhost` |
| Puerto | `5432` |
| Base de datos | `flit_dev` |
| Usuario | `flit` |
| Contraseña | `flit_local` |

**Aplicar migraciones EF Core:**

```bash
pnpm run migrate
```

**Si `pnpm run migrate` falla con `28P01` (autenticación):**

1. Verifica que no tengas `ConnectionStrings__Core` definida en el sistema con credenciales distintas (`Get-ChildItem Env:ConnectionStrings__Core` en PowerShell).
2. Reinicia el contenedor con volumen limpio:

```bash
docker compose -f infra/docker-compose.yml down -v
docker compose -f infra/docker-compose.yml up postgres -d
pnpm run migrate
```

### 3. Configurar variables de entorno

**Backend** — editar `backend/dotnet/src/Flit.Api/appsettings.Development.json`:

| Clave | Descripción | Valor DEV |
|-------|-------------|-----------|
| `ConnectionStrings:Core` | Cadena PostgreSQL (Docker) | `Host=localhost;Port=5433;Database=flit_dev;Username=flit;Password=flit_local` |
| `Cors:AllowedOrigins` | Origen del frontend | `http://localhost:5173` |

También puedes sobreescribir con variables de entorno:

```bash
# PowerShell
$env:ConnectionStrings__Core = "Host=localhost;Port=5433;Database=flit_dev;Username=flit;Password=flit_local"
$env:Cors__AllowedOrigins = "http://localhost:5173"
```

**Frontend** — copiar el ejemplo y ajustar:

```bash
# Linux / macOS
cp frontend/.env.example frontend/.env.local

# Windows (PowerShell)
Copy-Item frontend\.env.example frontend\.env.local
```

Contenido de `frontend/.env.local` (o `frontend/.env`):

```
# Ruta relativa: Vite hace proxy de /api → http://localhost:3030
VITE_API_BASE_URL=/api/v1
```

> Si el login muestra error de conexión, confirma que el backend esté en marcha (`pnpm run dev:api` o `pnpm run dev`) y que Postgres esté activo en el puerto **5433**.

### 4. Levantar el backend (.NET)

**Desde la raíz del monorepo:**

```bash
pnpm run dev:api
```

Equivale a:

```bash
dotnet watch run --project backend/dotnet/src/Flit.Api/Flit.Api.csproj
```

**Desde `backend/dotnet/`:**

```bash
cd backend/dotnet
dotnet watch run --project src/Flit.Api/Flit.Api.csproj
```

El backend queda disponible en:

| Recurso | URL |
|---------|-----|
| API base | http://localhost:3030/api/v1 |
| Health check | http://localhost:3030/api/v1/health |
| OpenAPI (DEV) | http://localhost:3030/openapi/v1.json |

### 5. Levantar el frontend (React + Vite)

**Desde la raíz:**

```bash
pnpm run dev:frontend
```

**Desde `frontend/`:**

```bash
cd frontend
pnpm dev
```

El frontend queda en **http://localhost:5173**. Vite hace proxy de `/api` hacia `http://localhost:3030`.

**Credenciales DEV** (tras `pnpm run migrate` y seed `AddTramites20DevFunctionalSeed`):

| Usuario | Contraseña |
|---------|------------|
| `superadmin@flit.com.co` | `FlitDev2026!` |
| `operador1@transportes-andina.com` | `FlitDev2026!` |

### 6. Levantar backend + frontend juntos

```bash
pnpm run dev
```

Inicia en este orden:
1. `pnpm run dev:api` → API .NET con hot reload (puerto **3030**)
2. Espera a que responda `http://localhost:3030/api/v1/health`
3. `pnpm run dev:frontend` → Vite dev server (puerto **5173**)

**Si Vite dice que el puerto 5173 está ocupado**, cierra la instancia anterior:

```powershell
# PowerShell — ver qué proceso usa el puerto
Get-NetTCPConnection -LocalPort 5173 -ErrorAction SilentlyContinue | Select-Object OwningProcess
Stop-Process -Id <PID> -Force
```

**Si la API no arranca en 3030**, repite el mismo procedimiento con el puerto `3030`.

### 7. Stack completo con Docker

Levanta PostgreSQL, API y frontend containerizados:

```bash
docker compose -f infra/docker-compose.yml up --build
```

| Servicio | Puerto host | Descripción |
|----------|-------------|-------------|
| PostgreSQL | 5433 | Base de datos (host; interno 5432) |
| core-api | 3030 | API .NET (interno 8081) |
| frontend | 5173 | SPA servida por nginx (interno 80) |

### 8. Producción (VPS)

Usar `docker-compose.prod.yml` con un archivo `.env` en el servidor:

```bash
docker compose -f docker-compose.prod.yml up -d
```

Variables requeridas en `.env`:

| Variable | Descripción |
|----------|-------------|
| `ConnectionStrings__Core` | Cadena PostgreSQL del host |
| `Cors__AllowedOrigins` | URL pública del frontend |

---

## URLs y puertos (desarrollo local)

| Servicio | URL / Puerto |
|----------|--------------|
| Frontend (Vite) | http://localhost:5173 |
| API (`Flit.Api`) | http://localhost:3030 |
| Health check | http://localhost:3030/api/v1/health |
| PostgreSQL (Docker) | localhost:5433 |
| Python ML (opcional) | http://localhost:4012/health |

---

## Scripts disponibles

### Raíz del monorepo (`package.json`)

| Comando | Descripción |
|---------|-------------|
| `pnpm install` | Instalar dependencias del workspace |
| `pnpm run dev` | Backend + frontend en paralelo |
| `pnpm run dev:api` | Solo API .NET (`dotnet watch`) |
| `pnpm run dev:frontend` | Solo frontend (Vite) |
| `pnpm run build` | Compilar API + frontend |
| `pnpm run build:api` | Solo `dotnet build` de la solución |
| `pnpm run test` | Tests API + frontend |
| `pnpm run test:api` | Solo `dotnet test` |
| `pnpm run migrate` | Aplicar migraciones EF Core |
| `pnpm run lint` | ESLint del frontend |
| `pnpm run format:check` | Prettier check del frontend |

### Frontend (`frontend/package.json`)

| Comando | Descripción |
|---------|-------------|
| `pnpm dev` | Servidor de desarrollo Vite |
| `pnpm build` | `tsc` + build de producción |
| `pnpm preview` | Previsualizar build de producción |
| `pnpm test` | Vitest (unitarios) |
| `pnpm test:watch` | Vitest en modo watch |
| `pnpm test:coverage` | Cobertura con Vitest |
| `pnpm test:e2e` | Playwright E2E |
| `pnpm lint` / `pnpm lint:fix` | ESLint |
| `pnpm format` / `pnpm format:check` | Prettier |
| `pnpm typecheck` | Verificación de tipos TypeScript |

### Backend (comandos directos)

```bash
# Compilar
dotnet build backend/dotnet/Flit.slnx

# Ejecutar sin watch
dotnet run --project backend/dotnet/src/Flit.Api/Flit.Api.csproj

# Tests
dotnet test backend/dotnet/src/Flit.Api/Flit.Api.csproj

# Crear migración
dotnet ef migrations add <Nombre> \
  --project backend/dotnet/src/Flit.Infrastructure/Flit.Infrastructure.csproj \
  --startup-project backend/dotnet/src/Flit.Api/Flit.Api.csproj

# Aplicar migraciones
pnpm run migrate
```

### Servicio Python ML (opcional)

```bash
cd services/python-ml
uv sync --extra dev
uv run uvicorn app.main:app --reload --port 4012
```

Health: http://localhost:4012/health

---

## Compilar y verificar

```bash
# Compilación completa (backend + frontend)
pnpm run build

# Solo backend
pnpm run build:api

# Solo frontend
pnpm --filter @flit/frontend build

# Lint
pnpm run lint
```

---

## Arquitectura

### Backend (.NET — Clean Architecture + SOLID)

El backend cumple **Clean Architecture estricta** y los **principios SOLID**. ADR **Aceptado**: [`docs/decisions/ADR-001-clean-architecture-solid.md`](docs/decisions/ADR-001-clean-architecture-solid.md).

```
backend/dotnet/src/
  Flit.Api/                # Endpoints, middleware, configuración
  Flit.Infrastructure/     # EF Core, migraciones, repositorios
  Flit.Modules.<Modulo>/
    Domain/                # Entidades y reglas de negocio (sin EF Core)
    Application/           # Handlers / casos de uso (SRP)
    Ports/                 # Interfaces (DIP, ISP)
    Adapters/              # Implementaciones EF Core o externas
```

| Principio | Aplicación |
|-----------|------------|
| **S** | Un handler = un caso de uso |
| **O** | Extender con nuevos handlers, no modificar Domain con `if` gigantes |
| **L** | Repositorios InMemory intercambiables en tests |
| **I** | Puertos pequeños por responsabilidad |
| **D** | Application depende de `Ports/`, no de `DbContext` |

Detalle en [`backend/CLAUDE.md`](backend/CLAUDE.md) · Ejemplos en [`agent-templates/code-style-guide.md`](agent-templates/code-style-guide.md).

### Frontend (Feature-sliced)

```
frontend/src/features/<feature>/
  api/                     # Schemas Zod + hooks TanStack Query
  components/              # UI del feature
  pages/                   # Componentes de ruta

frontend/src/shared/
  api/client.ts            # Cliente Axios
  components/ui/           # Primitivos UI reutilizables
```

Detalle en [`frontend/CLAUDE.md`](frontend/CLAUDE.md).

---

## Flujo de trabajo Git

| Rama | Propósito |
|------|-----------|
| `develop` | Integración diaria (**rama predeterminada**) |
| `main` | Producción |
| `agent/<tipo>/<US-ID>-<slug>` | Ramas de trabajo por historia de usuario |

- Las **PRs** siempre apuntan a `develop`.
- Convención de commits: `feat(modulo): descripción [#US-ID]`

---

## Agentes IA

El proyecto incluye agentes especializados en `.cursor/agents/` para orquestar el ciclo de desarrollo:

| Agente | Uso |
|--------|-----|
| Orchestrator | Flujos completos end-to-end |
| Tech Lead | Features, descomposición US, DoR/DoD |
| Architecture | ADRs, diseño técnico |
| Backend | Implementar historias [BACKEND] |
| Frontend | Implementar historias [FRONTEND] |
| Database | Migraciones, schemas, RLS |
| Code Review | Revisión de PRs |
| QA | Test cases, Playwright, bugs |
| Security | SAST, SCA, secretos, Habeas Data |
| Infra | Docker, CI/CD, deploys |
| Integration | PRs, Azure DevOps, deploys |

Punto de entrada: [`CLAUDE.md`](CLAUDE.md).

---

## Seguridad

- No commitear `.env`, `env.user-identity`, credenciales ni secretos JWT.
- Gitleaks configurado en `.gitleaks.toml`.
- Variables sensibles del backend van en `appsettings.*.json` (DEV) o variables de entorno (QA/PDN).
- El frontend solo expone variables `VITE_*`.

---

## Licencia

Proyecto privado de la organización **flitsas**. Uso interno del equipo FLIT.
