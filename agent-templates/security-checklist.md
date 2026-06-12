# Security Checklist — FLIT

## Puerta pre-push (push directo a `develop` o sin PR)

Si haces **commit + push** sin PR, CI corre **después** del push. Ejecuta **antes** (misma sesión) **un solo comando** (paridad CI completa):

- `pnpm run validate:pre-push:win` (Windows) o `pnpm run validate:pre-push` (Linux/macOS)
- Incluye: Prettier (`format:check`), lint, typecheck, tests/build frontend, `dotnet test`/build, EF migrations, gitleaks
- **Exit code 0 obligatorio** antes de `git push`

Bloqueo mecánico opcional: `pnpm run hooks:install` (activa `.githooks/pre-push`).

Regla Cursor obligatoria: `.cursor/rules/pre-push-gate.mdc`.

---

## Para desarrolladores (inline review — antes de abrir PR)

Verifica manualmente estos 7 patrones ANTES de abrir la PR:

| # | Patrón | Verificación |
|---|--------|--------------|
| 1 | **SQL injection** | No hay strings concatenados en queries. Usa TypeORM query builder o parámetros. |
| 2 | **Hardcoded credentials** | No hay passwords, API keys, tokens como literales en el código. |
| 3 | **Secret logging** | No se logean campos: `password`, `token`, `jwt`, `secret`, `apiKey`, `authorization`. |
| 4 | **dangerouslySetInnerHTML** | Si se usa, siempre con `DOMPurify.sanitize()` en la misma línea. |
| 5 | **eval() / new Function()** | No se usan con input de usuarios. |
| 6 | **CSRF** | Formularios POST/PUT/DELETE tienen token CSRF o el endpoint requiere Authorization header. |
| 7 | **Direct DB en controller** | No hay queries directas a la DB en controllers. Toda lógica en use cases / services. |

## Habeas Data Colombia — Ley 1581 de 2012

Aplica a cualquier campo PII (Personally Identifiable Information):

| Campo PII | Controles requeridos |
|-----------|---------------------|
| `cedula` / `documento` | Consentimiento explícito + encryption at rest + retention policy |
| `email` | Consentimiento explícito + retention policy |
| `telefono` | Consentimiento explícito + retention policy |
| `nombreCompleto` / `nombre` + `apellido` | Consentimiento explícito |
| `direccion` | Consentimiento explícito + retention policy |
| `fechaNacimiento` | Consentimiento explícito |
| Datos sensibles (salud, política, religión) | Consentimiento explícito ESPECIAL (Ley 1581 Art. 6) |

### Controles técnicos mínimos

```typescript
// ✅ Encryption at rest para PII sensible (TypeORM)
@Column({ transformer: new EncryptionTransformer() })
cedula: string

// ✅ Consentimiento explícito en el schema
@Column({ type: 'boolean', default: false })
consentimientoDatosPersonales: boolean

@Column({ type: 'timestamp', nullable: true })
fechaConsentimiento: Date
```

### Retention policy

Documenta en `docs/data-retention.md`:
- ¿Cuánto tiempo se conserva cada tipo de dato PII?
- ¿Qué proceso elimina datos al vencer la retención?
- ¿Hay ADR sobre retención? (`docs/decisions/ADR-XXXX-data-retention.md`)

## Para el Security Agent (análisis profundo)

### Layer 1 — SAST con Semgrep

```bash
# Reglas mínimas
semgrep --config=p/owasp-top-ten \
        --config=p/cwe-top-25 \
        --config=.semgrep.yml \
        --json --output semgrep-report.json .
```

Crear `.semgrep.yml` en la raíz con reglas custom para patrones específicos de FLIT.

### Layer 2 — SCA (dependencias)

```bash
pnpm audit --prod --json > audit-report.json
```

Tolerancia FLIT:
- Critical: 0
- High: 0 (o con excepción documentada en `docs/security-exceptions/`)
- Medium: máximo 5 con justificación

### Layer 3 — Secrets scan

```bash
gitleaks detect --source . --report-format json --report-path gitleaks-report.json
```

Si hay hallazgos → reset del secreto INMEDIATO + documentar en `docs/security-exceptions/`.

### Documentar excepciones

Si un hallazgo es falso positivo, crear `docs/security-exceptions/<hash>.md`:

```markdown
# Excepción de Seguridad: <descripción>

**Fecha**: YYYY-MM-DD
**Herramienta**: semgrep | gitleaks | pnpm audit
**Regla/CVE**: <nombre>
**Archivo:línea**: <path>
**Razón del falso positivo**: <explicación técnica>
**Aprobado por**: <Líder Técnico>
**Próxima revisión**: YYYY-MM-DD
```
