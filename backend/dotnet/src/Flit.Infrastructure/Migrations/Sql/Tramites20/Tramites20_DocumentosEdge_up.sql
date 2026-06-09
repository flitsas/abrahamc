-- Arista «documentos» — parametrización de required_documents por trámite (no por arista persona).
DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
  v_edge_doc uuid;
BEGIN
  ALTER TABLE procedures_config.edges DROP CONSTRAINT IF EXISTS edges_code_check;
  ALTER TABLE procedures_config.edges ADD CONSTRAINT edges_code_check
    CHECK (code IN ('vehiculo','propietario','comprador','locatario','documentos'));

  ALTER TABLE procedures_config.edges DROP CONSTRAINT IF EXISTS edges_edge_kind_check;
  ALTER TABLE procedures_config.edges ADD CONSTRAINT edges_edge_kind_check
    CHECK (edge_kind IN ('vehicle','person','documents'));

  INSERT INTO procedures_config.edges (id, code, name, edge_kind, display_order, is_active, created_by, updated_by)
  VALUES ('00000000-0000-7000-8002-000000000005', 'documentos', 'Documentos del trámite', 'documents', 5, TRUE, v_sys, v_sys)
  ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    edge_kind = EXCLUDED.edge_kind,
    display_order = EXCLUDED.display_order,
    is_active = EXCLUDED.is_active,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  SELECT id INTO v_edge_doc FROM procedures_config.edges WHERE code = 'documentos' LIMIT 1;

  -- Matriz: documentos como paso 4 en tipos con traspaso estándar / leasing / etc.
  INSERT INTO procedures_config.procedure_type_edges (procedure_type_id, edge_id, is_active, is_required, display_order, role_label, created_by, updated_by)
  SELECT t.id, v_edge_doc, TRUE, FALSE, 4, 'Documentos', v_sys, v_sys
  FROM procedures_config.procedure_types t
  WHERE t.code IN ('TRA_ESTANDAR','TRA_UNILATERAL','TRA_DOMINIO','MAT_ESTANDAR','MAT_LEASING')
  ON CONFLICT (procedure_type_id, edge_id) DO UPDATE SET
    is_active = EXCLUDED.is_active,
    display_order = EXCLUDED.display_order,
    role_label = EXCLUDED.role_label,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  -- Migrar required_documents: edge_id → documentos; actor_role = arista persona anterior.
  UPDATE procedures_config.required_documents rd
  SET edge_id = v_edge_doc,
      actor_role = COALESCE(rd.actor_role, e.code),
      updated_at = now(),
      updated_by = v_sys
  FROM procedures_config.edges e
  WHERE rd.edge_id = e.id
    AND e.code IN ('propietario','comprador','locatario','vehiculo')
    AND e.code <> 'documentos';
END $$;
