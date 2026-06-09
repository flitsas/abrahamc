-- HU #9460 (Feature #9379) — FK consolidado → procedure_document_catalog (#9409) + seed DEV Bogotá.
SET search_path TO ot, procedures_config, public, identity;

DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'ot' AND table_name = 'ot_consolidated_doc_order_items'
  ) THEN
    RAISE NOTICE 'ConsolidatedDocOrder9379: omitido — aplicar Tramites20_Foundation primero';
    RETURN;
  END IF;

  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'procedures_config' AND table_name = 'procedure_document_catalog'
  ) THEN
    INSERT INTO procedures_config.procedure_document_catalog (code, name, display_order, created_by, updated_by) VALUES
      ('FUN', 'Formulario Único Nacional', 5, v_sys, v_sys),
      ('CARTA_SELFIE', 'Carta Selfie', 15, v_sys, v_sys),
      ('ORDEN_MANDATO', 'Orden de Mandato', 25, v_sys, v_sys)
    ON CONFLICT (code) DO UPDATE SET
      name = EXCLUDED.name,
      display_order = EXCLUDED.display_order,
      is_active = TRUE,
      updated_at = now();
  END IF;
END $$;

ALTER TABLE ot.ot_consolidated_doc_order_items
  ADD COLUMN IF NOT EXISTS procedure_document_catalog_id uuid NULL;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_ot_cons_items_procedure_document_catalog'
  ) THEN
    ALTER TABLE ot.ot_consolidated_doc_order_items
      ADD CONSTRAINT fk_ot_cons_items_procedure_document_catalog
      FOREIGN KEY (procedure_document_catalog_id)
      REFERENCES procedures_config.procedure_document_catalog (id)
      ON UPDATE CASCADE ON DELETE RESTRICT;
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ot_cons_items_procedure_document_catalog_id
  ON ot.ot_consolidated_doc_order_items (procedure_document_catalog_id);

ALTER TABLE ot.ot_consolidated_doc_order_items DROP CONSTRAINT IF EXISTS ck_ot_cons_items_source;

ALTER TABLE ot.ot_consolidated_doc_order_items
  ADD CONSTRAINT ck_ot_cons_items_source CHECK (
    (
      source = 'global'
      AND procedure_document_catalog_id IS NOT NULL
      AND document_type_id IS NULL
      AND custom_label IS NULL
    )
    OR (
      source = 'custom'
      AND custom_label IS NOT NULL
      AND procedure_document_catalog_id IS NULL
      AND document_type_id IS NULL
    )
  );

COMMENT ON COLUMN ot.ot_consolidated_doc_order_items.procedure_document_catalog_id IS
  '@context:ot #9379 — documento global del catálogo de trámite (#9409 procedure_document_catalog).';

-- Seed orden activo DEV — OT Bogotá (Tramites20_DevSeed v_ot_bogota). AC2 HU #9460.
DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
  v_ot_bogota uuid := '01930102-0001-7001-8001-000000000101';
  v_order_id uuid;
  v_pos int := 0;
  v_code text;
  v_cat_id uuid;
  v_codes text[] := ARRAY['FUN', 'CARTA_SELFIE', 'SOAT', 'TARJETA_PROPIEDAD', 'ORDEN_MANDATO'];
BEGIN
  IF NOT EXISTS (SELECT 1 FROM ot.traffic_agencies WHERE id = v_ot_bogota) THEN
    RAISE NOTICE 'ConsolidatedDocOrder9379 seed: OT Bogotá DEV no encontrado — omitido';
    RETURN;
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'procedures_config' AND table_name = 'procedure_document_catalog'
  ) THEN
    RETURN;
  END IF;

  INSERT INTO ot.ot_consolidated_doc_orders (traffic_agency_id, version, is_active, created_by, updated_by)
  SELECT v_ot_bogota, 1, true, v_sys, v_sys
  WHERE NOT EXISTS (
    SELECT 1 FROM ot.ot_consolidated_doc_orders
    WHERE traffic_agency_id = v_ot_bogota AND is_active = true AND deleted_at IS NULL
  );

  SELECT id INTO v_order_id
  FROM ot.ot_consolidated_doc_orders
  WHERE traffic_agency_id = v_ot_bogota AND is_active = true AND deleted_at IS NULL
  LIMIT 1;

  IF v_order_id IS NULL THEN
    RETURN;
  END IF;

  FOREACH v_code IN ARRAY v_codes LOOP
    SELECT id INTO v_cat_id
    FROM procedures_config.procedure_document_catalog
    WHERE code = v_code AND is_active = true
    LIMIT 1;

    IF v_cat_id IS NULL THEN
      CONTINUE;
    END IF;

    v_pos := v_pos + 1;

    IF NOT EXISTS (
      SELECT 1 FROM ot.ot_consolidated_doc_order_items
      WHERE order_id = v_order_id AND procedure_document_catalog_id = v_cat_id
    ) THEN
      INSERT INTO ot.ot_consolidated_doc_order_items (
        traffic_agency_id, order_id, procedure_document_catalog_id, position, source, created_by, updated_by
      ) VALUES (
        v_ot_bogota, v_order_id, v_cat_id, v_pos, 'global', v_sys, v_sys
      );
    END IF;
  END LOOP;
END $$;

RESET search_path;
