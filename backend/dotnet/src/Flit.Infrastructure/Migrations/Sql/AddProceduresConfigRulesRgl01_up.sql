-- HU #9437 RGL-01 — procedures_config.rules + endpoint_catalog + validadores JSONB (ADR-0011)
-- Prerrequisitos: shell FLIT (identity.users vía InitialFlitShell o 20-identity.sql).
-- Fuente parcial: docs/designs/tramites-2.0/ddl/50-procedures_config.sql

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE OR REPLACE FUNCTION public.uuidv7()
RETURNS uuid
LANGUAGE sql
VOLATILE
AS $$
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

CREATE SCHEMA IF NOT EXISTS procedures_config;
CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE IF NOT EXISTS audit.audit_log (
  id            uuid         PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid         NOT NULL,
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

CREATE OR REPLACE FUNCTION audit.increment_row_version()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
  NEW.row_version := OLD.row_version + 1;
  NEW.updated_at  := now();
  RETURN NEW;
END;
$$;

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

CREATE OR REPLACE FUNCTION identity.is_super_admin()
RETURNS boolean LANGUAGE sql STABLE AS $$
  SELECT COALESCE(current_setting('app.is_super_admin', true) = 'true', false);
$$;

CREATE TABLE IF NOT EXISTS identity.tenants (
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

CREATE OR REPLACE FUNCTION public.is_valid_rule_condition(node jsonb)
RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
  child jsonb;
  op    text;
BEGIN
  IF node IS NULL OR node = 'null'::jsonb THEN
    RETURN true;
  END IF;
  IF jsonb_typeof(node) <> 'object' THEN RETURN false; END IF;

  IF node ? 'op' THEN
    op := node->>'op';
    IF op NOT IN ('AND','OR') THEN RETURN false; END IF;
    IF jsonb_typeof(node->'children') <> 'array' THEN RETURN false; END IF;
    FOR child IN SELECT * FROM jsonb_array_elements(node->'children') LOOP
      IF NOT public.is_valid_rule_condition(child) THEN RETURN false; END IF;
    END LOOP;
    RETURN true;
  END IF;

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

CREATE TABLE IF NOT EXISTS procedures_config.procedure_families (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  display_order integer     NOT NULL DEFAULT 0,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT uq_procedure_families_code UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS procedures_config.procedure_types (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  family_id     uuid        NOT NULL,
  code          text        NOT NULL,
  slug          text        NOT NULL,
  name          text        NOT NULL,
  description   text        NULL,
  max_steps     integer     NOT NULL DEFAULT 4 CHECK (max_steps BETWEEN 1 AND 4),
  display_order integer     NOT NULL DEFAULT 0,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_types_families FOREIGN KEY (family_id)
    REFERENCES procedures_config.procedure_families (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_procedure_types_code UNIQUE (code),
  CONSTRAINT uq_procedure_types_slug UNIQUE (slug)
);

CREATE TABLE IF NOT EXISTS procedures_config.endpoint_catalog (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  code          text        NOT NULL,
  name          text        NOT NULL,
  url           text        NOT NULL,
  method        text        NOT NULL DEFAULT 'GET' CHECK (method IN ('GET','POST')),
  auth_type     text        NOT NULL DEFAULT 'none' CHECK (auth_type IN ('none','api_key','bearer','basic')),
  auth_config   jsonb       NOT NULL DEFAULT '{}'::jsonb,
  timeout_ms    integer     NOT NULL DEFAULT 5000,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_endpoint_catalog_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_endpoint_catalog_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_endpoint_catalog_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_endpoint_catalog_tenant_code UNIQUE (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS procedures_config.rules (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  procedure_type_id uuid        NOT NULL,
  name              text        NOT NULL,
  description       text        NULL,
  condition_tree    jsonb       NOT NULL DEFAULT '{}'::jsonb,
  actions           jsonb       NOT NULL DEFAULT '[]'::jsonb,
  priority          integer     NOT NULL DEFAULT 100,
  is_active         boolean     NOT NULL DEFAULT true,
  schema_version    integer     NOT NULL DEFAULT 1,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_rules_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_rules_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_rules_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_rules_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_rules_tenant_type_name UNIQUE (tenant_id, procedure_type_id, name),
  CONSTRAINT ck_rules_condition_valid CHECK (public.is_valid_rule_condition(condition_tree)),
  CONSTRAINT ck_rules_actions_valid   CHECK (public.is_valid_rule_actions(actions))
);

CREATE INDEX IF NOT EXISTS ix_endpoint_catalog_tenant_id
  ON procedures_config.endpoint_catalog (tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_rules_tenant_id_type
  ON procedures_config.rules (tenant_id, procedure_type_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_rules_tenant_id_active_priority
  ON procedures_config.rules (tenant_id, is_active, priority);

ALTER TABLE procedures_config.endpoint_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE procedures_config.rules ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON procedures_config.endpoint_catalog;
CREATE POLICY tenant_isolation ON procedures_config.endpoint_catalog
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

DROP POLICY IF EXISTS tenant_isolation ON procedures_config.rules;
CREATE POLICY tenant_isolation ON procedures_config.rules
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

DROP TRIGGER IF EXISTS tr_endpoint_catalog_before_update_row_version ON procedures_config.endpoint_catalog;
CREATE TRIGGER tr_endpoint_catalog_before_update_row_version
  BEFORE UPDATE ON procedures_config.endpoint_catalog
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

DROP TRIGGER IF EXISTS tr_endpoint_catalog_audit ON procedures_config.endpoint_catalog;
CREATE TRIGGER tr_endpoint_catalog_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.endpoint_catalog
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

DROP TRIGGER IF EXISTS tr_rules_before_update_row_version ON procedures_config.rules;
CREATE TRIGGER tr_rules_before_update_row_version
  BEFORE UPDATE ON procedures_config.rules
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

DROP TRIGGER IF EXISTS tr_rules_audit ON procedures_config.rules;
CREATE TRIGGER tr_rules_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.rules
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

DO $$
DECLARE
  v_sys    uuid := '00000000-0000-7000-8000-000000000001';
  v_tenant uuid := '00000000-0000-7000-8001-000000000010';
  v_family uuid := '00000000-0000-7000-8001-000000000001';
  v_type   uuid := '00000000-0000-7000-8002-000000000001';
  v_is_tramites20 boolean;
BEGIN
  SELECT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'identity' AND table_name = 'users' AND column_name = 'tenant_id'
  ) INTO v_is_tramites20;

  IF NOT v_is_tramites20 THEN
    INSERT INTO identity.users
      (id, cognito_sub, email, full_name, document_type, document_number, phone, status, mfa_enabled, created_at, updated_at)
    VALUES
      (v_sys, 'stub-system@flit.platform', 'system@flit.platform', 'FLIT Platform System', 'CC', '0000000000', NULL, 'ACTIVE', FALSE, now(), now())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.tenants (id, name, nit, slug, created_by, updated_by)
    VALUES (v_tenant, 'FLIT Demo Tenant', '900000000-1', 'flit-demo', v_sys, v_sys)
    ON CONFLICT (slug) DO NOTHING;
  ELSIF NOT EXISTS (SELECT 1 FROM identity.tenants WHERE id = v_tenant) THEN
    INSERT INTO identity.tenants (id, name, nit, slug, status, created_by, updated_by)
    VALUES (v_tenant, 'FLIT Demo Tenant', '900000000-1', 'flit-demo', 'active', v_sys, v_sys)
    ON CONFLICT (slug) DO NOTHING;
  END IF;

  INSERT INTO procedures_config.procedure_families (id, code, name, display_order, created_by, updated_by)
  VALUES (v_family, 'TRASPASO', 'Traspaso', 1, v_sys, v_sys)
  ON CONFLICT (code) DO NOTHING;

  INSERT INTO procedures_config.procedure_types (id, family_id, code, slug, name, display_order, created_by, updated_by)
  VALUES (v_type, v_family, 'TRA_ESTANDAR', 'traspaso-estandar', 'Traspaso Estandar', 1, v_sys, v_sys)
  ON CONFLICT (code) DO NOTHING;
END $$;
