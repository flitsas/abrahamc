-- HU #9408 #9409 #9410 — Núcleo de parametrización procedures_config (DDL 50)
-- Complementa AddProceduresConfigRulesRgl01 (#9437: rules + endpoint_catalog).
-- Fuente: docs/designs/tramites-2.0/ddl/50-procedures_config.sql

SET search_path TO procedures_config, public, identity, audit, catalogs, files, ot;

-- -------------------------------------------------------------------------------------
-- Prerrequisitos mínimos (stubs idempotentes hasta migraciones DDL 10/25/40)
-- -------------------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS catalogs;
CREATE TABLE IF NOT EXISTS catalogs.document_types (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  display_order integer     NOT NULL DEFAULT 0,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_document_types_code UNIQUE (code)
);

CREATE SCHEMA IF NOT EXISTS files;
CREATE TABLE IF NOT EXISTS files.files (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  bucket            text        NOT NULL,
  object_key        text        NOT NULL,
  content_type      text        NOT NULL DEFAULT 'application/octet-stream',
  size_bytes        bigint      NOT NULL DEFAULT 0 CHECK (size_bytes >= 0),
  status            text        NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','ready','deleted')),
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_files_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_files_bucket_object_key UNIQUE (bucket, object_key)
);

CREATE SCHEMA IF NOT EXISTS ot;
CREATE TABLE IF NOT EXISTS ot.traffic_agencies (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT uq_traffic_agencies_code UNIQUE (code)
);

-- -------------------------------------------------------------------------------------
-- A. Maestros globales — endurecer familias/tipos + tablas nuevas
-- -------------------------------------------------------------------------------------
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_procedure_families_users_creator') THEN
    ALTER TABLE procedures_config.procedure_families
      ADD CONSTRAINT fk_procedure_families_users_creator FOREIGN KEY (created_by)
        REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_procedure_families_users_updater') THEN
    ALTER TABLE procedures_config.procedure_families
      ADD CONSTRAINT fk_procedure_families_users_updater FOREIGN KEY (updated_by)
        REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_procedure_types_users_creator') THEN
    ALTER TABLE procedures_config.procedure_types
      ADD CONSTRAINT fk_procedure_types_users_creator FOREIGN KEY (created_by)
        REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_procedure_types_users_updater') THEN
    ALTER TABLE procedures_config.procedure_types
      ADD CONSTRAINT fk_procedure_types_users_updater FOREIGN KEY (updated_by)
        REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL;
  END IF;
END $$;

ALTER TABLE procedures_config.procedure_families ENABLE ROW LEVEL SECURITY;
ALTER TABLE procedures_config.procedure_types ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS config_read ON procedures_config.procedure_families;
CREATE POLICY config_read ON procedures_config.procedure_families FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.procedure_families;
CREATE POLICY config_modify ON procedures_config.procedure_families
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());

DROP POLICY IF EXISTS config_read ON procedures_config.procedure_types;
CREATE POLICY config_read ON procedures_config.procedure_types FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.procedure_types;
CREATE POLICY config_modify ON procedures_config.procedure_types
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());

DROP TRIGGER IF EXISTS tr_procedure_families_before_update_row_version ON procedures_config.procedure_families;
CREATE TRIGGER tr_procedure_families_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_families
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_procedure_families_audit ON procedures_config.procedure_families;
CREATE TRIGGER tr_procedure_families_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_families
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

DROP TRIGGER IF EXISTS tr_procedure_types_before_update_row_version ON procedures_config.procedure_types;
CREATE TRIGGER tr_procedure_types_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_types
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_procedure_types_audit ON procedures_config.procedure_types;
CREATE TRIGGER tr_procedure_types_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_types
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

COMMENT ON TABLE procedures_config.procedure_families IS
  '@context:procedures_config Familias padre de trámites (global, SuperAdmin).';

CREATE INDEX IF NOT EXISTS ix_procedure_types_family_id ON procedures_config.procedure_types (family_id);
CREATE INDEX IF NOT EXISTS ix_procedure_types_is_active ON procedures_config.procedure_types (is_active);

CREATE TABLE IF NOT EXISTS procedures_config.edges (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL CHECK (code IN ('vehiculo','propietario','comprador','locatario')),
  name          text        NOT NULL,
  edge_kind     text        NOT NULL CHECK (edge_kind IN ('vehicle','person')),
  display_order integer     NOT NULL DEFAULT 0,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_edges_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_edges_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_edges_code UNIQUE (code)
);
ALTER TABLE procedures_config.edges ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.edges;
CREATE POLICY config_read ON procedures_config.edges FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.edges;
CREATE POLICY config_modify ON procedures_config.edges
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_edges_before_update_row_version ON procedures_config.edges;
CREATE TRIGGER tr_edges_before_update_row_version
  BEFORE UPDATE ON procedures_config.edges FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_edges_audit ON procedures_config.edges;
CREATE TRIGGER tr_edges_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.edges FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.procedure_type_edges (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  procedure_type_id uuid        NOT NULL,
  edge_id           uuid        NOT NULL,
  is_active         boolean     NOT NULL DEFAULT true,
  is_required       boolean     NOT NULL DEFAULT true,
  display_order     integer     NOT NULL DEFAULT 0,
  role_label        text        NULL,
  config            jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_type_edges_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_type_edges_edges FOREIGN KEY (edge_id)
    REFERENCES procedures_config.edges (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_type_edges_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_type_edges_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_procedure_type_edges_type_edge UNIQUE (procedure_type_id, edge_id)
);
CREATE INDEX IF NOT EXISTS ix_procedure_type_edges_procedure_type_id
  ON procedures_config.procedure_type_edges (procedure_type_id);
CREATE INDEX IF NOT EXISTS ix_procedure_type_edges_edge_id
  ON procedures_config.procedure_type_edges (edge_id);
ALTER TABLE procedures_config.procedure_type_edges ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.procedure_type_edges;
CREATE POLICY config_read ON procedures_config.procedure_type_edges FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.procedure_type_edges;
CREATE POLICY config_modify ON procedures_config.procedure_type_edges
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_procedure_type_edges_before_update_row_version ON procedures_config.procedure_type_edges;
CREATE TRIGGER tr_procedure_type_edges_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_type_edges FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_procedure_type_edges_audit ON procedures_config.procedure_type_edges;
CREATE TRIGGER tr_procedure_type_edges_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_type_edges FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE procedures_config.procedure_type_edges IS
  '@context:procedures_config MATRIZ trámite×arista. La fila ausente = arista no aplica.';

CREATE TABLE IF NOT EXISTS procedures_config.form_sections (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  procedure_type_id uuid        NOT NULL,
  edge_id           uuid        NULL,
  section_key       text        NOT NULL,
  title             text        NOT NULL,
  display_order     integer     NOT NULL DEFAULT 0,
  ui_mode           text        NOT NULL DEFAULT 'interactive' CHECK (ui_mode IN ('read_only','interactive')),
  is_active         boolean     NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_form_sections_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_form_sections_matrix FOREIGN KEY (procedure_type_id, edge_id)
    REFERENCES procedures_config.procedure_type_edges (procedure_type_id, edge_id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_form_sections_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_form_sections_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_form_sections_type_key UNIQUE (procedure_type_id, section_key)
);
CREATE INDEX IF NOT EXISTS ix_form_sections_procedure_type_id ON procedures_config.form_sections (procedure_type_id);
CREATE INDEX IF NOT EXISTS ix_form_sections_procedure_type_id_edge_id
  ON procedures_config.form_sections (procedure_type_id, edge_id);
ALTER TABLE procedures_config.form_sections ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.form_sections;
CREATE POLICY config_read ON procedures_config.form_sections FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.form_sections;
CREATE POLICY config_modify ON procedures_config.form_sections
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_form_sections_before_update_row_version ON procedures_config.form_sections;
CREATE TRIGGER tr_form_sections_before_update_row_version
  BEFORE UPDATE ON procedures_config.form_sections FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_form_sections_audit ON procedures_config.form_sections;
CREATE TRIGGER tr_form_sections_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.form_sections FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.form_fields (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  section_id    uuid        NOT NULL,
  field_key     text        NOT NULL,
  data_type     text        NOT NULL CHECK (data_type IN ('text','number','date','boolean','select','multiselect','file')),
  label         text        NOT NULL,
  is_required   boolean     NOT NULL DEFAULT false,
  display_order integer     NOT NULL DEFAULT 0,
  ui_state      text        NOT NULL DEFAULT 'lleno' CHECK (ui_state IN ('vacio','cargando','error','lleno')),
  is_trigger    boolean     NOT NULL DEFAULT false,
  validation    jsonb       NOT NULL DEFAULT '{}'::jsonb,
  options       jsonb       NOT NULL DEFAULT '[]'::jsonb,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_form_fields_sections FOREIGN KEY (section_id)
    REFERENCES procedures_config.form_sections (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_form_fields_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_form_fields_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_form_fields_section_key UNIQUE (section_id, field_key)
);
CREATE INDEX IF NOT EXISTS ix_form_fields_section_id ON procedures_config.form_fields (section_id);
ALTER TABLE procedures_config.form_fields ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.form_fields;
CREATE POLICY config_read ON procedures_config.form_fields FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.form_fields;
CREATE POLICY config_modify ON procedures_config.form_fields
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_form_fields_before_update_row_version ON procedures_config.form_fields;
CREATE TRIGGER tr_form_fields_before_update_row_version
  BEFORE UPDATE ON procedures_config.form_fields FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_form_fields_audit ON procedures_config.form_fields;
CREATE TRIGGER tr_form_fields_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.form_fields FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.query_connectors (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code          text        NOT NULL,
  name          text        NOT NULL,
  base_config   jsonb       NOT NULL DEFAULT '{}'::jsonb,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_query_connectors_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_query_connectors_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_query_connectors_code UNIQUE (code)
);
ALTER TABLE procedures_config.query_connectors ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.query_connectors;
CREATE POLICY config_read ON procedures_config.query_connectors FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.query_connectors;
CREATE POLICY config_modify ON procedures_config.query_connectors
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_query_connectors_before_update_row_version ON procedures_config.query_connectors;
CREATE TRIGGER tr_query_connectors_before_update_row_version
  BEFORE UPDATE ON procedures_config.query_connectors FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_query_connectors_audit ON procedures_config.query_connectors;
CREATE TRIGGER tr_query_connectors_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.query_connectors FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.procedure_type_query_configs (
  id                 uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  procedure_type_id  uuid        NOT NULL,
  edge_id            uuid        NULL,
  query_connector_id uuid        NOT NULL,
  is_mandatory       boolean     NOT NULL DEFAULT false,
  is_omitible        boolean     NOT NULL DEFAULT false,
  person_kind_filter text        NOT NULL DEFAULT 'any' CHECK (person_kind_filter IN ('natural','juridica','any')),
  run_condition      jsonb       NOT NULL DEFAULT '{}'::jsonb,
  display_order      integer     NOT NULL DEFAULT 0,
  is_active          boolean     NOT NULL DEFAULT true,
  created_at         timestamptz NOT NULL DEFAULT now(),
  created_by         uuid        NULL,
  updated_at         timestamptz NOT NULL DEFAULT now(),
  updated_by         uuid        NULL,
  row_version        integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_pt_query_configs_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_pt_query_configs_matrix FOREIGN KEY (procedure_type_id, edge_id)
    REFERENCES procedures_config.procedure_type_edges (procedure_type_id, edge_id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_pt_query_configs_connectors FOREIGN KEY (query_connector_id)
    REFERENCES procedures_config.query_connectors (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_pt_query_configs_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_pt_query_configs_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_pt_query_configs UNIQUE (procedure_type_id, edge_id, query_connector_id, person_kind_filter)
);
CREATE INDEX IF NOT EXISTS ix_pt_query_configs_procedure_type_id
  ON procedures_config.procedure_type_query_configs (procedure_type_id);
CREATE INDEX IF NOT EXISTS ix_pt_query_configs_connector_id
  ON procedures_config.procedure_type_query_configs (query_connector_id);
ALTER TABLE procedures_config.procedure_type_query_configs ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.procedure_type_query_configs;
CREATE POLICY config_read ON procedures_config.procedure_type_query_configs FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.procedure_type_query_configs;
CREATE POLICY config_modify ON procedures_config.procedure_type_query_configs
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_pt_query_configs_before_update_row_version ON procedures_config.procedure_type_query_configs;
CREATE TRIGGER tr_pt_query_configs_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_type_query_configs FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_pt_query_configs_audit ON procedures_config.procedure_type_query_configs;
CREATE TRIGGER tr_pt_query_configs_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_type_query_configs FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.document_templates (
  id               uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code             text        NOT NULL,
  name             text        NOT NULL,
  document_type_id uuid        NOT NULL,
  scope            text        NOT NULL DEFAULT 'global' CHECK (scope IN ('global','tenant')),
  tenant_id        uuid        NULL,
  current_version  integer     NOT NULL DEFAULT 0,
  is_active        boolean     NOT NULL DEFAULT true,
  created_at       timestamptz NOT NULL DEFAULT now(),
  created_by       uuid        NULL,
  updated_at       timestamptz NOT NULL DEFAULT now(),
  updated_by       uuid        NULL,
  row_version      integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_document_templates_document_types FOREIGN KEY (document_type_id)
    REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_document_templates_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_document_templates_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_document_templates_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_document_templates_code UNIQUE (code),
  CONSTRAINT ck_document_templates_scope_tenant CHECK (
    (scope = 'global' AND tenant_id IS NULL) OR (scope = 'tenant' AND tenant_id IS NOT NULL)
  )
);
CREATE INDEX IF NOT EXISTS ix_document_templates_document_type_id ON procedures_config.document_templates (document_type_id);
CREATE INDEX IF NOT EXISTS ix_document_templates_tenant_id ON procedures_config.document_templates (tenant_id);
ALTER TABLE procedures_config.document_templates ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.document_templates;
CREATE POLICY config_read ON procedures_config.document_templates FOR SELECT
  USING (tenant_id IS NULL OR tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin());
DROP POLICY IF EXISTS config_modify ON procedures_config.document_templates;
CREATE POLICY config_modify ON procedures_config.document_templates FOR ALL
  USING (identity.is_super_admin() OR tenant_id = current_setting('app.current_tenant_id', true)::uuid)
  WITH CHECK (identity.is_super_admin() OR tenant_id = current_setting('app.current_tenant_id', true)::uuid);
DROP TRIGGER IF EXISTS tr_document_templates_before_update_row_version ON procedures_config.document_templates;
CREATE TRIGGER tr_document_templates_before_update_row_version
  BEFORE UPDATE ON procedures_config.document_templates FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_document_templates_audit ON procedures_config.document_templates;
CREATE TRIGGER tr_document_templates_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.document_templates FOR EACH ROW EXECUTE FUNCTION audit.log_change();

CREATE TABLE IF NOT EXISTS procedures_config.document_template_versions (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  template_id   uuid        NOT NULL,
  version       integer     NOT NULL,
  body_file_id  uuid        NULL,
  body_inline   text        NULL,
  marker_map    jsonb       NOT NULL DEFAULT '{}'::jsonb,
  is_current    boolean     NOT NULL DEFAULT false,
  published_at  timestamptz NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_document_template_versions_templates FOREIGN KEY (template_id)
    REFERENCES procedures_config.document_templates (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_document_template_versions_files FOREIGN KEY (body_file_id)
    REFERENCES files.files (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_document_template_versions_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_document_template_versions_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_document_template_versions_template_version UNIQUE (template_id, version),
  CONSTRAINT ck_document_template_versions_body CHECK (body_file_id IS NOT NULL OR body_inline IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS ix_document_template_versions_template_id
  ON procedures_config.document_template_versions (template_id);
ALTER TABLE procedures_config.document_template_versions ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.document_template_versions;
CREATE POLICY config_read ON procedures_config.document_template_versions FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.document_template_versions;
CREATE POLICY config_modify ON procedures_config.document_template_versions
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_document_template_versions_before_update_row_version ON procedures_config.document_template_versions;
CREATE TRIGGER tr_document_template_versions_before_update_row_version
  BEFORE UPDATE ON procedures_config.document_template_versions FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_document_template_versions_audit ON procedures_config.document_template_versions;
CREATE TRIGGER tr_document_template_versions_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.document_template_versions FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE procedures_config.document_template_versions IS
  '@context:procedures_config Versión inmutable una vez publicada (radicado conserva su versión).';

CREATE TABLE IF NOT EXISTS procedures_config.required_documents (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  procedure_type_id uuid        NOT NULL,
  edge_id           uuid        NULL,
  document_type_id  uuid        NOT NULL,
  kind              text        NOT NULL CHECK (kind IN ('upload','auto_generated')),
  is_required       boolean     NOT NULL DEFAULT true,
  display_order     integer     NOT NULL DEFAULT 0,
  template_id       uuid        NULL,
  actor_role        text        NULL,
  allowed_formats   jsonb       NOT NULL DEFAULT '["pdf"]'::jsonb,
  max_size_mb       integer     NULL,
  is_active         boolean     NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_required_documents_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_required_documents_matrix FOREIGN KEY (procedure_type_id, edge_id)
    REFERENCES procedures_config.procedure_type_edges (procedure_type_id, edge_id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_required_documents_document_types FOREIGN KEY (document_type_id)
    REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_required_documents_templates FOREIGN KEY (template_id)
    REFERENCES procedures_config.document_templates (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_required_documents_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_required_documents_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT ck_required_documents_template_when_auto CHECK (kind <> 'auto_generated' OR template_id IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS ix_required_documents_procedure_type_id ON procedures_config.required_documents (procedure_type_id);
CREATE INDEX IF NOT EXISTS ix_required_documents_document_type_id ON procedures_config.required_documents (document_type_id);
CREATE INDEX IF NOT EXISTS ix_required_documents_template_id ON procedures_config.required_documents (template_id);
ALTER TABLE procedures_config.required_documents ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.required_documents;
CREATE POLICY config_read ON procedures_config.required_documents FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.required_documents;
CREATE POLICY config_modify ON procedures_config.required_documents
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());
DROP TRIGGER IF EXISTS tr_required_documents_before_update_row_version ON procedures_config.required_documents;
CREATE TRIGGER tr_required_documents_before_update_row_version
  BEFORE UPDATE ON procedures_config.required_documents FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_required_documents_audit ON procedures_config.required_documents;
CREATE TRIGGER tr_required_documents_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.required_documents FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- B. Capas tenant-scoped (activaciones; rules/endpoint_catalog ya en RGL-01)
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS procedures_config.procedure_type_activations (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  procedure_type_id uuid        NOT NULL,
  traffic_agency_id uuid        NULL,
  is_active         boolean     NOT NULL DEFAULT true,
  overrides         jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NOT NULL,
  updated_at        timestamptz NOT NULL DEFAULT now(),
  updated_by        uuid        NOT NULL,
  deleted_at        timestamptz NULL,
  deleted_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_pt_activations_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_pt_activations_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_pt_activations_traffic_agencies FOREIGN KEY (traffic_agency_id)
    REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_pt_activations_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_pt_activations_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_pt_activations_type_tenant_agency
  ON procedures_config.procedure_type_activations (procedure_type_id, tenant_id, COALESCE(traffic_agency_id, '00000000-0000-7000-8000-000000000000'))
  WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_pt_activations_tenant_id ON procedures_config.procedure_type_activations (tenant_id);
CREATE INDEX IF NOT EXISTS ix_pt_activations_traffic_agency_id ON procedures_config.procedure_type_activations (traffic_agency_id);
ALTER TABLE procedures_config.procedure_type_activations ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON procedures_config.procedure_type_activations;
CREATE POLICY tenant_isolation ON procedures_config.procedure_type_activations
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
DROP TRIGGER IF EXISTS tr_pt_activations_before_update_row_version ON procedures_config.procedure_type_activations;
CREATE TRIGGER tr_pt_activations_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_type_activations FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
DROP TRIGGER IF EXISTS tr_pt_activations_audit ON procedures_config.procedure_type_activations;
CREATE TRIGGER tr_pt_activations_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_type_activations FOR EACH ROW EXECUTE FUNCTION audit.log_change();

COMMENT ON COLUMN procedures_config.endpoint_catalog.auth_config IS
  '@pii:none Solo referencia a secreto (clave de vault). Nunca credenciales en claro.';

-- -------------------------------------------------------------------------------------
-- C. SEED — Familias (3), Tipos (13), Aristas (4), Matriz Tabla 2.3, Conectores (6)
-- -------------------------------------------------------------------------------------
DO $$
DECLARE v_sys uuid := '00000000-0000-7000-8000-000000000001';
BEGIN
  -- Sin IDs fijos en familias: RGL-01 puede haber creado TRASPASO en ...000001.
  INSERT INTO procedures_config.procedure_families (code, name, display_order, created_by, updated_by) VALUES
    ('MATRICULAS','Matrículas',1,v_sys,v_sys),
    ('TRASPASO','Traspaso',2,v_sys,v_sys),
    ('OTROS_TRAMITES','Otros trámites',3,v_sys,v_sys)
  ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  INSERT INTO procedures_config.edges (id, code, name, edge_kind, display_order, created_by, updated_by) VALUES
    ('00000000-0000-7000-8002-000000000001','vehiculo','Vehículo','vehicle',1,v_sys,v_sys),
    ('00000000-0000-7000-8002-000000000002','propietario','Propietario / Vendedor','person',2,v_sys,v_sys),
    ('00000000-0000-7000-8002-000000000003','comprador','Comprador','person',3,v_sys,v_sys),
    ('00000000-0000-7000-8002-000000000004','locatario','Locatario','person',4,v_sys,v_sys)
  ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    edge_kind = EXCLUDED.edge_kind,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  INSERT INTO procedures_config.procedure_types (family_id, code, slug, name, display_order, created_by, updated_by)
  SELECT f.id, v.code, v.slug, v.name, v.display_order, v_sys, v_sys
  FROM (VALUES
    ('MATRICULAS','MAT_ESTANDAR','matricula-estandar','Matrícula Estándar',1),
    ('MATRICULAS','MAT_LEASING','matricula-leasing','Matrícula Leasing',2),
    ('TRASPASO','TRA_ESTANDAR','traspaso-estandar','Traspaso Estándar',3),
    ('TRASPASO','TRA_UNILATERAL','traspaso-unilateral','Traspaso Unilateral',4),
    ('TRASPASO','TRA_DOMINIO','transferencia-dominio','Transferencia de Dominio',5),
    ('OTROS_TRAMITES','BLINDAJE','blindaje','Blindaje',6),
    ('OTROS_TRAMITES','CAMBIO_CARROCERIA','cambio-carroceria','Cambio de Carrocería',7),
    ('OTROS_TRAMITES','CAMBIO_COLOR','cambio-color','Cambio de Color',8),
    ('OTROS_TRAMITES','CAMBIO_COMBUSTIBLE','cambio-combustible','Cambio de Combustible',9),
    ('OTROS_TRAMITES','INSCRIPCION_PRENDA','inscripcion-prenda','Inscripción de Prenda',10),
    ('OTROS_TRAMITES','LEVANTAMIENTO_PRENDA','levantamiento-prenda','Levantamiento de Prenda',11),
    ('OTROS_TRAMITES','RADICADO_CUENTA','radicado-cuenta','Radicado de Cuenta',12),
    ('OTROS_TRAMITES','TRASLADO_CUENTA','traslado-cuenta','Traslado de Cuenta',13)
  ) AS v(family_code, code, slug, name, display_order)
  JOIN procedures_config.procedure_families f ON f.code = v.family_code
  ON CONFLICT (code) DO UPDATE SET
    family_id = EXCLUDED.family_id,
    slug = EXCLUDED.slug,
    name = EXCLUDED.name,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  INSERT INTO procedures_config.procedure_type_edges (procedure_type_id, edge_id, is_required, display_order, role_label, created_by, updated_by)
  SELECT t.id, e.id, m.is_required, m.display_order, m.role_label, v_sys, v_sys
  FROM (VALUES
    ('MAT_ESTANDAR','vehiculo',true,1,'Vehículo'),
    ('MAT_ESTANDAR','propietario',true,2,'Propietario'),
    ('MAT_LEASING','vehiculo',true,1,'Vehículo'),
    ('MAT_LEASING','propietario',true,2,'Propietario'),
    ('MAT_LEASING','locatario',true,3,'Locatario'),
    ('TRA_ESTANDAR','vehiculo',true,1,'Vehículo'),
    ('TRA_ESTANDAR','propietario',true,2,'Vendedor'),
    ('TRA_ESTANDAR','comprador',true,3,'Comprador'),
    ('TRA_UNILATERAL','vehiculo',true,1,'Vehículo'),
    ('TRA_UNILATERAL','propietario',true,2,'Vendedor'),
    ('TRA_UNILATERAL','locatario',true,3,'Locatario'),
    ('TRA_DOMINIO','vehiculo',true,1,'Vehículo'),
    ('TRA_DOMINIO','propietario',true,2,'Vendedor'),
    ('TRA_DOMINIO','comprador',true,3,'Comprador'),
    ('BLINDAJE','vehiculo',true,1,'Vehículo'),
    ('BLINDAJE','propietario',true,2,'Propietario'),
    ('CAMBIO_CARROCERIA','vehiculo',true,1,'Vehículo'),
    ('CAMBIO_CARROCERIA','propietario',true,2,'Propietario'),
    ('CAMBIO_COLOR','vehiculo',true,1,'Vehículo'),
    ('CAMBIO_COLOR','propietario',true,2,'Propietario'),
    ('CAMBIO_COMBUSTIBLE','vehiculo',true,1,'Vehículo'),
    ('CAMBIO_COMBUSTIBLE','propietario',true,2,'Propietario'),
    ('INSCRIPCION_PRENDA','vehiculo',true,1,'Vehículo'),
    ('INSCRIPCION_PRENDA','propietario',true,2,'Propietario'),
    ('LEVANTAMIENTO_PRENDA','vehiculo',true,1,'Vehículo'),
    ('LEVANTAMIENTO_PRENDA','propietario',true,2,'Propietario'),
    ('RADICADO_CUENTA','vehiculo',true,1,'Vehículo'),
    ('RADICADO_CUENTA','propietario',true,2,'Propietario'),
    ('TRASLADO_CUENTA','vehiculo',true,1,'Vehículo'),
    ('TRASLADO_CUENTA','propietario',true,2,'Propietario')
  ) AS m(type_code, edge_code, is_required, display_order, role_label)
  JOIN procedures_config.procedure_types t ON t.code = m.type_code
  JOIN procedures_config.edges e ON e.code = m.edge_code
  ON CONFLICT (procedure_type_id, edge_id) DO UPDATE SET
    is_required = EXCLUDED.is_required,
    display_order = EXCLUDED.display_order,
    role_label = EXCLUDED.role_label,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  INSERT INTO procedures_config.query_connectors (code, name, created_by, updated_by) VALUES
    ('RUNT','Registro Único Nacional de Tránsito',v_sys,v_sys),
    ('SIMIT','Sistema Integrado de Multas y Sanciones',v_sys,v_sys),
    ('RNMC','Registro Nacional de Medidas Correctivas',v_sys,v_sys),
    ('RESOLUCIONES','Resoluciones / Normatividad',v_sys,v_sys),
    ('RUES','Registro Único Empresarial y Social',v_sys,v_sys),
    ('FASECOLDA','FASECOLDA (técnicos/avalúo/pólizas)',v_sys,v_sys)
  ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  INSERT INTO procedures_config.procedure_type_query_configs (procedure_type_id, edge_id, query_connector_id, is_mandatory, person_kind_filter, display_order, created_by, updated_by)
  SELECT t.id, e.id, qc.id, q.is_mandatory, q.person_kind_filter, q.display_order, v_sys, v_sys
  FROM (VALUES
    ('TRA_ESTANDAR','vehiculo','RUNT',true,'any',1),
    ('TRA_ESTANDAR','vehiculo','FASECOLDA',false,'any',2),
    ('TRA_ESTANDAR','propietario','RUNT',true,'natural',1),
    ('TRA_ESTANDAR','propietario','SIMIT',true,'natural',2),
    ('TRA_ESTANDAR','propietario','RUES',true,'juridica',3),
    ('TRA_ESTANDAR','comprador','RUNT',true,'natural',1),
    ('TRA_ESTANDAR','comprador','SIMIT',true,'natural',2),
    ('TRA_ESTANDAR','comprador','RUES',true,'juridica',3),
    ('MAT_LEASING','locatario','SIMIT',true,'any',1)
  ) AS q(type_code, edge_code, connector_code, is_mandatory, person_kind_filter, display_order)
  JOIN procedures_config.procedure_types t ON t.code = q.type_code
  JOIN procedures_config.edges e ON e.code = q.edge_code
  JOIN procedures_config.procedure_type_edges m ON m.procedure_type_id = t.id AND m.edge_id = e.id
  JOIN procedures_config.query_connectors qc ON qc.code = q.connector_code
  ON CONFLICT (procedure_type_id, edge_id, query_connector_id, person_kind_filter) DO UPDATE SET
    is_mandatory = EXCLUDED.is_mandatory,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();
END $$;

RESET search_path;
