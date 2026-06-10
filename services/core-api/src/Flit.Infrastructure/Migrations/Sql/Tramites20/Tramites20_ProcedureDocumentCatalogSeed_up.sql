-- Seed idempotente del catálogo procedure_document_catalog (bases ya migradas).

SET search_path TO procedures_config, public, identity;

DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'procedures_config' AND table_name = 'procedure_document_catalog'
  ) THEN
    RAISE NOTICE 'ProcedureDocumentCatalogSeed: omitido — tabla no existe';
    RETURN;
  END IF;

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
