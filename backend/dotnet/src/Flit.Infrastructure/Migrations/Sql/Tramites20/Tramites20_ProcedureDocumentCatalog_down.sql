SET search_path TO procedures_config, public;

ALTER TABLE procedures_config.required_documents
  DROP CONSTRAINT IF EXISTS ck_required_documents_document_ref;

ALTER TABLE procedures_config.required_documents
  DROP CONSTRAINT IF EXISTS fk_required_documents_procedure_document_catalog;

DROP INDEX IF EXISTS ix_required_documents_procedure_document_catalog_id;

ALTER TABLE procedures_config.required_documents
  DROP COLUMN IF EXISTS procedure_document_catalog_id;

-- document_type_id vuelve NOT NULL solo si no hay filas huérfanas (DEV).
ALTER TABLE procedures_config.required_documents
  ALTER COLUMN document_type_id SET NOT NULL;

DROP TABLE IF EXISTS procedures_config.procedure_document_catalog CASCADE;
