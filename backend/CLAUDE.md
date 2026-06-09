# backend/CLAUDE.md — Convenciones específicas del backend FLIT (.NET)

Lee también el `CLAUDE.md` raíz para convenciones generales.

## Stack

.NET 10 + C# 14 + ASP.NET Core Minimal APIs + EF Core + PostgreSQL 16 + xUnit + Serilog + OpenTelemetry

## Ubicación

```
backend/dotnet/
  src/
    Flit.Api/              # Host HTTP, endpoints, middleware
    Flit.Gateway/          # API Gateway (YARP) — opcional en DEV
    Flit.Infrastructure/   # EF Core, DbContext, migraciones
    Flit.SharedKernel/     # Tipos compartidos, abstracciones
    Flit.Modules.*/        # Módulos de dominio (Clean Architecture)
  tests/                   # Proyectos xUnit (cuando existan)
  Flit.slnx                # Solución
  global.json              # SDK 10.0.300
```

## Estructura por módulo

```
src/Flit.Modules.<Modulo>/
  Domain/          # Entidades, value objects, reglas de negocio puras
  Application/     # Casos de uso, handlers, puertos (interfaces)
  Infrastructure/  # Implementaciones EF Core, adaptadores externos (si aplica)
  Ports/           # Contratos que Application expone hacia fuera
```

**Regla de dependencias**: Domain ← Application ← Infrastructure. `Flit.Api` solo orquesta endpoints y DI.

> **ADR Aceptado:** `docs/decisions/ADR-001-clean-architecture-solid.md` — Clean Architecture + SOLID son obligatorios en todo código nuevo.

## Clean Architecture — reglas de capas

| Capa | Ubicación | Puede importar | No puede importar |
|------|-----------|----------------|-------------------|
| **Domain** | `Flit.Modules.*/Domain/` | Tipos del mismo módulo, `Flit.SharedKernel` | EF Core, ASP.NET, HTTP, `Flit.Infrastructure`, otros módulos Application |
| **Application** | `Flit.Modules.*/Application/` | Domain, `Ports/` del módulo | EF Core, `DbContext`, Minimal API, implementaciones concretas de repos |
| **Ports** | `Flit.Modules.*/Ports/` | Tipos de Domain (DTOs, IDs) | Implementaciones de infraestructura |
| **Adapters** | `Flit.Modules.*/Adapters/` o `Flit.Infrastructure/` | Application, Ports, EF Core | Endpoints HTTP |
| **API** | `Flit.Api/Endpoints/` | Application handlers, DTOs de request/response | Lógica de negocio, `DbContext`, queries LINQ |

**Flujo de un caso de uso:**

```
HTTP Request → Endpoint (mapea DTO) → Handler (Application) → Port (interface) → Repository (Infrastructure)
```

## Principios SOLID (obligatorios)

| Principio | Regla en este repo | Ejemplo correcto |
|-----------|-------------------|------------------|
| **S** — Single Responsibility | Un handler/clase = una razón para cambiar | `CreateEmployeeHandler` solo crea empleados |
| **O** — Open/Closed | Extender con nuevos handlers/adaptadores, no modificando Domain | Nuevo trámite = nuevo handler, no `if` en handler existente |
| **L** — Liskov Substitution | Implementaciones de `Ports/` intercambiables (EF, InMemory en tests) | `InMemoryUsersRepository` sustituye `EfUsersRepository` en tests |
| **I** — Interface Segregation | Interfaces pequeñas en `Ports/` por responsabilidad | `IEmployeeReadRepository` vs `IEmployeeWriteRepository` si aplica |
| **D** — Dependency Inversion | Application depende de abstracciones (`Ports/`), no de EF Core | Handler recibe `IEmployeeRepository`, no `FlitDbContext` |

## Anti-patrones prohibidos

- Lógica de negocio en `Flit.Api/Endpoints/` (validaciones de dominio, cálculos, reglas de trámite).
- `DbContext` o `IQueryable` en Domain o Application.
- Handlers que instancian repositorios con `new` — siempre inyección por constructor.
- God-handlers con múltiples responsabilidades o `switch` gigante por tipo de trámite.
- Domain entities con atributos EF (`[Column]`, `[Table]`) — mapping solo en Infrastructure.
- Referencias cruzadas entre módulos: comunicación vía eventos, ports compartidos en `SharedKernel` o orquestación en Application de `Flit.Api`.

## Reglas críticas backend

- Lógica de negocio en `Application/`, **nunca** en endpoints de `Flit.Api`
- Persistencia solo en `Flit.Infrastructure` y adaptadores de módulo
- Queries EF Core: LINQ / `IQueryable` solo en infraestructura — nunca en Domain
- Configuración vía `appsettings.*.json` + variables de entorno — nunca hardcodear secretos
- Logging: Serilog — nunca `Console.WriteLine` en producción
- NUNCA loguees `password`, `token`, `jwt`, `secret`, `authorization`, `pin`

## Tests

- Framework: xUnit v3 + FluentAssertions + NSubstitute
- Ubicación: `backend/dotnet/tests/<Proyecto>.Tests/`
- Patrón: AAA (Arrange / Act / Assert)
- Cobertura mínima sobre código nuevo: 80%
- Ejecutar: `dotnet test backend/dotnet/Flit.slnx` o `pnpm run test:api`

## Migraciones EF Core

- Ubicación: `backend/dotnet/src/Flit.Infrastructure/Migrations/`
- Crear: `dotnet ef migrations add <Nombre> --project src/Flit.Infrastructure --startup-project src/Flit.Api`
- Aplicar: `pnpm run migrate` (desde raíz) o `dotnet ef database update`
- NUNCA modificar una migración ya aplicada en otro ambiente

## Puertos y URLs (DEV)

| Servicio | URL |
|----------|-----|
| API (`Flit.Api`) | http://localhost:3030 |
| Health | http://localhost:3030/api/v1/health |
| Frontend (Vite) | http://localhost:5173 |

## Variables de entorno clave

| Variable | Descripción |
|----------|-------------|
| `ConnectionStrings__Core` | PostgreSQL connection string |
| `Cors__AllowedOrigins` | Origen del frontend (ej. `http://localhost:5173`) |
| `ASPNETCORE_URLS` | Bind address (default DEV: `http://localhost:3030`) |

## Comandos rápidos

```bash
# Desde la raíz del monorepo
pnpm run dev:api         # dotnet watch run (Flit.Api)
pnpm run build:api       # dotnet build
pnpm run test:api        # dotnet test
pnpm run migrate         # EF Core database update

# Desde backend/dotnet/
dotnet watch run --project src/Flit.Api/Flit.Api.csproj
```
