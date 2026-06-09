-- =====================================================================================
-- FLIT Trámites 2.0 — Foundation (Features #9370 #9466 #9381 #9383 #9378 #9379)
-- Orden: 00 → legacy shell cleanup → 20 → 10 → 25 → 30 → 40
-- Requiere BD limpia o migraciones previas shell + procedures_config; ver README Tramites20.
-- =====================================================================================

-- Retirar tablas MVP shell (InitialFlitShell) reemplazadas por modelo Trámites 2.0
DROP SCHEMA IF EXISTS rbac CASCADE;
DROP TABLE IF EXISTS identity.user_roles CASCADE;
DROP TABLE IF EXISTS identity.user_audit_log CASCADE;
DROP TABLE IF EXISTS identity.password_reset_tokens CASCADE;
DROP TABLE IF EXISTS identity.users CASCADE;
DROP TABLE IF EXISTS identity.sync_inconsistencies CASCADE;
DROP TABLE IF EXISTS identity.identity_credentials CASCADE;
DROP TABLE IF EXISTS identity.identity_refresh_tokens CASCADE;
DROP TABLE IF EXISTS identity.identity_users CASCADE;


-- >>> 00-extensions-and-schemas.sql

-- =====================================================================================
-- FLIT 2.0 · Trámites de Tránsito · DDL 00 — Extensiones, schemas y auditoría base
-- =====================================================================================
-- Orden de ejecución: 00 → 10 → 20 → 25 → 30 → 40 → 50 → 70 → 75 → 80 → 90
-- Cumple docs/database-conventions.md (PostgreSQL 17+, EF Core 10, multi-tenant + RLS).
-- Idioma: estructura técnica en inglés; datos/enums de negocio en español (sin tildes/ñ).
-- Reversibilidad: bloque DOWN comentado al final de cada archivo (ver §16).
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1. Extensiones
-- -------------------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS pgcrypto;   -- gen_random_uuid / gen_random_bytes
CREATE EXTENSION IF NOT EXISTS citext;     -- email case-insensitive
CREATE EXTENSION IF NOT EXISTS pg_trgm;    -- búsquedas ILIKE eficientes (filtros dashboard/grids)
-- Si el motor es PostgreSQL 17 con la extensión disponible, se prefiere la nativa:
--   CREATE EXTENSION IF NOT EXISTS pg_uuidv7;
-- En PostgreSQL 18+ uuidv7() es nativo (pg_catalog). El fallback de §2 es inocuo en ambos casos
-- porque pg_catalog se resuelve antes que public en el search_path.

-- -------------------------------------------------------------------------------------
-- 2. uuidv7() — fallback portable (RFC 9562) para PostgreSQL < 18 sin pg_uuidv7
--    En PostgreSQL 18+ la función nativa pg_catalog.uuidv7() tiene precedencia.
-- -------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.uuidv7()
RETURNS uuid
LANGUAGE sql
VOLATILE
AS $$
  -- 48 bits de timestamp unix en milisegundos + version(7)/variant + 74 bits aleatorios
  SELECT encode(
    set_bit(
      set_bit(
        overlay(
          uuid_send(gen_random_uuid())
          PLACING substring(int8send((extract(epoch FROM clock_timestamp()) * 1000)::bigint) FROM 3)
          FROM 1 FOR 6
        ),
        52, 1
      ),
      53, 1
    ),
    'hex'
  )::uuid;
$$;

COMMENT ON FUNCTION public.uuidv7() IS
  'Fallback RFC 9562 UUIDv7 para PG<18. Ordenable temporalmente. En PG18+ se usa la nativa.';

-- -------------------------------------------------------------------------------------
-- 3. Schemas (bounded contexts). public queda vacío salvo extensiones/uuidv7.
--    Jerarquía de referencias (evita ciclos de FK; alto → bajo):
--    dashboard > procedures > identity_verification > integrations > procedures_config
--             > ot > companies > files > identity > catalogs
-- -------------------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS catalogs;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS files;
CREATE SCHEMA IF NOT EXISTS companies;
CREATE SCHEMA IF NOT EXISTS ot;
CREATE SCHEMA IF NOT EXISTS procedures_config;
CREATE SCHEMA IF NOT EXISTS integrations;
CREATE SCHEMA IF NOT EXISTS identity_verification;
CREATE SCHEMA IF NOT EXISTS procedures;
CREATE SCHEMA IF NOT EXISTS dashboard;
CREATE SCHEMA IF NOT EXISTS audit;

COMMENT ON SCHEMA catalogs            IS 'Datos de referencia compartidos (sin tenant_id).';
COMMENT ON SCHEMA identity            IS 'Tenants (compañía-raíz), usuarios, roles, permisos-slug, onboarding. #9370';
COMMENT ON SCHEMA files               IS 'Metadatos de objetos en MinIO; substrato binario (referenciado por file_id).';
COMMENT ON SCHEMA companies           IS 'Maestro de compañía (1:1 tenant), config modular, firmas, escrituras. #9381 #9383';
COMMENT ON SCHEMA ot                  IS 'Organismos de Tránsito (cross-tenant, sin tenant_id), reglas OT, orden consolidado, QX. #9378 #9379';
COMMENT ON SCHEMA procedures_config   IS 'Parametrización: familias/tipos/aristas/matriz/forms/docs/consultas/reglas. #9408 #9409 #9410';
COMMENT ON SCHEMA integrations        IS 'Ejecución de conectores externos: logs, RUNT/Verifik/Intempo, webhooks QX.';
COMMENT ON SCHEMA identity_verification IS 'Liveness/biometría: sesiones, veredictos, evidencias (@pii:high). §8 reglas-estándar';
COMMENT ON SCHEMA procedures          IS 'Runtime: instancias radicadas con snapshot inmutable de config.';
COMMENT ON SCHEMA dashboard           IS 'Read model (vistas/MV) para KPIs y exportación. #9369';
COMMENT ON SCHEMA audit               IS 'Bitácora transversal de cambios y de accesos a datos.';

-- -------------------------------------------------------------------------------------
-- 4. Auditoría — tabla central (§11) + bitácora de accesos a datos (ADR-0006 C5)
-- -------------------------------------------------------------------------------------
CREATE TABLE audit.audit_log (
  id            uuid         PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid         NOT NULL,   -- denormalizado; sentinela para config global de plataforma
  schema_name   text         NOT NULL,
  table_name    text         NOT NULL,
  record_id     uuid         NOT NULL,
  operation     char(1)      NOT NULL CHECK (operation IN ('I','U','D')),
  changed_by    uuid         NOT NULL,
  changed_at    timestamptz  NOT NULL DEFAULT now(),
  old_values    jsonb        NULL,
  new_values    jsonb        NULL,
  request_id    uuid         NULL,
  ip_address    inet         NULL
);
CREATE INDEX ix_audit_log_tenant_id_changed_at ON audit.audit_log (tenant_id, changed_at DESC);
CREATE INDEX ix_audit_log_record               ON audit.audit_log (schema_name, table_name, record_id);
COMMENT ON TABLE audit.audit_log IS '@context:audit Bitácora inmutable de cambios (INSERT/UPDATE/DELETE) por trigger.';

-- Bitácora de accesos/lecturas sensibles (emisión de URLs prefirmadas, lectura de PII).
CREATE TABLE audit.data_access_log (
  id            uuid         PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid         NOT NULL,
  actor_user_id uuid         NULL,
  schema_name   text         NOT NULL,
  table_name    text         NOT NULL,
  record_id     uuid         NULL,
  access_type   text         NOT NULL CHECK (access_type IN ('read','download','presign','export')),
  purpose       text         NULL,
  accessed_at   timestamptz  NOT NULL DEFAULT now(),
  request_id    uuid         NULL,
  ip_address    inet         NULL
);
CREATE INDEX ix_data_access_log_tenant_id_accessed_at ON audit.data_access_log (tenant_id, accessed_at DESC);
COMMENT ON TABLE audit.data_access_log IS '@context:audit Trazabilidad de accesos a datos sensibles (Habeas Data / ADR-0006).';

-- Sentinela de plataforma para auditar config GLOBAL (sin tenant). audit_log.tenant_id no tiene FK.
-- '00000000-0000-7000-8000-000000000000' = "FLIT Platform".

-- -------------------------------------------------------------------------------------
-- 5. Funciones de trigger genéricas
-- -------------------------------------------------------------------------------------

-- 5.1 Concurrencia optimista + touch updated_at (tablas de negocio con row_version)
CREATE OR REPLACE FUNCTION audit.increment_row_version()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
  NEW.row_version := OLD.row_version + 1;
  NEW.updated_at  := now();
  RETURN NEW;
END;
$$;

-- 5.2 Touch updated_at (catálogos y tablas sin row_version)
CREATE OR REPLACE FUNCTION audit.touch_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END;
$$;

-- 5.3 Auditoría genérica AFTER I/U/D → audit.audit_log
--     Resuelve tenant/usuario desde la fila o desde variables de sesión de la app.
CREATE OR REPLACE FUNCTION audit.log_change()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
DECLARE
  v_old jsonb;
  v_new jsonb;
  v_op  char(1);
  v_record_id uuid;
  v_tenant_id uuid;
  v_changed_by uuid;
  v_request_id uuid;
  v_ip inet;
  c_platform constant uuid := '00000000-0000-7000-8000-000000000000';
BEGIN
  IF (TG_OP = 'INSERT') THEN
    v_op := 'I'; v_new := to_jsonb(NEW); v_old := NULL;
  ELSIF (TG_OP = 'UPDATE') THEN
    v_op := 'U'; v_new := to_jsonb(NEW); v_old := to_jsonb(OLD);
  ELSE
    v_op := 'D'; v_new := NULL; v_old := to_jsonb(OLD);
  END IF;

  v_record_id := COALESCE((v_new->>'id')::uuid, (v_old->>'id')::uuid);

  v_tenant_id := COALESCE(
    (v_new->>'tenant_id')::uuid,
    (v_old->>'tenant_id')::uuid,
    NULLIF(current_setting('app.current_tenant_id', true), '')::uuid,
    c_platform
  );

  v_changed_by := COALESCE(
    NULLIF(current_setting('app.current_user_id', true), '')::uuid,
    (v_new->>'updated_by')::uuid,
    (v_new->>'created_by')::uuid,
    (v_old->>'updated_by')::uuid,
    c_platform
  );

  v_request_id := NULLIF(current_setting('app.request_id', true), '')::uuid;
  BEGIN
    v_ip := NULLIF(current_setting('app.client_ip', true), '')::inet;
  EXCEPTION WHEN others THEN
    v_ip := NULL;
  END;

  INSERT INTO audit.audit_log (
    tenant_id, schema_name, table_name, record_id, operation,
    changed_by, old_values, new_values, request_id, ip_address
  ) VALUES (
    v_tenant_id, TG_TABLE_SCHEMA, TG_TABLE_NAME, v_record_id, v_op,
    v_changed_by, v_old, v_new, v_request_id, v_ip
  );

  IF (TG_OP = 'DELETE') THEN RETURN OLD; ELSE RETURN NEW; END IF;
END;
$$;

COMMENT ON FUNCTION audit.log_change() IS
  'Trigger genérico de auditoría. Aplicar AFTER INSERT OR UPDATE OR DELETE en tablas de negocio y config.';

-- -------------------------------------------------------------------------------------
-- 6. Validadores del árbol de reglas no-code (#9410). Usados en CHECK de ot_rules y
--    procedures_config.rules para que la BD rechace árboles malformados (sin SQL, solo claves).
--    Operadores y tipos de acción = conjuntos CERRADOS (sin inyección).
-- -------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.is_valid_rule_condition(node jsonb)
RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
  child jsonb;
  op    text;
BEGIN
  IF node IS NULL OR node = 'null'::jsonb THEN
    RETURN true;  -- condición vacía = regla siempre verdadera
  END IF;
  IF jsonb_typeof(node) <> 'object' THEN RETURN false; END IF;

  -- Grupo lógico AND/OR
  IF node ? 'op' THEN
    op := node->>'op';
    IF op NOT IN ('AND','OR') THEN RETURN false; END IF;
    IF jsonb_typeof(node->'children') <> 'array' THEN RETURN false; END IF;
    FOR child IN SELECT * FROM jsonb_array_elements(node->'children') LOOP
      IF NOT public.is_valid_rule_condition(child) THEN RETURN false; END IF;
    END LOOP;
    RETURN true;
  END IF;

  -- Hoja: field + operator [+ value]
  IF NOT (node ? 'field' AND node ? 'operator') THEN RETURN false; END IF;
  IF (node->>'operator') NOT IN
     ('equal','notEqual','greater','less','contains','isEmpty','isNotEmpty') THEN
    RETURN false;
  END IF;
  IF (node->>'operator') NOT IN ('isEmpty','isNotEmpty') THEN
    IF jsonb_typeof(node->'value') <> 'object' THEN RETURN false; END IF;
    IF (node->'value'->>'kind') NOT IN ('static','field') THEN RETURN false; END IF;
  END IF;
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION public.is_valid_rule_actions(actions jsonb)
RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE a jsonb;
BEGIN
  IF actions IS NULL OR actions = 'null'::jsonb THEN RETURN true; END IF;
  IF jsonb_typeof(actions) <> 'array' THEN RETURN false; END IF;
  FOR a IN SELECT * FROM jsonb_array_elements(actions) LOOP
    IF jsonb_typeof(a) <> 'object' THEN RETURN false; END IF;
    IF (a->>'type') NOT IN ('popup_modal','inject_section','call_endpoint','block') THEN
      RETURN false;
    END IF;
  END LOOP;
  RETURN true;
END;
$$;

COMMENT ON FUNCTION public.is_valid_rule_condition(jsonb) IS 'Valida estructuralmente el árbol AND/OR de condiciones (#9410).';
COMMENT ON FUNCTION public.is_valid_rule_actions(jsonb)   IS 'Valida la lista tipada de acciones (#9410).';

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP FUNCTION IF EXISTS audit.log_change();
-- DROP FUNCTION IF EXISTS audit.touch_updated_at();
-- DROP FUNCTION IF EXISTS audit.increment_row_version();
-- DROP TABLE IF EXISTS audit.data_access_log;
-- DROP TABLE IF EXISTS audit.audit_log;
-- DROP SCHEMA IF EXISTS audit, dashboard, procedures, identity_verification, integrations,
--   procedures_config, ot, companies, files, identity, catalogs CASCADE;
-- DROP FUNCTION IF EXISTS public.uuidv7();


-- >>> 20-identity.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 20 — Identidad, autenticación y gobernanza multi-tenant (#9370)
-- Tenant = Compañía B2B. Reconstruido desde cero.
-- Patrón RLS estándar: tenant match en escritura; lectura con bypass SuperAdmin.
-- Bootstrap: tenants/users permiten created_by/updated_by NULL (excepción documentada ADR-0009).
-- =====================================================================================
SET search_path TO identity, public;

-- Helper de SuperAdmin (lee GUC app.is_super_admin). Usado por políticas RLS de todos los schemas.
CREATE OR REPLACE FUNCTION identity.is_super_admin()
RETURNS boolean LANGUAGE sql STABLE AS $$
  SELECT COALESCE(current_setting('app.is_super_admin', true) = 'true', false);
$$;

-- -------------------------------------------------------------------------------------
-- tenants — raíz multi-tenant (excepción §6.1: sin tenant_id). RLS por id propio + SuperAdmin.
-- -------------------------------------------------------------------------------------
CREATE TABLE tenants (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  name          text        NOT NULL,
  nit           text        NOT NULL,
  slug          text        NOT NULL,
  status        text        NOT NULL DEFAULT 'active'
                            CHECK (status IN ('active','inactive','suspended')),
  settings      jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT uq_tenants_nit  UNIQUE (nit),
  CONSTRAINT uq_tenants_slug UNIQUE (slug),
  CONSTRAINT ck_tenants_slug_format CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$')
);
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_self_isolation ON tenants
  USING (id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (identity.is_super_admin());
CREATE TRIGGER tr_tenants_before_update_row_version
  BEFORE UPDATE ON tenants FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_tenants_audit
  AFTER INSERT OR UPDATE OR DELETE ON tenants FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE tenants IS '@context:identity @entity:compañía-tenant Raíz de aislamiento multi-tenant.';
COMMENT ON COLUMN tenants.nit IS '@pii:low Identificación tributaria de la compañía.';

-- -------------------------------------------------------------------------------------
-- users — cuentas (3 perfiles vía rol). Email global único → login unificado (#9370 RF-1.1).
-- -------------------------------------------------------------------------------------
CREATE TABLE users (
  id                   uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id            uuid        NOT NULL,
  email                citext      NOT NULL,
  password_hash        text        NULL,
  password_algo        text        NOT NULL DEFAULT 'argon2id',
  account_state        text        NOT NULL DEFAULT 'inactive'
                       CHECK (account_state IN ('active','inactive','temp_blocked','permanent_blocked')),
  blocked_until        timestamptz NULL,
  block_reason         text        NULL,
  must_change_password boolean     NOT NULL DEFAULT false,
  failed_attempt_count integer     NOT NULL DEFAULT 0,
  last_login_at        timestamptz NULL,
  created_at           timestamptz NOT NULL DEFAULT now(),
  created_by           uuid        NULL,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  updated_by           uuid        NULL,
  deleted_at           timestamptz NULL,
  deleted_by           uuid        NULL,
  row_version          integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_users_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_users_email UNIQUE (email),
  CONSTRAINT ck_users_blocked_until CHECK (
    (account_state = 'temp_blocked' AND blocked_until IS NOT NULL)
    OR (account_state <> 'temp_blocked')
  )
);
CREATE INDEX ix_users_tenant_id ON users (tenant_id);
CREATE INDEX ix_users_tenant_id_account_state ON users (tenant_id, account_state) WHERE deleted_at IS NULL;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON users
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_users_before_update_row_version
  BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_users_audit
  AFTER INSERT OR UPDATE OR DELETE ON users FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN users.email IS '@pii:medium';
COMMENT ON COLUMN users.password_hash IS '@pii:high Nunca en logs. Hash Argon2id/bcrypt.';

-- FKs diferidas de bootstrap (tenants/users.created_by → users)
ALTER TABLE tenants
  ADD CONSTRAINT fk_tenants_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  ADD CONSTRAINT fk_tenants_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL;
ALTER TABLE users
  ADD CONSTRAINT fk_users_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  ADD CONSTRAINT fk_users_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL;

-- Función de autenticación cross-tenant (bypassa RLS): resuelve la cuenta por email para el login.
CREATE OR REPLACE FUNCTION identity.find_user_for_auth(p_email citext)
RETURNS TABLE (id uuid, tenant_id uuid, password_hash text, password_algo text, account_state text, blocked_until timestamptz)
LANGUAGE sql SECURITY DEFINER STABLE
SET search_path = identity, pg_temp
AS $$
  SELECT u.id, u.tenant_id, u.password_hash, u.password_algo, u.account_state, u.blocked_until
  FROM identity.users u
  WHERE u.email = p_email AND u.deleted_at IS NULL;
$$;
COMMENT ON FUNCTION identity.find_user_for_auth(citext) IS
  'Lectura mínima para autenticación previa al contexto de tenant. Solo la usa el flujo de login.';

-- -------------------------------------------------------------------------------------
-- profiles — zona perfil autoservicio (RF-4.3), 1:1 con user
-- -------------------------------------------------------------------------------------
CREATE TABLE profiles (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  user_id       uuid        NOT NULL,
  full_name     text        NOT NULL,
  phone         text        NULL,
  address       text        NULL,
  locale        text        NOT NULL DEFAULT 'es-CO',
  timezone      text        NOT NULL DEFAULT 'America/Bogota',
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_profiles_tenants  FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_profiles_users    FOREIGN KEY (user_id)   REFERENCES users (id)   ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_profiles_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_profiles_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_profiles_user_id UNIQUE (user_id)
);
CREATE INDEX ix_profiles_tenant_id ON profiles (tenant_id);
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON profiles
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_profiles_before_update_row_version
  BEFORE UPDATE ON profiles FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_profiles_audit
  AFTER INSERT OR UPDATE OR DELETE ON profiles FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN profiles.full_name IS '@pii:low';
COMMENT ON COLUMN profiles.phone     IS '@pii:medium';
COMMENT ON COLUMN profiles.address   IS '@pii:high';

-- -------------------------------------------------------------------------------------
-- permissions — slugs dinámicos GLOBALES (modulo.tramites.crud-total). Tenant-exento (catálogo).
-- -------------------------------------------------------------------------------------
CREATE TABLE permissions (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  slug          text        NOT NULL,
  module        text        NOT NULL,
  action        text        NOT NULL,
  description   text        NULL,
  is_assignable boolean     NOT NULL DEFAULT true,
  is_system     boolean     NOT NULL DEFAULT false,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_permissions_slug UNIQUE (slug),
  CONSTRAINT ck_permissions_slug_format CHECK (slug ~ '^[a-z0-9]+(\.[a-z0-9-]+)+$')
);
CREATE TRIGGER tr_permissions_before_update_touch
  BEFORE UPDATE ON permissions FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();
COMMENT ON TABLE permissions IS '@context:identity Catálogo global de permisos-slug (RF-2.1). Definidos por SuperAdmin.';

INSERT INTO permissions (slug, module, action, is_system) VALUES
  ('modulo.tramites.crud-total','tramites','crud-total', true),
  ('modulo.tramites.ver','tramites','ver', true),
  ('modulo.companias.crud-total','companias','crud-total', true),
  ('modulo.ot.crud-total','ot','crud-total', true),
  ('modulo.ot.operador','ot','operador', true),
  ('modulo.ot.superadmin','ot','superadmin', true),
  ('modulo.parametrizacion.crud-total','parametrizacion','crud-total', true),
  ('modulo.dashboard.ver','dashboard','ver', true),
  ('modulo.dashboard.exportar','dashboard','exportar', true);

-- -------------------------------------------------------------------------------------
-- roles — maestros globales (tenant_id NULL) + locales por tenant
-- -------------------------------------------------------------------------------------
CREATE TABLE roles (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NULL,
  slug          text        NOT NULL,
  name          text        NOT NULL,
  scope         text        NOT NULL CHECK (scope IN ('global','tenant')),
  is_system     boolean     NOT NULL DEFAULT false,
  description   text        NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_roles_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_roles_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_roles_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT ck_roles_scope_tenant CHECK (
    (scope = 'global' AND tenant_id IS NULL) OR (scope = 'tenant' AND tenant_id IS NOT NULL)
  )
);
CREATE UNIQUE INDEX uq_roles_global_slug ON roles (slug) WHERE tenant_id IS NULL AND deleted_at IS NULL;
CREATE UNIQUE INDEX uq_roles_tenant_slug ON roles (tenant_id, slug) WHERE tenant_id IS NOT NULL AND deleted_at IS NULL;
CREATE INDEX ix_roles_tenant_id ON roles (tenant_id);
ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_or_global_isolation ON roles
  USING (tenant_id IS NULL OR tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (
    (tenant_id IS NULL AND identity.is_super_admin())
    OR tenant_id = current_setting('app.current_tenant_id', true)::uuid
  );
CREATE TRIGGER tr_roles_before_update_row_version
  BEFORE UPDATE ON roles FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_roles_audit
  AFTER INSERT OR UPDATE OR DELETE ON roles FOR EACH ROW EXECUTE FUNCTION audit.log_change();

INSERT INTO roles (id, tenant_id, slug, name, scope, is_system) VALUES
  ('00000000-0000-7000-8000-0000000000a1', NULL, 'super-admin',  'Super Administrador', 'global', true),
  ('00000000-0000-7000-8000-0000000000a2', NULL, 'tenant-admin', 'Administrador de Compañía', 'global', true),
  ('00000000-0000-7000-8000-0000000000a3', NULL, 'colaborador',  'Colaborador', 'global', true);

-- -------------------------------------------------------------------------------------
-- role_permissions — junction rol↔permiso (global o por tenant, espejo del scope del rol)
-- -------------------------------------------------------------------------------------
CREATE TABLE role_permissions (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NULL,
  role_id       uuid        NOT NULL,
  permission_id uuid        NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_role_permissions_roles FOREIGN KEY (role_id) REFERENCES roles (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_role_permissions_permissions FOREIGN KEY (permission_id) REFERENCES permissions (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_role_permissions_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT uq_role_permissions_role_permission UNIQUE (role_id, permission_id)
);
CREATE INDEX ix_role_permissions_role_id ON role_permissions (role_id);
CREATE INDEX ix_role_permissions_permission_id ON role_permissions (permission_id);
ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_or_global_isolation ON role_permissions
  USING (tenant_id IS NULL OR tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (
    (tenant_id IS NULL AND identity.is_super_admin())
    OR tenant_id = current_setting('app.current_tenant_id', true)::uuid
  );
CREATE TRIGGER tr_role_permissions_before_update_row_version
  BEFORE UPDATE ON role_permissions FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_role_permissions_audit
  AFTER INSERT OR UPDATE OR DELETE ON role_permissions FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- user_roles — asignación usuario↔rol dentro de un tenant (anti escalamiento horizontal)
-- -------------------------------------------------------------------------------------
CREATE TABLE user_roles (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  user_id       uuid        NOT NULL,
  role_id       uuid        NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_user_roles_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_user_roles_users   FOREIGN KEY (user_id)   REFERENCES users (id)   ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_user_roles_roles   FOREIGN KEY (role_id)   REFERENCES roles (id)   ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_user_roles_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_user_roles_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_user_roles_tenant_user_role UNIQUE (tenant_id, user_id, role_id)
);
CREATE INDEX ix_user_roles_tenant_id_user_id ON user_roles (tenant_id, user_id);
CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);
ALTER TABLE user_roles ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON user_roles
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_user_roles_before_update_row_version
  BEFORE UPDATE ON user_roles FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_user_roles_audit
  AFTER INSERT OR UPDATE OR DELETE ON user_roles FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- onboarding_invitations — alta sin contraseña; enlace firmado 24h, un solo uso (RF-4.1)
-- -------------------------------------------------------------------------------------
CREATE TABLE onboarding_invitations (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  email         citext      NOT NULL,
  invited_role_id uuid      NOT NULL,
  token_hash    text        NOT NULL,
  signature     text        NOT NULL,
  status        text        NOT NULL DEFAULT 'pending'
                            CHECK (status IN ('pending','consumed','expired','revoked')),
  expires_at    timestamptz NOT NULL,
  consumed_at   timestamptz NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_onboarding_invitations_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_onboarding_invitations_roles   FOREIGN KEY (invited_role_id) REFERENCES roles (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_onboarding_invitations_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_onboarding_invitations_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_onboarding_invitations_token UNIQUE (token_hash)
);
CREATE UNIQUE INDEX uq_onboarding_invitations_tenant_email_pending
  ON onboarding_invitations (tenant_id, email) WHERE status = 'pending';
CREATE INDEX ix_onboarding_invitations_tenant_id ON onboarding_invitations (tenant_id);
ALTER TABLE onboarding_invitations ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON onboarding_invitations
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_onboarding_invitations_before_update_row_version
  BEFORE UPDATE ON onboarding_invitations FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_onboarding_invitations_audit
  AFTER INSERT OR UPDATE OR DELETE ON onboarding_invitations FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN onboarding_invitations.token_hash IS '@pii:high Solo hash del token; nunca el token en claro.';

-- -------------------------------------------------------------------------------------
-- password_reset_tokens — restablecimiento credenciales (un solo uso)
-- -------------------------------------------------------------------------------------
CREATE TABLE password_reset_tokens (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  user_id       uuid        NOT NULL,
  token_hash    text        NOT NULL,
  expires_at    timestamptz NOT NULL,
  consumed_at   timestamptz NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_password_reset_tokens_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_password_reset_tokens_users   FOREIGN KEY (user_id)   REFERENCES users (id)   ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_password_reset_tokens_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_password_reset_tokens_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_password_reset_tokens_token UNIQUE (token_hash)
);
CREATE INDEX ix_password_reset_tokens_tenant_id_user_id ON password_reset_tokens (tenant_id, user_id);
ALTER TABLE password_reset_tokens ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON password_reset_tokens
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_password_reset_tokens_before_update_row_version
  BEFORE UPDATE ON password_reset_tokens FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
COMMENT ON COLUMN password_reset_tokens.token_hash IS '@pii:high';

-- -------------------------------------------------------------------------------------
-- refresh_tokens — sesiones (tokens HttpOnly/refresh, RF seguridad CF-H2)
-- -------------------------------------------------------------------------------------
CREATE TABLE refresh_tokens (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  user_id       uuid        NOT NULL,
  token_hash    text        NOT NULL,
  user_agent    text        NULL,
  ip_address    inet        NULL,
  expires_at    timestamptz NOT NULL,
  revoked_at    timestamptz NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_refresh_tokens_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_refresh_tokens_users   FOREIGN KEY (user_id)   REFERENCES users (id)   ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_refresh_tokens_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_refresh_tokens_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_refresh_tokens_token UNIQUE (token_hash)
);
CREATE INDEX ix_refresh_tokens_tenant_id_user_id ON refresh_tokens (tenant_id, user_id) WHERE revoked_at IS NULL;
ALTER TABLE refresh_tokens ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON refresh_tokens
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_refresh_tokens_before_update_row_version
  BEFORE UPDATE ON refresh_tokens FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
COMMENT ON COLUMN refresh_tokens.token_hash IS '@pii:high';

-- -------------------------------------------------------------------------------------
-- password_policies — política de contraseñas por tenant (RF-4.2)
-- -------------------------------------------------------------------------------------
CREATE TABLE password_policies (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  min_length        integer     NOT NULL DEFAULT 12 CHECK (min_length >= 8),
  require_uppercase boolean     NOT NULL DEFAULT true,
  require_lowercase boolean     NOT NULL DEFAULT true,
  require_digit     boolean     NOT NULL DEFAULT true,
  require_symbol    boolean     NOT NULL DEFAULT true,
  history_count     integer     NOT NULL DEFAULT 5,
  max_age_days      integer     NOT NULL DEFAULT 90,
  lockout_threshold integer     NOT NULL DEFAULT 5,
  lockout_minutes   integer     NOT NULL DEFAULT 15,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_password_policies_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_password_policies_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_password_policies_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_password_policies_tenant UNIQUE (tenant_id)
);
ALTER TABLE password_policies ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON password_policies
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_password_policies_before_update_row_version
  BEFORE UPDATE ON password_policies FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_password_policies_audit
  AFTER INSERT OR UPDATE OR DELETE ON password_policies FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- login_attempts — auditoría de intentos y bloqueos (CF-B9). Append-only; sin RLS (audit infra).
-- -------------------------------------------------------------------------------------
CREATE TABLE login_attempts (
  id              uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id       uuid        NULL,
  user_id         uuid        NULL,
  email_attempted citext      NOT NULL,
  succeeded       boolean     NOT NULL,
  failure_reason  text        NULL CHECK (failure_reason IN
                    ('invalid_credentials','inactive','temp_blocked','permanent_blocked','rate_limited','unknown_user')),
  ip_address      inet        NULL,
  user_agent      text        NULL,
  attempted_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_login_attempts_email_attempted_at ON login_attempts (email_attempted, attempted_at DESC);
CREATE INDEX ix_login_attempts_tenant_id_attempted_at ON login_attempts (tenant_id, attempted_at DESC);
COMMENT ON TABLE login_attempts IS '@context:identity Bitácora de seguridad (append-only). Particionar por mes en producción.';
COMMENT ON COLUMN login_attempts.email_attempted IS '@pii:medium';

-- -------------------------------------------------------------------------------------
-- support_tickets — soporte con contexto del colaborador (RF-4.4)
-- -------------------------------------------------------------------------------------
CREATE TABLE support_tickets (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id           uuid        NOT NULL,
  reporter_user_id    uuid        NOT NULL,
  assigned_to_user_id uuid        NULL,
  subject             text        NOT NULL,
  body                text        NOT NULL,
  category            text        NOT NULL DEFAULT 'general',
  status              text        NOT NULL DEFAULT 'open'
                                  CHECK (status IN ('open','in_progress','resolved','closed')),
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid        NOT NULL,
  updated_at          timestamptz NOT NULL DEFAULT now(),
  updated_by          uuid        NOT NULL,
  deleted_at          timestamptz NULL,
  deleted_by          uuid        NULL,
  row_version         integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_support_tickets_tenants FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_support_tickets_users_reporter FOREIGN KEY (reporter_user_id) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_support_tickets_users_assignee FOREIGN KEY (assigned_to_user_id) REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_support_tickets_users_creator FOREIGN KEY (created_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_support_tickets_users_updater FOREIGN KEY (updated_by) REFERENCES users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_support_tickets_tenant_id_status ON support_tickets (tenant_id, status) WHERE deleted_at IS NULL;
ALTER TABLE support_tickets ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON support_tickets
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_support_tickets_before_update_row_version
  BEFORE UPDATE ON support_tickets FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_support_tickets_audit
  AFTER INSERT OR UPDATE OR DELETE ON support_tickets FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- Bootstrap: tenant de plataforma + usuario de sistema (para created_by de config global y seeds)
-- -------------------------------------------------------------------------------------
INSERT INTO tenants (id, name, nit, slug, status, created_by)
VALUES ('00000000-0000-7000-8000-000000000000', 'FLIT Platform', '000000000', 'flit-platform', 'active', NULL);

INSERT INTO users (id, tenant_id, email, password_hash, account_state, created_by)
VALUES ('00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000000',
        'system@flit.co', NULL, 'active', NULL);

INSERT INTO user_roles (tenant_id, user_id, role_id, created_by, updated_by)
VALUES ('00000000-0000-7000-8000-000000000000', '00000000-0000-7000-8000-000000000001',
        '00000000-0000-7000-8000-0000000000a1', '00000000-0000-7000-8000-000000000001',
        '00000000-0000-7000-8000-000000000001');

-- Seeds role_permissions (slugs efectivos post-login — IDN-02 / #9415)
INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, '00000000-0000-7000-8000-0000000000a1', p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM permissions p;

INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, '00000000-0000-7000-8000-0000000000a2', p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM permissions p
WHERE p.slug IN (
  'modulo.tramites.crud-total', 'modulo.tramites.ver',
  'modulo.companias.crud-total', 'modulo.parametrizacion.crud-total',
  'modulo.dashboard.ver', 'modulo.dashboard.exportar',
  'modulo.ot.operador'
);

INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, '00000000-0000-7000-8000-0000000000a3', p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM permissions p
WHERE p.slug IN ('modulo.tramites.ver', 'modulo.dashboard.ver');

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS identity.support_tickets, identity.login_attempts, identity.password_policies,
--   identity.refresh_tokens, identity.password_reset_tokens, identity.onboarding_invitations,
--   identity.user_roles, identity.role_permissions, identity.roles, identity.permissions,
--   identity.profiles, identity.users, identity.tenants CASCADE;
-- DROP FUNCTION IF EXISTS identity.find_user_for_auth(citext);
-- DROP FUNCTION IF EXISTS identity.is_super_admin();


-- >>> 10-catalogs.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 10 — Catálogos (schema catalogs)
-- Sin tenant_id, sin soft-delete. Lifecycle por is_active. code UNIQUE. external_refs jsonb.
-- Seeds: canónicos colombianos (DIVIPOLA, Registraduría, RUNT/CNT). Los volúmenes completos
-- (≈1.122 municipios, marcas/líneas RUNT) los carga un ETL (infra-agent / database-agent D).
-- =====================================================================================
SET search_path TO catalogs, public;

-- -------------------------------------------------------------------------------------
-- document_types (Registraduría) — default_person_kind habilita el enrutamiento FR-2 (#9409)
-- -------------------------------------------------------------------------------------
CREATE TABLE document_types (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code                text        NOT NULL,
  name                text        NOT NULL,
  default_person_kind text        NULL CHECK (default_person_kind IN ('natural','juridica')),
  is_active           boolean     NOT NULL DEFAULT true,
  display_order       integer     NOT NULL DEFAULT 0,
  external_refs       jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at          timestamptz NOT NULL DEFAULT now(),
  updated_at          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_document_types_code UNIQUE (code)
);
CREATE TRIGGER tr_document_types_before_update_touch
  BEFORE UPDATE ON document_types FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();
COMMENT ON TABLE catalogs.document_types IS '@context:catalogs Tipos de documento de identidad (CC, NIT, ...).';

INSERT INTO document_types (code, name, default_person_kind, display_order) VALUES
  ('CC',   'Cédula de ciudadanía',          'natural',  1),
  ('CE',   'Cédula de extranjería',         'natural',  2),
  ('TI',   'Tarjeta de identidad',          'natural',  3),
  ('RC',   'Registro civil',                'natural',  4),
  ('PA',   'Pasaporte',                     'natural',  5),
  ('PEP',  'Permiso especial de permanencia','natural', 6),
  ('NUIP', 'Número único de identificación personal','natural', 7),
  ('NIT',  'Número de identificación tributaria','juridica', 8);

-- -------------------------------------------------------------------------------------
-- divipola_departments (DANE) — 32 departamentos + Bogotá D.C.
-- -------------------------------------------------------------------------------------
CREATE TABLE divipola_departments (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          char(2)     NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_divipola_departments_code UNIQUE (code)
);
CREATE TRIGGER tr_divipola_departments_before_update_touch
  BEFORE UPDATE ON divipola_departments FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO divipola_departments (code, name) VALUES
  ('05','Antioquia'),('08','Atlántico'),('11','Bogotá D.C.'),('13','Bolívar'),('15','Boyacá'),
  ('17','Caldas'),('18','Caquetá'),('19','Cauca'),('20','Cesar'),('23','Córdoba'),
  ('25','Cundinamarca'),('27','Chocó'),('41','Huila'),('44','La Guajira'),('47','Magdalena'),
  ('50','Meta'),('52','Nariño'),('54','Norte de Santander'),('63','Quindío'),('66','Risaralda'),
  ('68','Santander'),('70','Sucre'),('73','Tolima'),('76','Valle del Cauca'),('81','Arauca'),
  ('85','Casanare'),('86','Putumayo'),('88','San Andrés y Providencia'),('91','Amazonas'),
  ('94','Guainía'),('95','Guaviare'),('97','Vaupés'),('99','Vichada');

-- -------------------------------------------------------------------------------------
-- divipola_municipalities (DANE) — code de 5 dígitos; FK a department. Muestra representativa.
-- -------------------------------------------------------------------------------------
CREATE TABLE divipola_municipalities (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  department_id uuid        NOT NULL,
  code          char(5)     NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_divipola_municipalities_code UNIQUE (code),
  CONSTRAINT fk_divipola_municipalities_departments
    FOREIGN KEY (department_id) REFERENCES divipola_departments (id)
    ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_divipola_municipalities_department_id ON divipola_municipalities (department_id);
CREATE TRIGGER tr_divipola_municipalities_before_update_touch
  BEFORE UPDATE ON divipola_municipalities FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO divipola_municipalities (department_id, code, name)
SELECT d.id, m.code, m.name
FROM (VALUES
  ('11','11001','Bogotá D.C.'),
  ('05','05001','Medellín'),
  ('76','76001','Cali'),
  ('08','08001','Barranquilla'),
  ('13','13001','Cartagena'),
  ('68','68001','Bucaramanga'),
  ('54','54001','Cúcuta'),
  ('52','52001','Pasto'),
  ('66','66001','Pereira'),
  ('17','17001','Manizales')
) AS m(dep_code, code, name)
JOIN divipola_departments d ON d.code = m.dep_code;

-- -------------------------------------------------------------------------------------
-- vehicle_classes (CNT Art. 2)
-- -------------------------------------------------------------------------------------
CREATE TABLE vehicle_classes (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_vehicle_classes_code UNIQUE (code)
);
CREATE TRIGGER tr_vehicle_classes_before_update_touch
  BEFORE UPDATE ON vehicle_classes FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO vehicle_classes (code, name, display_order) VALUES
  ('AUTOMOVIL','Automóvil',1),('CAMIONETA','Camioneta',2),('CAMPERO','Campero',3),
  ('MOTOCICLETA','Motocicleta',4),('MOTOCARRO','Motocarro',5),('CUATRIMOTO','Cuatrimoto',6),
  ('BUS','Bus',7),('BUSETA','Buseta',8),('MICROBUS','Microbús',9),('CAMION','Camión',10),
  ('VOLQUETA','Volqueta',11),('TRACTOCAMION','Tractocamión',12),('MAQUINARIA','Maquinaria',13),
  ('REMOLQUE','Remolque',14),('SEMIREMOLQUE','Semirremolque',15);

-- -------------------------------------------------------------------------------------
-- service_classes (CNT Art. 2)
-- -------------------------------------------------------------------------------------
CREATE TABLE service_classes (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_service_classes_code UNIQUE (code)
);
CREATE TRIGGER tr_service_classes_before_update_touch
  BEFORE UPDATE ON service_classes FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO service_classes (code, name, display_order) VALUES
  ('PARTICULAR','Particular',1),('PUBLICO','Público',2),('OFICIAL','Oficial',3),
  ('DIPLOMATICO','Diplomático',4);

-- -------------------------------------------------------------------------------------
-- fuel_types (RUNT)
-- -------------------------------------------------------------------------------------
CREATE TABLE fuel_types (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_fuel_types_code UNIQUE (code)
);
CREATE TRIGGER tr_fuel_types_before_update_touch
  BEFORE UPDATE ON fuel_types FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO fuel_types (code, name, display_order) VALUES
  ('GASOLINA','Gasolina',1),('DIESEL','Diésel',2),('GNV','Gas natural vehicular',3),
  ('ELECTRICO','Eléctrico',4),('HIBRIDO','Híbrido',5),('HIDROGENO','Hidrógeno',6);

-- -------------------------------------------------------------------------------------
-- body_types (RUNT)
-- -------------------------------------------------------------------------------------
CREATE TABLE body_types (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_body_types_code UNIQUE (code)
);
CREATE TRIGGER tr_body_types_before_update_touch
  BEFORE UPDATE ON body_types FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO body_types (code, name, display_order) VALUES
  ('SEDAN','Sedán',1),('HATCHBACK','Hatchback',2),('SUV','SUV',3),('COUPE','Coupé',4),
  ('PICKUP','Pick-up',5),('PANEL','Panel',6),('ESTACAS','Estacas',7),('FURGON','Furgón',8);

-- -------------------------------------------------------------------------------------
-- colors (RUNT)
-- -------------------------------------------------------------------------------------
CREATE TABLE colors (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_colors_code UNIQUE (code)
);
CREATE TRIGGER tr_colors_before_update_touch
  BEFORE UPDATE ON colors FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO colors (code, name, display_order) VALUES
  ('BLANCO','Blanco',1),('NEGRO','Negro',2),('GRIS','Gris',3),('PLATA','Plata',4),
  ('ROJO','Rojo',5),('AZUL','Azul',6),('VERDE','Verde',7),('AMARILLO','Amarillo',8),
  ('NARANJA','Naranja',9),('VINOTINTO','Vinotinto',10);

-- -------------------------------------------------------------------------------------
-- vehicle_makes (RUNT). Muestra (incluye TESLA para escenarios de reglas #9410).
-- -------------------------------------------------------------------------------------
CREATE TABLE vehicle_makes (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_vehicle_makes_code UNIQUE (code)
);
CREATE TRIGGER tr_vehicle_makes_before_update_touch
  BEFORE UPDATE ON vehicle_makes FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO vehicle_makes (code, name, display_order) VALUES
  ('CHEVROLET','Chevrolet',1),('RENAULT','Renault',2),('MAZDA','Mazda',3),('TOYOTA','Toyota',4),
  ('KIA','Kia',5),('NISSAN','Nissan',6),('FORD','Ford',7),('VOLKSWAGEN','Volkswagen',8),
  ('BMW','BMW',9),('MERCEDES','Mercedes-Benz',10),('TESLA','Tesla',11),('YAMAHA','Yamaha',12),
  ('HONDA','Honda',13),('BAJAJ','Bajaj',14),('AKT','AKT',15),('SUZUKI','Suzuki',16);

-- -------------------------------------------------------------------------------------
-- vehicle_lines (RUNT) — FK a make. UNIQUE por (make_id, code).
-- -------------------------------------------------------------------------------------
CREATE TABLE vehicle_lines (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  make_id       uuid        NOT NULL,
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  external_refs jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_vehicle_lines_make_id_code UNIQUE (make_id, code),
  CONSTRAINT fk_vehicle_lines_vehicle_makes
    FOREIGN KEY (make_id) REFERENCES vehicle_makes (id)
    ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_vehicle_lines_make_id ON vehicle_lines (make_id);
CREATE TRIGGER tr_vehicle_lines_before_update_touch
  BEFORE UPDATE ON vehicle_lines FOR EACH ROW EXECUTE FUNCTION audit.touch_updated_at();

INSERT INTO vehicle_lines (make_id, code, name)
SELECT mk.id, l.code, l.name
FROM (VALUES
  ('CHEVROLET','SPARK','Spark'),
  ('CHEVROLET','ONIX','Onix'),
  ('RENAULT','LOGAN','Logan'),
  ('RENAULT','SANDERO','Sandero'),
  ('MAZDA','CX30','CX-30'),
  ('TESLA','MODEL3','Model 3'),
  ('YAMAHA','FZ','FZ'),
  ('BAJAJ','BOXER','Boxer')
) AS l(make_code, code, name)
JOIN vehicle_makes mk ON mk.code = l.make_code;

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS catalogs.vehicle_lines, catalogs.vehicle_makes, catalogs.colors,
--   catalogs.body_types, catalogs.fuel_types, catalogs.service_classes, catalogs.vehicle_classes,
--   catalogs.divipola_municipalities, catalogs.divipola_departments, catalogs.document_types CASCADE;


-- >>> 25-files.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 25 — Archivos (schema files). Substrato binario MinIO (ADR-0006) a convención.
-- Referenciado por file_id explícito desde companies/procedures/identity_verification (sin FK polimórfica).
-- =====================================================================================
SET search_path TO files, public;

CREATE TABLE files (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  bucket            text        NOT NULL,
  object_key        text        NOT NULL,
  original_filename text        NULL,
  content_type      text        NOT NULL,
  size_bytes        bigint      NOT NULL DEFAULT 0 CHECK (size_bytes >= 0),
  sha256            char(64)    NULL,
  status            text        NOT NULL DEFAULT 'pending'
                                CHECK (status IN ('pending','ready','deleted')),
  metadata          jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_files_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_files_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_files_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_files_bucket_object_key UNIQUE (bucket, object_key)
);
CREATE INDEX ix_files_tenant_id ON files (tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX ix_files_tenant_id_status ON files (tenant_id, status);
ALTER TABLE files ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON files
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_files_before_update_row_version
  BEFORE UPDATE ON files FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_files_audit
  AFTER INSERT OR UPDATE OR DELETE ON files FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE files IS '@context:files Metadatos de objetos MinIO. El binario vive en object storage; aquí solo referencia.';
COMMENT ON COLUMN files.object_key IS 'Llave del objeto en el bucket (no exponer en URLs públicas; usar URLs prefirmadas).';

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS files.files CASCADE;


-- >>> 30-companies.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 30 — Administrador de Compañías B2B (#9381) + Escrituras (#9383)
-- Compañía = 1:1 con tenant. Config modular hot-reload por module_key (jsonb).
--
-- Inventario (schema companies) — HU de consumo en ADO:
-- | Tabla                      | Feature   | HU migración / uso principal        |
-- |----------------------------|-----------|-------------------------------------|
-- | companies                  | #9381     | #9444 crear · #9445–#9449 leer    |
-- | company_module_configs     | #9381     | #9444 crear · #9446/#9447/#9449   |
-- | signature_wallets          | #9381     | #9444 crear · #9449 leer          |
-- | signature_wallet_movements | #9381     | #9444 crear · runtime firma trámite |
-- | vehicle_ownership_rules    | #9381     | #9444 crear · #9448 leer/escribir |
-- | escrituras                 | #9383     | #9450 crear · #9451/#9452 CRUD    |
-- | escritura_attachments      | #9383     | #9450 crear · #9451 adjuntos PDF  |
-- integrations.runt_sync_log → ddl/70-integrations.sql (#9447)
-- =====================================================================================
SET search_path TO companies, public;

-- -------------------------------------------------------------------------------------
-- companies — maestro de compañía (1:1 con identity.tenants)
-- -------------------------------------------------------------------------------------
CREATE TABLE companies (
  id              uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id       uuid        NOT NULL,
  nit             text        NOT NULL,
  legal_name      text        NOT NULL,
  commercial_name text        NULL,
  modules_enabled jsonb       NOT NULL DEFAULT '{}'::jsonb,  -- {"escrituras": true, "traspasos": true}
  created_at      timestamptz NOT NULL DEFAULT now(),
  created_by      uuid        NOT NULL,
  updated_at      timestamptz NOT NULL DEFAULT now(),
  updated_by      uuid        NOT NULL,
  deleted_at      timestamptz NULL,
  deleted_by      uuid        NULL,
  row_version     integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_companies_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_companies_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_companies_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_companies_tenant UNIQUE (tenant_id),
  CONSTRAINT uq_companies_nit UNIQUE (nit)
);
CREATE INDEX ix_companies_tenant_id ON companies (tenant_id);
CREATE INDEX ix_companies_nit_trgm ON companies USING gin (nit gin_trgm_ops);          -- filtro por NIT (#9381)
CREATE INDEX ix_companies_legal_name_trgm ON companies USING gin (legal_name gin_trgm_ops); -- filtro por Nombre
ALTER TABLE companies ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON companies
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_companies_before_update_row_version
  BEFORE UPDATE ON companies FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_companies_audit
  AFTER INSERT OR UPDATE OR DELETE ON companies FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN companies.modules_enabled IS '@semi-structured Flags de módulos activos (controla visibilidad de pestañas como Escrituras #9383).';

-- -------------------------------------------------------------------------------------
-- company_module_configs — config modular hot-reload, 1 fila por módulo (CF hot-reload #9381)
-- -------------------------------------------------------------------------------------
CREATE TABLE company_module_configs (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  module_key    text        NOT NULL
                            CHECK (module_key IN ('registration','transfers','company','runt_contingency')),
  config        jsonb       NOT NULL DEFAULT '{}'::jsonb,
  is_active     boolean     NOT NULL DEFAULT true,
  version       integer     NOT NULL DEFAULT 1,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_company_module_configs_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_company_module_configs_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_company_module_configs_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_company_module_configs_tenant_module UNIQUE (tenant_id, module_key)
);
CREATE INDEX ix_company_module_configs_tenant_id_module ON company_module_configs (tenant_id, module_key);
ALTER TABLE company_module_configs ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON company_module_configs
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_company_module_configs_before_update_row_version
  BEFORE UPDATE ON company_module_configs FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_company_module_configs_audit
  AFTER INSERT OR UPDATE OR DELETE ON company_module_configs FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE company_module_configs IS
  '@context:companies Config por módulo: registration | transfers | company (firmas/correos/notif/pagos) | runt_contingency (Verifik/Intempo failover).';

-- -------------------------------------------------------------------------------------
-- signature_wallets — "bal de firmas" (#9381 Configuración Empresa)
-- -------------------------------------------------------------------------------------
CREATE TABLE signature_wallets (
  id             uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id      uuid        NOT NULL,
  balance        integer     NOT NULL DEFAULT 0 CHECK (balance >= 0),
  low_threshold  integer     NOT NULL DEFAULT 0,
  auto_recharge  boolean     NOT NULL DEFAULT false,
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     uuid        NOT NULL,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid        NOT NULL,
  deleted_at     timestamptz NULL,
  deleted_by     uuid        NULL,
  row_version    integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_signature_wallets_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_signature_wallets_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_signature_wallets_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_signature_wallets_tenant UNIQUE (tenant_id)
);
ALTER TABLE signature_wallets ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON signature_wallets
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_signature_wallets_before_update_row_version
  BEFORE UPDATE ON signature_wallets FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_signature_wallets_audit
  AFTER INSERT OR UPDATE OR DELETE ON signature_wallets FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- signature_wallet_movements — ledger de consumo/recarga de firmas (append-only)
-- procedure_instance_id es referencia suave (sin FK; procedures es de mayor jerarquía).
-- -------------------------------------------------------------------------------------
CREATE TABLE signature_wallet_movements (
  id                   uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id            uuid        NOT NULL,
  wallet_id            uuid        NOT NULL,
  delta                integer     NOT NULL,
  reason               text        NOT NULL,
  balance_after        integer     NOT NULL CHECK (balance_after >= 0),
  procedure_instance_id uuid       NULL,  -- soft ref a procedures.procedure_instances
  created_at           timestamptz NOT NULL DEFAULT now(),
  created_by           uuid        NOT NULL,
  CONSTRAINT fk_signature_wallet_movements_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_signature_wallet_movements_wallets FOREIGN KEY (wallet_id) REFERENCES signature_wallets (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_signature_wallet_movements_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_signature_wallet_movements_tenant_id_wallet_id ON signature_wallet_movements (tenant_id, wallet_id, created_at DESC);
ALTER TABLE signature_wallet_movements ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON signature_wallet_movements
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
COMMENT ON TABLE signature_wallet_movements IS '@context:companies Ledger append-only del bal de firmas.';

-- -------------------------------------------------------------------------------------
-- vehicle_ownership_rules — interceptor de propiedad vehicular (#9381). Reutiliza árbol JSONB.
-- -------------------------------------------------------------------------------------
CREATE TABLE vehicle_ownership_rules (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  name          text        NOT NULL,
  rule_type     text        NOT NULL CHECK (rule_type IN ('allow','block','warn','require_exception')),
  condition     jsonb       NOT NULL DEFAULT '{}'::jsonb,
  priority      integer     NOT NULL DEFAULT 100,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_vehicle_ownership_rules_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_vehicle_ownership_rules_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_vehicle_ownership_rules_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_vehicle_ownership_rules_tenant_id_active ON vehicle_ownership_rules (tenant_id, is_active) WHERE deleted_at IS NULL;
ALTER TABLE vehicle_ownership_rules ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vehicle_ownership_rules
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_vehicle_ownership_rules_before_update_row_version
  BEFORE UPDATE ON vehicle_ownership_rules FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_vehicle_ownership_rules_audit
  AFTER INSERT OR UPDATE OR DELETE ON vehicle_ownership_rules FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- escrituras — repositorio legal por compañía (#9383). Visible solo si módulo activo.
-- -------------------------------------------------------------------------------------
CREATE TABLE escrituras (
  id               uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id        uuid        NOT NULL,
  document_type_id uuid        NOT NULL,
  deed_number      text        NOT NULL,
  company_name     text        NOT NULL,
  expiration_date  date        NOT NULL,
  notes            text        NULL,
  created_at       timestamptz NOT NULL DEFAULT now(),
  created_by       uuid        NOT NULL,
  updated_at       timestamptz NOT NULL DEFAULT now(),
  updated_by       uuid        NOT NULL,
  deleted_at       timestamptz NULL,
  deleted_by       uuid        NULL,
  row_version      integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_escrituras_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_escrituras_document_types FOREIGN KEY (document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_escrituras_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_escrituras_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_escrituras_tenant_deed_number UNIQUE (tenant_id, deed_number)
);
CREATE INDEX ix_escrituras_tenant_id ON escrituras (tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX ix_escrituras_document_type_id ON escrituras (document_type_id);
CREATE INDEX ix_escrituras_tenant_id_expiration_date ON escrituras (tenant_id, expiration_date);
ALTER TABLE escrituras ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON escrituras
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_escrituras_before_update_row_version
  BEFORE UPDATE ON escrituras FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_escrituras_audit
  AFTER INSERT OR UPDATE OR DELETE ON escrituras FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- escritura_attachments — PDF adjuntos (solo PDF, ≤3MB, máx 5; reemplazo total al actualizar)
-- -------------------------------------------------------------------------------------
CREATE TABLE escritura_attachments (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  escritura_id  uuid        NOT NULL,
  file_id       uuid        NOT NULL,
  position      integer     NOT NULL CHECK (position BETWEEN 1 AND 5),
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_escritura_attachments_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_escritura_attachments_escrituras FOREIGN KEY (escritura_id) REFERENCES escrituras (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_escritura_attachments_files FOREIGN KEY (file_id) REFERENCES files.files (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_escritura_attachments_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_escritura_attachments_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_escritura_attachments_escritura_position UNIQUE (escritura_id, position)
);
CREATE INDEX ix_escritura_attachments_tenant_id_escritura_id ON escritura_attachments (tenant_id, escritura_id);
CREATE INDEX ix_escritura_attachments_file_id ON escritura_attachments (file_id);
ALTER TABLE escritura_attachments ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON escritura_attachments
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_escritura_attachments_before_update_row_version
  BEFORE UPDATE ON escritura_attachments FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
COMMENT ON TABLE escritura_attachments IS
  '@context:companies Validación de negocio (solo PDF, ≤3MB, máx 5) en la capa de aplicación; el límite de 5 lo refuerza el CHECK de position.';

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS companies.escritura_attachments, companies.escrituras,
--   companies.vehicle_ownership_rules, companies.signature_wallet_movements,
--   companies.signature_wallets, companies.company_module_configs, companies.companies CASCADE;


-- >>> 40-ot.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 40 — Organismos de Tránsito (#9378) + Orden del Consolidado (#9379)
-- OT = referencia cross-tenant (SIN tenant_id, ver ADR-0012). Las tablas de CONFIG OT se
-- aíslan por traffic_agency_id (GUC app.current_agency_id) + bypass SuperAdmin.
-- =====================================================================================
SET search_path TO ot, public;

-- -------------------------------------------------------------------------------------
-- traffic_agencies — registro OT (estilo catálogo: is_active, sin tenant_id, sin RLS).
-- Semilla completa (~370) se adapta de services/.../SeedData/traffic_secretaries.sql.
-- -------------------------------------------------------------------------------------
CREATE TABLE traffic_agencies (
  id                            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code                          text        NOT NULL,
  name                          text        NOT NULL,
  agency_type                   text        NOT NULL DEFAULT 'Organismos de Tránsito',
  address                       text        NULL,
  phone                         text        NULL,
  department_name               text        NULL,
  municipality_name             text        NULL,
  dane_municipality_code        char(5)     NULL,   -- DIVIPOLA (FK opcional tras carga completa)
  nit                           text        NULL,
  notifier_email                text        NULL,
  contact_name                  text        NULL,
  contact_phone                 text        NULL,
  runt_agency_code              text        NULL,   -- traffic_agency_code RUNT (no único: '0' repetido)
  mandate_document_applies      boolean     NOT NULL DEFAULT false,
  virtual_process_applies       boolean     NOT NULL DEFAULT false,
  requires_peace_and_safe       boolean     NOT NULL DEFAULT false,
  allows_runt_approval_queries  boolean     NOT NULL DEFAULT false,
  requires_preassignment_plate  boolean     NOT NULL DEFAULT false,
  request_issue_date_flag       boolean     NOT NULL DEFAULT false,
  external_refs                 jsonb       NOT NULL DEFAULT '{}'::jsonb,  -- {"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"..."}
  is_active                     boolean     NOT NULL DEFAULT true,
  created_at                    timestamptz NOT NULL DEFAULT now(),
  created_by                    uuid        NULL,
  updated_at                    timestamptz NOT NULL DEFAULT now(),
  updated_by                    uuid        NULL,
  row_version                   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_traffic_agencies_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_traffic_agencies_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_traffic_agencies_code UNIQUE (code)
);
CREATE INDEX ix_traffic_agencies_name_trgm ON traffic_agencies USING gin (name gin_trgm_ops);
CREATE INDEX ix_traffic_agencies_dane_municipality_code ON traffic_agencies (dane_municipality_code);
CREATE INDEX ix_traffic_agencies_is_active ON traffic_agencies (is_active);
CREATE TRIGGER tr_traffic_agencies_before_update_row_version
  BEFORE UPDATE ON traffic_agencies FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_traffic_agencies_audit
  AFTER INSERT OR UPDATE OR DELETE ON traffic_agencies FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE traffic_agencies IS
  '@context:ot Registro de Organismos de Tránsito (referencia cross-tenant). Sin tenant_id ni RLS (ADR-0012).';
COMMENT ON COLUMN traffic_agencies.notifier_email IS '@pii:medium';

INSERT INTO traffic_agencies (code, name, department_name, municipality_name, dane_municipality_code, nit,
  notifier_email, runt_agency_code, mandate_document_applies, virtual_process_applies, requires_preassignment_plate,
  requires_peace_and_safe, allows_runt_approval_queries, external_refs, created_by) VALUES
  ('OT-BUCARAMANGA','DIR TTOyTTE BUCARAMANGA','SANTANDER','BUCARAMANGA','68001','890201222',
   'notificaciones@bucaramanga.gov.co','68001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"68"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-BARBOSA','DIR TTEyTTO MCPAL BARBOSA','ANTIOQUIA','BARBOSA','05079','890980445',
   'asistenteadmsatt@gmail.com','5079000', true,true,true,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"05"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-CARTAGENA','DPTO ADTVO TTOyTTE DIST CARTAGENA','BOLIVAR','CARTAGENA','13001','890480184',
   'notificacionesjudicialesadministrativo@cartagena.gov.co','13001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"13"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-PASTO','DPTO ADTVO TTOYTTE MCPAL PASTO','NARIÑO','PASTO','52001','8912800003',
   'contactenos@pasto.gov.co','52001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1}'::jsonb,'00000000-0000-7000-8000-000000000001');

-- -------------------------------------------------------------------------------------
-- ot_users — operadores de la consola OT (#9378 vistas base: usuarios y permisos)
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_users (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  email             citext      NOT NULL,
  full_name         text        NOT NULL,
  external_auth_ref text        NULL,
  is_active         boolean     NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_users_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_users_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_users_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_ot_users_agency_email UNIQUE (traffic_agency_id, email)
);
CREATE INDEX ix_ot_users_traffic_agency_id ON ot_users (traffic_agency_id) WHERE deleted_at IS NULL;
ALTER TABLE ot_users ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_users
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_users_before_update_row_version
  BEFORE UPDATE ON ot_users FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_ot_users_audit
  AFTER INSERT OR UPDATE OR DELETE ON ot_users FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN ot_users.email IS '@pii:medium';

-- -------------------------------------------------------------------------------------
-- ot_user_permissions — permisos operativos por usuario OT (traffic_agency_id denormalizado p/ RLS)
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_user_permissions (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  ot_user_id        uuid        NOT NULL,
  permission_slug   text        NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_user_permissions_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_user_permissions_ot_users FOREIGN KEY (ot_user_id) REFERENCES ot_users (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_user_permissions_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_user_permissions_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_ot_user_permissions_user_slug UNIQUE (ot_user_id, permission_slug)
);
CREATE INDEX ix_ot_user_permissions_traffic_agency_id ON ot_user_permissions (traffic_agency_id, ot_user_id);
ALTER TABLE ot_user_permissions ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_user_permissions
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_user_permissions_before_update_row_version
  BEFORE UPDATE ON ot_user_permissions FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

-- -------------------------------------------------------------------------------------
-- ot_rules — constructor de reglas OT (#9378 parametrización dinámica). Árbol JSONB validado.
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_rules (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  name              text        NOT NULL,
  trigger_event     text        NOT NULL,   -- gatillo de activación (ej. on_submit, on_field_change)
  condition_tree    jsonb       NOT NULL DEFAULT '{}'::jsonb,
  actions           jsonb       NOT NULL DEFAULT '[]'::jsonb,   -- bloqueo, validación adicional, enrutamiento QX
  priority          integer     NOT NULL DEFAULT 100,
  is_active         boolean     NOT NULL DEFAULT true,
  valid_from        timestamptz NULL,
  valid_until       timestamptz NULL,
  schema_version    integer     NOT NULL DEFAULT 1,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_rules_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_rules_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_rules_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT ck_ot_rules_condition_valid CHECK (public.is_valid_rule_condition(condition_tree)),
  CONSTRAINT ck_ot_rules_actions_valid   CHECK (public.is_valid_rule_actions(actions))
);
CREATE INDEX ix_ot_rules_traffic_agency_id_active ON ot_rules (traffic_agency_id, is_active) WHERE deleted_at IS NULL;
ALTER TABLE ot_rules ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_rules
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_rules_before_update_row_version
  BEFORE UPDATE ON ot_rules FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_ot_rules_audit
  AFTER INSERT OR UPDATE OR DELETE ON ot_rules FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- ot_consolidated_doc_orders — orden del expediente consolidado por OT (#9379). 1 activo por OT.
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_consolidated_doc_orders (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  version           integer     NOT NULL DEFAULT 1,
  is_active         boolean     NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_consolidated_doc_orders_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_consolidated_doc_orders_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_consolidated_doc_orders_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE UNIQUE INDEX uq_ot_consolidated_doc_orders_active ON ot_consolidated_doc_orders (traffic_agency_id) WHERE is_active AND deleted_at IS NULL;
ALTER TABLE ot_consolidated_doc_orders ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_consolidated_doc_orders
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_consolidated_doc_orders_before_update_row_version
  BEFORE UPDATE ON ot_consolidated_doc_orders FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_ot_consolidated_doc_orders_audit
  AFTER INSERT OR UPDATE OR DELETE ON ot_consolidated_doc_orders FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- ot_consolidated_doc_order_items — ítems ordenados (drag&drop). Quitar = borrar fila (no archivos).
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_consolidated_doc_order_items (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  order_id          uuid        NOT NULL,
  document_type_id  uuid        NULL,           -- tipo global (catalogs)
  custom_label      text        NULL,           -- etiqueta personalizada
  position          integer     NOT NULL,
  source            text        NOT NULL CHECK (source IN ('global','custom')),
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_cons_items_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_cons_items_orders FOREIGN KEY (order_id) REFERENCES ot_consolidated_doc_orders (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_cons_items_document_types FOREIGN KEY (document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_cons_items_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_cons_items_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_ot_cons_items_order_position UNIQUE (order_id, position),
  CONSTRAINT ck_ot_cons_items_source CHECK (
    (source = 'global' AND document_type_id IS NOT NULL)
    OR (source = 'custom' AND custom_label IS NOT NULL)
  )
);
CREATE INDEX ix_ot_cons_items_order_id ON ot_consolidated_doc_order_items (order_id, position);
CREATE INDEX ix_ot_cons_items_document_type_id ON ot_consolidated_doc_order_items (document_type_id);
ALTER TABLE ot_consolidated_doc_order_items ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_consolidated_doc_order_items
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_cons_items_before_update_row_version
  BEFORE UPDATE ON ot_consolidated_doc_order_items FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
COMMENT ON TABLE ot_consolidated_doc_order_items IS
  '@context:ot Orden del consolidado (#9379). Guardado <500ms: UPDATE de position en transacción corta; índice uq por (order_id, position).';

-- -------------------------------------------------------------------------------------
-- ot_qx_integrations — modo de gestión por OT: Dashboard FLIT vs QX/colas externas (Quipux)
-- -------------------------------------------------------------------------------------
CREATE TABLE ot_qx_integrations (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id uuid        NOT NULL,
  mode              text        NOT NULL DEFAULT 'dashboard' CHECK (mode IN ('dashboard','qx')),
  callback_url      text        NULL,
  auth_config       jsonb       NOT NULL DEFAULT '{}'::jsonb,  -- referencia a secreto, no plaintext
  is_active         boolean     NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ot_qx_integrations_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ot_qx_integrations_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ot_qx_integrations_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_ot_qx_integrations_agency UNIQUE (traffic_agency_id)
);
ALTER TABLE ot_qx_integrations ENABLE ROW LEVEL SECURITY;
CREATE POLICY agency_isolation ON ot_qx_integrations
  USING (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (traffic_agency_id = current_setting('app.current_agency_id', true)::uuid OR identity.is_super_admin());
CREATE TRIGGER tr_ot_qx_integrations_before_update_row_version
  BEFORE UPDATE ON ot_qx_integrations FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_ot_qx_integrations_audit
  AFTER INSERT OR UPDATE OR DELETE ON ot_qx_integrations FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS ot.ot_qx_integrations, ot.ot_consolidated_doc_order_items,
--   ot.ot_consolidated_doc_orders, ot.ot_rules, ot.ot_user_permissions, ot.ot_users,
--   ot.traffic_agencies CASCADE;

