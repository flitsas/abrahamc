-- Catálogo de documentos del trámite (adjuntos / auto-generados) — procedures_config.
-- Distinto de catalogs.document_types (identidad Registraduría: CC, NIT…).

SET search_path TO procedures_config, public, identity, audit, catalogs;

CREATE TABLE IF NOT EXISTS procedures_config.procedure_document_catalog (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  code              text        NOT NULL,
  name              text        NOT NULL,
  is_active         boolean     NOT NULL DEFAULT true,
  display_order     integer     NOT NULL DEFAULT 0,
  allowed_formats   jsonb       NOT NULL DEFAULT '["pdf"]'::jsonb,
  max_size_mb       integer       NULL,
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid        NULL,
  updated_by        uuid        NULL,
  row_version       integer     NOT NULL DEFAULT 1,
  CONSTRAINT uq_procedure_document_catalog_code UNIQUE (code),
  CONSTRAINT fk_procedure_document_catalog_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_document_catalog_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_procedure_document_catalog_display_order
  ON procedures_config.procedure_document_catalog (display_order, name);

ALTER TABLE procedures_config.procedure_document_catalog ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS config_read ON procedures_config.procedure_document_catalog;
CREATE POLICY config_read ON procedures_config.procedure_document_catalog FOR SELECT USING (true);
DROP POLICY IF EXISTS config_modify ON procedures_config.procedure_document_catalog;
CREATE POLICY config_modify ON procedures_config.procedure_document_catalog
  FOR ALL USING (identity.is_super_admin()) WITH CHECK (identity.is_super_admin());

DROP TRIGGER IF EXISTS tr_procedure_document_catalog_before_update_row_version
  ON procedures_config.procedure_document_catalog;
CREATE TRIGGER tr_procedure_document_catalog_before_update_row_version
  BEFORE UPDATE ON procedures_config.procedure_document_catalog
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

DROP TRIGGER IF EXISTS tr_procedure_document_catalog_audit ON procedures_config.procedure_document_catalog;
CREATE TRIGGER tr_procedure_document_catalog_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedures_config.procedure_document_catalog
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

COMMENT ON TABLE procedures_config.procedure_document_catalog IS
  '@context:procedures_config Catálogo parametrizable de documentos del trámite (SOAT, tarjeta de propiedad, etc.).';

-- required_documents: referencia al catálogo de trámite (nuevo) además de document_types (legacy seeds).
ALTER TABLE procedures_config.required_documents
  ADD COLUMN IF NOT EXISTS procedure_document_catalog_id uuid NULL;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'fk_required_documents_procedure_document_catalog'
  ) THEN
    ALTER TABLE procedures_config.required_documents
      ADD CONSTRAINT fk_required_documents_procedure_document_catalog
      FOREIGN KEY (procedure_document_catalog_id)
      REFERENCES procedures_config.procedure_document_catalog (id)
      ON UPDATE CASCADE ON DELETE RESTRICT;
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_required_documents_procedure_document_catalog_id
  ON procedures_config.required_documents (procedure_document_catalog_id);

ALTER TABLE procedures_config.required_documents
  ALTER COLUMN document_type_id DROP NOT NULL;

ALTER TABLE procedures_config.required_documents
  DROP CONSTRAINT IF EXISTS ck_required_documents_document_ref;

ALTER TABLE procedures_config.required_documents
  ADD CONSTRAINT ck_required_documents_document_ref CHECK (
    (document_type_id IS NOT NULL AND procedure_document_catalog_id IS NULL)
    OR (document_type_id IS NULL AND procedure_document_catalog_id IS NOT NULL)
  );

-- Catálogo inicial de documentos del trámite (adjuntos reales, no tipos de identidad).
DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
BEGIN
  INSERT INTO procedures_config.procedure_document_catalog (code, name, display_order, created_by, updated_by) VALUES
    ('SOAT', 'SOAT vigente', 10, v_sys, v_sys),
    ('TARJETA_PROPIEDAD', 'Tarjeta de propiedad', 20, v_sys, v_sys),
    ('FACTURA_COMPRAVENTA', 'Factura de compraventa', 30, v_sys, v_sys),
    ('PODER_NOTARIAL', 'Poder notarial', 40, v_sys, v_sys),
    ('CEDULA_COPIA', 'Copia de cédula', 50, v_sys, v_sys),
    ('RUNT_CERTIFICADO', 'Certificado RUNT', 60, v_sys, v_sys)
  ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    display_order = EXCLUDED.display_order,
    is_active = TRUE,
    updated_at = now();
END $$;
