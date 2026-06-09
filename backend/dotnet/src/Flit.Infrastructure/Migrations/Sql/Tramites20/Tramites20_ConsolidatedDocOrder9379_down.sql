-- Rollback HU #9460 — consolidado OT catalog FK + seed DEV Bogotá.
SET search_path TO ot, procedures_config, public, identity;

DO $$
DECLARE
  v_ot_bogota uuid := '01930102-0001-7001-8001-000000000101';
  v_order_id uuid;
BEGIN
  SELECT id INTO v_order_id
  FROM ot.ot_consolidated_doc_orders
  WHERE traffic_agency_id = v_ot_bogota AND is_active = true AND deleted_at IS NULL
  LIMIT 1;

  IF v_order_id IS NOT NULL THEN
    DELETE FROM ot.ot_consolidated_doc_order_items WHERE order_id = v_order_id;
    DELETE FROM ot.ot_consolidated_doc_orders WHERE id = v_order_id;
  END IF;
END $$;

ALTER TABLE ot.ot_consolidated_doc_order_items DROP CONSTRAINT IF EXISTS ck_ot_cons_items_source;

ALTER TABLE ot.ot_consolidated_doc_order_items
  ADD CONSTRAINT ck_ot_cons_items_source CHECK (
    (source = 'global' AND document_type_id IS NOT NULL)
    OR (source = 'custom' AND custom_label IS NOT NULL)
  );

DROP INDEX IF EXISTS ot.ix_ot_cons_items_procedure_document_catalog_id;

ALTER TABLE ot.ot_consolidated_doc_order_items
  DROP CONSTRAINT IF EXISTS fk_ot_cons_items_procedure_document_catalog;

ALTER TABLE ot.ot_consolidated_doc_order_items
  DROP COLUMN IF EXISTS procedure_document_catalog_id;

RESET search_path;
