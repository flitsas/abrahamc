---
name: flit-openapi-contract
description: Mantener contratos OpenAPI de core-api. Invocar cuando el backend-agent agrega o modifica endpoints, cuando QA pide Swagger, o antes de marcar una HU Resolved con cambios de API. Actualiza docs/openapi.yaml, verifica MapOpenApi en Program.cs y documenta endpoints en comentarios ADO de la HU.
---

# OpenAPI / Swagger — FLIT core-api

## Objetivo

Garantizar que **toda HU con endpoints REST** deje trazabilidad en:
1. `docs/openapi.yaml` (spec canónica versionada — DoD-US #8)
2. Runtime `GET /openapi/v1.json` (auto-generada desde Minimal APIs)
3. Comentario HTML en la HU de Azure DevOps (tabla de endpoints)

## Cuándo invocar

| Trigger | Acción |
|---------|--------|
| Nueva HU backend con endpoints | Actualizar los 3 artefactos antes de handoff |
| Modificación de ruta, método o permiso | Patch en `docs/openapi.yaml` + comentario ADO |
| QA reporta Swagger faltante | Verificar `Program.cs` tiene `AddOpenApi` + `MapOpenApi` |
| Feature Closed (DoD C2) | Confirmar spec publicada en DEV |

## Pre-flight

1. Leer `services/core-api/src/Flit.Api/Program.cs` — debe existir:
   - `builder.Services.AddOpenApi("v1", ...)`
   - `FlitOpenApiSecurityTransformers.Configure(options)` (bearerAuth + sessionCookie)
   - `app.MapOpenApi()`
   - `app.UseSwaggerUI` con `PersistAuthorization = true`
2. Leer `docs/openapi.yaml` vigente
3. Identificar archivo `*Endpoints.cs` de la HU

## Flujo (obligatorio tras implementar endpoints)

### Paso 1 — Registrar en código

En cada endpoint Minimal API:
- `.WithName("OperationId")` — único, PascalCase
- `.WithTags("Grupo")` — alineado con tags de openapi.yaml
- `.WithSummary("...")` — cuando el propósito no es obvio
- `.RequireTramitesPermission("slug")` o `.AllowAnonymous()` según AC

### Paso 2 — Actualizar `docs/openapi.yaml`

- Agregar/actualizar `paths` con método, parámetros, responses mínimos
- Tag debe incluir Feature/HU en description: `Feature #9550 — HU #9687`
- `operationId` = mismo `WithName` del endpoint
- No inventar campos: reflejar el contrato real del handler

### Paso 3 — Verificar runtime

```bash
cd services/core-api
dotnet build src/Flit.Api/Flit.Api.csproj
# Con API levantada:
curl -s http://localhost:3030/openapi/v1.json | head
```

Local (core-api directo — siempre funciona): `http://localhost:3030/swagger` · spec: `/openapi/v1.json`

Local vía SPA (`pnpm dev`): `http://localhost:5173/swagger` solo si `frontend/vite.config.ts` proxea `/swagger` y `/openapi` a `:3030`. Sin ese proxy, `:5173/swagger` carga el React SPA y redirige a `/login`.

**QA — probar endpoints protegidos en Swagger:**
1. `POST /api/v1/auth/login` (sin Authorize) → copiar `accessToken`
2. Clic **Authorize** → pegar token en `bearerAuth` (o cookie `flit_access` en `sessionCookie`)
3. Probar endpoints con candado (ej. `GET /api/v1/companies`)

Verificar en JSON: `components.securitySchemes.bearerAuth` y `security` en operaciones protegidas.

DEV desplegado (vía gateway YARP + nginx frontend):
- **Swagger UI (QA):** `https://dev.abrahamc.flitsas.online/swagger`
- Spec JSON: `https://dev.abrahamc.flitsas.online/openapi/v1.json`
- Alternativa API: `https://dev.api.abrahamc.flitsas.online/swagger`

**No usar** `dev.core.abrahamc.flitsas.online` — no tiene registro DNS; core-api es interno al compose.

### Paso 4 — Comentario en ADO (HU)

Usar `@flit-azure-devops` → `wit_add_work_item_comment` con HTML:

```html
<h2>Contrato API — OpenAPI</h2>
<p><strong>Spec:</strong> <code>docs/openapi.yaml</code> |
<strong>Runtime:</strong> <code>GET /openapi/v1.json</code></p>
<table>
  <tr><th>Método</th><th>Ruta</th><th>Permiso</th><th>OperationId</th></tr>
  ...
</table>
<p><em>Sin endpoints HTTP:</em> indicar explícitamente (ej. solo migración/schema).</p>
```

### Paso 5 — HUs solo frontend (#9691, etc.)

Documentar en comentario los endpoints **consumidos** (dependencias backend), no inventar rutas nuevas.

## HUs sin endpoints HTTP

Si la HU es solo schema/migración/servicio interno (#9682):
- Comentario ADO: "Sin endpoints REST — alcance: migración EF + servicio `IGlobalEmailService`"
- No agregar paths ficticios a openapi.yaml

## Responsables

| Rol | Responsabilidad |
|-----|-----------------|
| **backend-agent** | Pasos 1–4 en la misma sesión de implementación |
| **integration-agent** | Verificar comentario OpenAPI antes de merge |
| **qa-agent** | Consumir `docs/openapi.yaml` + `/openapi/v1.json` para TCs API |

## Checklist DoD-US #8

- [ ] `docs/openapi.yaml` actualizado en el commit de la HU
- [ ] `MapOpenApi` responde 200 en DEV
- [ ] Comentario ADO con tabla de endpoints (o "sin endpoints HTTP")
- [ ] `operationId` coincide entre código y YAML
