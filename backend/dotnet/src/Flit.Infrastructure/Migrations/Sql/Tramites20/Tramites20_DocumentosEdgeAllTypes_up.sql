-- Asegura arista documentos en la matriz de TODOS los procedure_types (FK required_documents).

SET search_path TO procedures_config, public, identity;

DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
  v_edge_doc uuid;
BEGIN
  SELECT id INTO v_edge_doc FROM procedures_config.edges WHERE code = 'documentos' LIMIT 1;
  IF v_edge_doc IS NULL THEN
    RAISE NOTICE 'DocumentosEdgeAllTypes: omitido — arista documentos no existe';
    RETURN;
  END IF;

  INSERT INTO procedures_config.procedure_type_edges
    (procedure_type_id, edge_id, is_active, is_required, display_order, role_label, created_by, updated_by)
  SELECT
    t.id,
    v_edge_doc,
    TRUE,
    FALSE,
    COALESCE((
      SELECT MAX(m.display_order) + 1
      FROM procedures_config.procedure_type_edges m
      WHERE m.procedure_type_id = t.id
    ), 4),
    'Documentos',
    v_sys,
    v_sys
  FROM procedures_config.procedure_types t
  WHERE NOT EXISTS (
    SELECT 1
    FROM procedures_config.procedure_type_edges m
    WHERE m.procedure_type_id = t.id AND m.edge_id = v_edge_doc
  );
END $$;
