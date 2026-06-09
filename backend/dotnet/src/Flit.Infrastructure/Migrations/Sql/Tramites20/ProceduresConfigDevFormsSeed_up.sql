-- HU #9409 MTR-06 — form_sections, form_fields, activaciones y docs en PostgreSQL (idempotente)
-- Complementa Tramites20_DevSeed en bases que ya lo tenían sin formularios.

SET search_path TO procedures_config, public, identity, audit, catalogs, ot;

DO $$
DECLARE
  v_sys          uuid := '00000000-0000-7000-8000-000000000001';
  v_t_andina     uuid := '01930101-0001-7001-8001-000000000001';
  v_t_caribe     uuid := '01930101-0001-7001-8001-000000000002';
  v_ot_bogota    uuid := '01930102-0001-7001-8001-000000000101';
  v_type_traspaso uuid;
  v_type_leasing uuid;
  v_type_color   uuid;
  v_edge_veh     uuid;
  v_edge_prop    uuid;
  v_edge_comp    uuid;
  v_edge_loc     uuid;
  v_doc_cc       uuid;
  v_sec_tra_veh  uuid;
  v_sec_tra_prop uuid;
  v_sec_tra_comp uuid;
  v_sec_lea_loc  uuid;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'procedures_config' AND table_name = 'form_sections') THEN
    RAISE NOTICE 'ProceduresConfigDevFormsSeed: omitido — aplicar AddProceduresConfigParametrization50 primero';
    RETURN;
  END IF;

  SELECT id INTO v_type_traspaso FROM procedures_config.procedure_types WHERE code = 'TRA_ESTANDAR' LIMIT 1;
  SELECT id INTO v_type_leasing FROM procedures_config.procedure_types WHERE code = 'MAT_LEASING' LIMIT 1;
  SELECT id INTO v_type_color FROM procedures_config.procedure_types WHERE code = 'CAMBIO_COLOR' LIMIT 1;
  SELECT id INTO v_edge_veh FROM procedures_config.edges WHERE code = 'vehiculo' LIMIT 1;
  SELECT id INTO v_edge_prop FROM procedures_config.edges WHERE code = 'propietario' LIMIT 1;
  SELECT id INTO v_edge_comp FROM procedures_config.edges WHERE code = 'comprador' LIMIT 1;
  SELECT id INTO v_edge_loc FROM procedures_config.edges WHERE code = 'locatario' LIMIT 1;
  SELECT id INTO v_doc_cc FROM catalogs.document_types WHERE code = 'CC' LIMIT 1;

  IF v_type_traspaso IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_andina AND procedure_type_id = v_type_traspaso AND traffic_agency_id = v_ot_bogota AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_andina, v_type_traspaso, v_ot_bogota, true, '{}'::jsonb, v_sys, v_sys);
  END IF;

  IF v_type_leasing IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_andina AND procedure_type_id = v_type_leasing AND traffic_agency_id = v_ot_bogota AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_andina, v_type_leasing, v_ot_bogota, true, '{}'::jsonb, v_sys, v_sys);
  END IF;

  IF v_type_color IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_andina AND procedure_type_id = v_type_color AND traffic_agency_id = v_ot_bogota AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_andina, v_type_color, v_ot_bogota, true, '{}'::jsonb, v_sys, v_sys);
  END IF;

  IF v_type_traspaso IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_caribe AND procedure_type_id = v_type_traspaso AND traffic_agency_id IS NULL AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_caribe, v_type_traspaso, NULL, true, '{"disabled_edges":["comprador"]}'::jsonb, v_sys, v_sys);
  END IF;

  INSERT INTO procedures_config.query_connectors (code, name, created_by, updated_by) VALUES
    ('FIRMA','Firma digital / formato',v_sys,v_sys),
    ('RTM','Revisión técnico-mecánica',v_sys,v_sys)
  ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, updated_at = now();

  INSERT INTO procedures_config.procedure_type_query_configs (procedure_type_id, edge_id, query_connector_id, is_mandatory, is_omitible, person_kind_filter, display_order, created_by, updated_by)
  SELECT t.id, e.id, qc.id, q.is_mandatory, q.is_omitible, q.person_kind_filter, q.display_order, v_sys, v_sys
  FROM (VALUES
    ('CAMBIO_COLOR','vehiculo','RUNT',true,false,'any',1),
    ('CAMBIO_COLOR','propietario','RUNT',true,false,'natural',1),
    ('MAT_LEASING','locatario','SIMIT',true,false,'any',1),
    ('MAT_LEASING','locatario','FIRMA',false,true,'any',2),
    ('MAT_LEASING','locatario','RTM',false,true,'any',3)
  ) AS q(type_code, edge_code, connector_code, is_mandatory, is_omitible, person_kind_filter, display_order)
  JOIN procedures_config.procedure_types t ON t.code = q.type_code
  JOIN procedures_config.edges e ON e.code = q.edge_code
  JOIN procedures_config.procedure_type_edges m ON m.procedure_type_id = t.id AND m.edge_id = e.id
  JOIN procedures_config.query_connectors qc ON qc.code = q.connector_code
  ON CONFLICT (procedure_type_id, edge_id, query_connector_id, person_kind_filter) DO UPDATE SET
    is_mandatory = EXCLUDED.is_mandatory, is_omitible = EXCLUDED.is_omitible, updated_at = now();

  IF v_type_traspaso IS NOT NULL AND v_edge_veh IS NOT NULL THEN
    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000001', v_type_traspaso, v_edge_veh, 'vehiculo_datos', 'Datos del vehículo', 1, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET edge_id = EXCLUDED.edge_id, title = EXCLUDED.title, updated_at = now();
    SELECT id INTO v_sec_tra_veh FROM procedures_config.form_sections WHERE procedure_type_id = v_type_traspaso AND section_key = 'vehiculo_datos' LIMIT 1;
    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES
      (v_sec_tra_veh, 'placa', 'text', 'Placa', true, 1, 'lleno', true, v_sys, v_sys),
      (v_sec_tra_veh, 'vin', 'text', 'VIN', false, 2, 'vacio', false, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO UPDATE SET is_trigger = EXCLUDED.is_trigger, updated_at = now();

    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000002', v_type_traspaso, v_edge_prop, 'propietario_datos', 'Propietario / Vendedor', 2, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET updated_at = now();
    SELECT id INTO v_sec_tra_prop FROM procedures_config.form_sections WHERE procedure_type_id = v_type_traspaso AND section_key = 'propietario_datos' LIMIT 1;
    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_tra_prop, 'doc_propietario', 'text', 'Documento propietario', true, 1, 'lleno', true, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO NOTHING;

    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000003', v_type_traspaso, v_edge_comp, 'comprador_datos', 'Comprador', 3, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO NOTHING;
    SELECT id INTO v_sec_tra_comp FROM procedures_config.form_sections WHERE procedure_type_id = v_type_traspaso AND section_key = 'comprador_datos' LIMIT 1;
    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_tra_comp, 'doc_comprador', 'text', 'Documento comprador', true, 1, 'lleno', false, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO NOTHING;
  END IF;

  IF v_type_leasing IS NOT NULL AND v_edge_loc IS NOT NULL THEN
    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000010', v_type_leasing, v_edge_loc, 'locatario_datos', 'Locatario (Leasing)', 3, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO NOTHING;
    SELECT id INTO v_sec_lea_loc FROM procedures_config.form_sections WHERE procedure_type_id = v_type_leasing AND section_key = 'locatario_datos' LIMIT 1;
    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_lea_loc, 'doc_locatario', 'text', 'Documento locatario', true, 1, 'lleno', true, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO NOTHING;
  END IF;

  IF v_type_traspaso IS NOT NULL AND v_doc_cc IS NOT NULL THEN
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_traspaso, v_edge_prop, v_doc_cc, 'upload', true, 1, 'propietario', v_sys, v_sys
    WHERE NOT EXISTS (SELECT 1 FROM procedures_config.required_documents WHERE procedure_type_id = v_type_traspaso AND actor_role = 'propietario');
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_traspaso, v_edge_comp, v_doc_cc, 'upload', true, 2, 'comprador', v_sys, v_sys
    WHERE NOT EXISTS (SELECT 1 FROM procedures_config.required_documents WHERE procedure_type_id = v_type_traspaso AND actor_role = 'comprador');
  END IF;

  IF v_type_leasing IS NOT NULL AND v_doc_cc IS NOT NULL AND v_edge_loc IS NOT NULL THEN
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_leasing, v_edge_loc, v_doc_cc, 'upload', true, 1, 'locatario', v_sys, v_sys
    WHERE NOT EXISTS (SELECT 1 FROM procedures_config.required_documents WHERE procedure_type_id = v_type_leasing AND actor_role = 'locatario');
  END IF;

  RAISE NOTICE 'ProceduresConfigDevFormsSeed: formularios y activaciones HU #9409 aplicados.';
END $$;

RESET search_path;
