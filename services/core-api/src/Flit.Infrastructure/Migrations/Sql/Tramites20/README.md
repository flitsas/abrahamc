# Migraciones Trámites 2.0 (SQL embebido)

## Features cubiertos

| Migración EF | Features ADO | DDL |
|---|---|---|
| `AddTramites20Foundation` (`20260603213625`, **antes** de rules/50) | #9370, #9466, #9381, #9383, #9378, #9379 | 00, 20, 10, 25, 30, 40 |
| `AddProceduresConfigRulesRgl01` | #9410 (parcial) | rules + endpoint_catalog |
| `AddIntegrationsEndpointCallLogRgl02` | #9410 | endpoint_call_log |
| `AddProceduresConfigParametrization50` | #9408, #9409, #9410 | 50 |
| `AddTramites20RuntimeLayer` | #9467, #9408, #9469, #9369 | 70, 75, 76, 80, 90 |
| `AddTramites20DevFunctionalSeed` | Todos (escenario DEV) | seed mock |
| `AddOtTrafficAgenciesCatalogSeed` | #9378 #9454 (OT-01) | catálogo ~359 OT en `ot.traffic_agencies` |
| `AddFeatures9549_9557FoundationGaps` (`20260610144632`) | #9549 #9550 #9553 #9557 | ABAC, excepciones vehiculares, matriz OT tenant, SMTP templates, etiquetas OT |
| `AddFeatures9549_9557DevSeed` (`20260610144706`) | #9549 #9550 #9553 #9557 | mocks DEV complementarios |

## Crear migración SQL embebida (obligatorio)

1. SQL en `Sql/Tramites20/<Nombre>_up.sql` y `_down.sql` (embebidos vía `Flit.Infrastructure.csproj`).
2. Par EF en `Migrations/`:
   - `<timestamp>_<Nombre>.cs` → `SqlMigrationHelper.ApplyEmbeddedSql(...)`
   - **`<timestamp>_<Nombre>.Designer.cs`** → `[Migration("...")]` + `BuildTargetModel` (copiar de la migración anterior si no hay cambio de modelo EF).
3. Verificar: `dotnet ef migrations list` debe listar la migración; `pnpm run validate:ef-migrations` antes de push.

**Sin `.Designer.cs`, core-api arranca pero la tabla no se crea** (error `relation does not exist` en runtime). Ver `.cursor/rules/ef-migrations.mdc`.

## Aplicar en local (recomendado BD limpia)

```bash
pnpm docker:up:infra
pnpm migrate:core-api
```

Si ya tenías el shell MVP (`identity_users`) y falla `identity.tenants`:

```bash
pnpm docker:reset:infra   # si existe en el monorepo
pnpm migrate:core-api
```

## Datos mock DEV

Tras `AddTramites20DevFunctionalSeed`:

| Tenant | Usuario | Password |
|---|---|---|
| Transportes Andina | `operador1@transportes-andina.com` | `FlitDev2026!` |
| Logística del Caribe | `operador1@logistica-caribe.com` | `FlitDev2026!` |
| Super Admin | `superadmin@flit.com.co` | `FlitDev2026!` |

UUIDs estables: `docs/designs/tramites-2.0/ejemplos-datos.html`

## Nota #9382

El ID **#9382** no aparece en el plan Trámites 2.0 (`03-detalle-features-hu.md`). Revisar en ADO si es typo de #9381/#9383.

## Fuente canónica

Los `.sql` embebidos se generan desde `docs/designs/tramites-2.0/ddl/`. Para regenerar Foundation/Runtime:

```powershell
# Ver script en historial del repo o re-ejecutar concatenación desde ddl/
```
