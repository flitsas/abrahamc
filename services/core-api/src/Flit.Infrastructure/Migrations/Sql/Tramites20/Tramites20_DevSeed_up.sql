-- =====================================================================================
-- FLIT Trámites 2.0 — Datos funcionales DEV (mock sin dependencias externas)
-- Features: #9370 #9466 #9381 #9383 #9378 #9379 #9408 #9409 #9410 #9467 #9469 #9369
-- Escenario: docs/designs/tramites-2.0/ejemplos-datos.html
-- Password dev para todos los usuarios con hash: FlitDev2026!
-- Idempotente: ON CONFLICT / WHERE NOT EXISTS
-- =====================================================================================

DO $$
DECLARE
  v_sys          uuid := '00000000-0000-7000-8000-000000000001';
  v_pwd          text := '$argon2id$v=19$m=32768,t=4,p=1$QlrrZTIZfyFG+sLWUR57FQ==$CgluLMnuKxT1XUoWqE18OgzJwml6729jRZKUs68rdvM=';
  v_t_andina     uuid := '01930101-0001-7001-8001-000000000001';
  v_t_caribe     uuid := '01930101-0001-7001-8001-000000000002';
  v_ot_bogota    uuid := '01930102-0001-7001-8001-000000000101';
  v_ot_medellin  uuid := '01930102-0001-7001-8001-000000000102';
  v_u_super      uuid := '01930201-0001-7001-8001-000000000010';
  v_u_and_admin  uuid := '01930201-0001-7001-8001-000000000011';
  v_u_and_op1    uuid := '01930201-0001-7001-8001-000000000012';
  v_u_car_admin  uuid := '01930201-0001-7001-8001-000000000021';
  v_u_car_op1    uuid := '01930201-0001-7001-8001-000000000022';
  v_u_legacy_dev uuid := '01900000-100b-7001-8001-000000000001';
  v_inst_draft   uuid := '01930301-0001-7001-8001-000000000201';
  v_inst_sent    uuid := '01930301-0001-7001-8001-000000000202';
  v_inst_pending uuid := '01930301-0001-7001-8001-000000000203';
  v_type_traspaso uuid;
  v_type_leasing uuid;
  v_type_color   uuid;
  v_edge_veh     uuid;
  v_edge_prop    uuid;
  v_edge_comp    uuid;
  v_edge_loc     uuid;
  v_doc_cc       uuid;
  v_doc_nit      uuid;
  v_sec_tra_veh  uuid;
  v_sec_tra_prop uuid;
  v_sec_tra_comp uuid;
  v_sec_lea_loc  uuid;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'identity' AND table_name = 'tenants') THEN
    RAISE NOTICE 'Tramites20_DevSeed: omitido — aplicar Tramites20_Foundation primero';
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
  SELECT id INTO v_doc_nit FROM catalogs.document_types WHERE code = 'NIT' LIMIT 1;

  -- Tenants B2B
  INSERT INTO identity.tenants (id, name, nit, slug, status, settings, created_by, updated_by)
  VALUES
    (v_t_andina, 'Transportes Andina S.A.', '900111222-3', 'transportes-andina', 'active',
     '{"locale":"es-CO","timezone":"America/Bogota"}'::jsonb, v_sys, v_sys),
    (v_t_caribe, 'Logística del Caribe Ltda.', '800333444-5', 'logistica-caribe', 'active',
     '{"locale":"es-CO","escrituras_enabled":true}'::jsonb, v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  -- Usuarios (login unificado por email)
  INSERT INTO identity.users (id, tenant_id, email, password_hash, account_state, created_by, updated_by)
  VALUES
    (v_u_super,     v_t_andina, 'superadmin@flit.com.co',              v_pwd, 'active', v_sys, v_sys),
    (v_u_and_admin, v_t_andina, 'admin@transportes-andina.com',        v_pwd, 'active', v_sys, v_sys),
    (v_u_and_op1,   v_t_andina, 'operador1@transportes-andina.com',  v_pwd, 'active', v_sys, v_sys),
    (v_u_car_admin, v_t_caribe, 'admin@logistica-caribe.com',          v_pwd, 'active', v_sys, v_sys),
    (v_u_car_op1,   v_t_caribe, 'operador1@logistica-caribe.com',      v_pwd, 'active', v_sys, v_sys),
    (v_u_legacy_dev, v_t_andina, 'dev-legacy@flit.local',              v_pwd, 'active', v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO identity.profiles (id, tenant_id, user_id, full_name, phone, locale, created_by, updated_by)
  VALUES
    ('01930210-0001-7001-8001-000000000011', v_t_andina, v_u_and_admin, 'María Gómez Ruiz', '+573001110001', 'es-CO', v_sys, v_sys),
    ('01930210-0001-7001-8001-000000000012', v_t_andina, v_u_and_op1,   'Carlos Pérez López', '+573102220002', 'es-CO', v_sys, v_sys),
    ('01930210-0001-7001-8001-000000000021', v_t_caribe, v_u_car_admin, 'Ana Torres Vega', '+573203330003', 'es-CO', v_sys, v_sys),
    ('01930210-0001-7001-8001-000000000022', v_t_caribe, v_u_car_op1,   'Luis Herrera Díaz', '+573154440004', 'es-CO', v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO identity.user_roles (tenant_id, user_id, role_id, created_by, updated_by)
  VALUES
    (v_t_andina,   v_u_super,     '00000000-0000-7000-8000-0000000000a1', v_sys, v_sys),
    (v_t_andina,   v_u_legacy_dev, '00000000-0000-7000-8000-0000000000a1', v_sys, v_sys),
    (v_t_andina,   v_u_and_admin, '00000000-0000-7000-8000-0000000000a2', v_sys, v_sys),
    (v_t_andina,   v_u_and_op1,   '00000000-0000-7000-8000-0000000000a3', v_sys, v_sys),
    (v_t_caribe,   v_u_car_admin, '00000000-0000-7000-8000-0000000000a2', v_sys, v_sys),
    (v_t_caribe,   v_u_car_op1,   '00000000-0000-7000-8000-0000000000a3', v_sys, v_sys)
  ON CONFLICT (tenant_id, user_id, role_id) DO NOTHING;

  INSERT INTO identity.password_policies (tenant_id, min_length, require_uppercase, lockout_threshold, lockout_minutes, created_by, updated_by)
  VALUES
    (v_t_andina, 12, true, 5, 30, v_sys, v_sys),
    (v_t_caribe, 10, true, 3, 15, v_sys, v_sys)
  ON CONFLICT (tenant_id) DO NOTHING;

  -- OT (escenario Bogotá + Medellín; complementa seed global de ddl/40)
  INSERT INTO ot.traffic_agencies (id, code, name, department_name, municipality_name, dane_municipality_code, nit,
    notifier_email, runt_agency_code, mandate_document_applies, created_by, updated_by)
  VALUES
    (v_ot_bogota,   'OT-BOGOTA',   'Secretaría Distrital de Movilidad — Bogotá',
     'Bogotá D.C.', 'Bogotá D.C.', '11001', '899999061-9', 'notificaciones@movilidadbogota.gov.co', '11001000', false, v_sys, v_sys),
    (v_ot_medellin, 'OT-MEDELLIN', 'ATM Medellín',
     'Antioquia', 'Medellín', '05001', '890905211-6', 'contacto@atm.gov.co', '05001000', true, v_sys, v_sys)
  ON CONFLICT (code) DO NOTHING;

  INSERT INTO ot.ot_users (id, traffic_agency_id, email, full_name, created_by, updated_by)
  VALUES
    ('01930202-0001-7001-8001-000000000101', v_ot_bogota,   'ot.bogota@movilidad.gov.co',   'Diana Rojas Prieto', v_sys, v_sys),
    ('01930202-0001-7001-8001-000000000102', v_ot_medellin, 'ot.medellin@atm.gov.co',       'Jorge Mejía Cano', v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO ot.ot_qx_integrations (traffic_agency_id, mode, callback_url, is_active, created_by, updated_by)
  VALUES
    (v_ot_bogota,   'dashboard', NULL, true, v_sys, v_sys),
    (v_ot_medellin, 'qx', 'https://qx-stub.flit.dev/atm-medellin/callback', true, v_sys, v_sys)
  ON CONFLICT (traffic_agency_id) DO NOTHING;

  -- Compañías 1:1 tenant
  INSERT INTO companies.companies (id, tenant_id, nit, legal_name, commercial_name, modules_enabled, created_by, updated_by)
  VALUES
    ('01930110-0001-7001-8001-000000000001', v_t_andina, '900111222-3', 'Transportes Andina S.A.', 'Andina',
     '{"registration":true,"transfers":true,"company":true,"escrituras":false}'::jsonb, v_sys, v_sys),
    ('01930110-0001-7001-8001-000000000002', v_t_caribe, '800333444-5', 'Logística del Caribe Ltda.', 'Caribe',
     '{"registration":true,"transfers":true,"company":true,"escrituras":true}'::jsonb, v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO companies.company_module_configs (tenant_id, module_key, config, created_by, updated_by)
  VALUES
    (v_t_andina, 'runt_contingency', '{"primary":"verifik","failover":["intempo"],"mock_dev":true}'::jsonb, v_sys, v_sys),
    (v_t_andina, 'transfers', '{"default_ot_code":"OT-BOGOTA","require_simit":true}'::jsonb, v_sys, v_sys),
    (v_t_caribe, 'registration', '{"default_ot_code":"OT-MEDELLIN"}'::jsonb, v_sys, v_sys)
  ON CONFLICT (tenant_id, module_key) DO NOTHING;

  INSERT INTO companies.signature_wallets (id, tenant_id, balance, low_threshold, auto_recharge, created_by, updated_by)
  VALUES
    ('01930111-0001-7001-8001-000000000001', v_t_andina, 450, 50, true, v_sys, v_sys),
    ('01930111-0001-7001-8001-000000000002', v_t_caribe, 120, 30, false, v_sys, v_sys)
  ON CONFLICT (tenant_id) DO NOTHING;

  -- Interceptor propiedad vehicular (#9448)
  INSERT INTO companies.vehicle_ownership_rules (id, tenant_id, name, rule_type, condition, priority, is_active, created_by, updated_by)
  VALUES
    ('01930120-0001-7001-8001-000000000001', v_t_andina, 'Bloquear placa restringida', 'block',
     '{"plates":["XYZ789"],"procedure_types":["traspaso"]}'::jsonb, 10, true, v_sys, v_sys),
    ('01930120-0001-7001-8001-000000000002', v_t_andina, 'Permitir traspaso estándar', 'allow',
     '{"procedure_types":["traspaso"]}'::jsonb, 100, true, v_sys, v_sys),
    ('01930120-0001-7001-8001-000000000003', v_t_caribe, 'Advertir vehículo antiguo', 'warn',
     '{"max_model_year":2010}'::jsonb, 50, true, v_sys, v_sys)
  ON CONFLICT (id) DO NOTHING;

  IF v_doc_nit IS NOT NULL THEN
    INSERT INTO companies.escrituras (id, tenant_id, document_type_id, deed_number, company_name, expiration_date, created_by, updated_by)
    VALUES
      ('01930112-0001-7001-8001-000000000001', v_t_caribe, v_doc_nit, 'ESC-2020-0045', 'Logística del Caribe Ltda.', '2030-12-31', v_sys, v_sys),
      ('01930112-0001-7001-8001-000000000002', v_t_caribe, v_doc_nit, 'POD-2024-0012', 'Logística del Caribe Ltda.', '2027-06-30', v_sys, v_sys)
    ON CONFLICT (id) DO NOTHING;
  END IF;

  -- Activaciones por tenant (parametrización usable sin OT real externo)
  IF v_type_traspaso IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_andina AND procedure_type_id = v_type_traspaso AND traffic_agency_id = v_ot_bogota AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_andina, v_type_traspaso, v_ot_bogota, true, '{"default_queries":["RUNT","SIMIT"]}'::jsonb, v_sys, v_sys);
  END IF;
  IF v_type_color IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM procedures_config.procedure_type_activations
    WHERE tenant_id = v_t_caribe AND procedure_type_id = v_type_color AND traffic_agency_id = v_ot_medellin AND deleted_at IS NULL
  ) THEN
    INSERT INTO procedures_config.procedure_type_activations (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
    VALUES (v_t_caribe, v_type_color, v_ot_medellin, true, '{"simit_mandatory":true}'::jsonb, v_sys, v_sys);
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

  -- Conectores omitibles leasing (MTR-05)
  INSERT INTO procedures_config.query_connectors (code, name, created_by, updated_by) VALUES
    ('FIRMA','Firma digital / formato',v_sys,v_sys),
    ('RTM','Revisión técnico-mecánica',v_sys,v_sys)
  ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, updated_by = EXCLUDED.updated_by, updated_at = now();

  INSERT INTO procedures_config.procedure_type_query_configs (procedure_type_id, edge_id, query_connector_id, is_mandatory, is_omitible, person_kind_filter, display_order, created_by, updated_by)
  SELECT t.id, e.id, qc.id, q.is_mandatory, q.is_omitible, q.person_kind_filter, q.display_order, v_sys, v_sys
  FROM (VALUES
    ('TRA_ESTANDAR','vehiculo','RUNT',true,false,'any',1),
    ('TRA_ESTANDAR','propietario','RUNT',true,false,'natural',1),
    ('TRA_ESTANDAR','propietario','SIMIT',true,false,'natural',2),
    ('TRA_ESTANDAR','propietario','RUES',true,false,'juridica',3),
    ('TRA_ESTANDAR','propietario','SIMIT',true,false,'juridica',4),
    ('TRA_ESTANDAR','comprador','SIMIT',true,false,'natural',2),
    ('TRA_ESTANDAR','comprador','RUES',true,false,'juridica',3),
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
    is_mandatory = EXCLUDED.is_mandatory,
    is_omitible = EXCLUDED.is_omitible,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();

  -- Formularios dinámicos (MTR-06) — datos en procedures_config.form_sections / form_fields
  IF v_type_traspaso IS NOT NULL AND v_edge_veh IS NOT NULL THEN
    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000001', v_type_traspaso, v_edge_veh, 'vehiculo_datos', 'Datos del vehículo', 1, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET
      edge_id = EXCLUDED.edge_id, title = EXCLUDED.title, display_order = EXCLUDED.display_order, updated_by = EXCLUDED.updated_by, updated_at = now();
    SELECT id INTO v_sec_tra_veh FROM procedures_config.form_sections
    WHERE procedure_type_id = v_type_traspaso AND section_key = 'vehiculo_datos' LIMIT 1;

    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES
      (v_sec_tra_veh, 'placa', 'text', 'Placa', true, 1, 'lleno', true, v_sys, v_sys),
      (v_sec_tra_veh, 'vin', 'text', 'VIN', false, 2, 'vacio', false, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO UPDATE SET
      label = EXCLUDED.label, is_required = EXCLUDED.is_required, is_trigger = EXCLUDED.is_trigger,
      display_order = EXCLUDED.display_order, updated_by = EXCLUDED.updated_by, updated_at = now();

    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000002', v_type_traspaso, v_edge_prop, 'propietario_datos', 'Propietario / Vendedor', 2, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET edge_id = EXCLUDED.edge_id, title = EXCLUDED.title, updated_by = EXCLUDED.updated_by, updated_at = now();
    SELECT id INTO v_sec_tra_prop FROM procedures_config.form_sections
    WHERE procedure_type_id = v_type_traspaso AND section_key = 'propietario_datos' LIMIT 1;

    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_tra_prop, 'doc_propietario', 'text', 'Documento propietario', true, 1, 'lleno', true, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO UPDATE SET is_trigger = EXCLUDED.is_trigger, updated_by = EXCLUDED.updated_by, updated_at = now();

    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000003', v_type_traspaso, v_edge_comp, 'comprador_datos', 'Comprador', 3, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET edge_id = EXCLUDED.edge_id, title = EXCLUDED.title, updated_by = EXCLUDED.updated_by, updated_at = now();
    SELECT id INTO v_sec_tra_comp FROM procedures_config.form_sections
    WHERE procedure_type_id = v_type_traspaso AND section_key = 'comprador_datos' LIMIT 1;

    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_tra_comp, 'doc_comprador', 'text', 'Documento comprador', true, 1, 'lleno', false, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO UPDATE SET updated_by = EXCLUDED.updated_by, updated_at = now();
  END IF;

  IF v_type_leasing IS NOT NULL AND v_edge_loc IS NOT NULL THEN
    INSERT INTO procedures_config.form_sections (id, procedure_type_id, edge_id, section_key, title, display_order, ui_mode, created_by, updated_by)
    VALUES ('01930601-0001-7001-8001-000000000010', v_type_leasing, v_edge_loc, 'locatario_datos', 'Locatario (Leasing)', 3, 'interactive', v_sys, v_sys)
    ON CONFLICT (procedure_type_id, section_key) DO UPDATE SET edge_id = EXCLUDED.edge_id, title = EXCLUDED.title, updated_by = EXCLUDED.updated_by, updated_at = now();
    SELECT id INTO v_sec_lea_loc FROM procedures_config.form_sections
    WHERE procedure_type_id = v_type_leasing AND section_key = 'locatario_datos' LIMIT 1;

    INSERT INTO procedures_config.form_fields (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
    VALUES (v_sec_lea_loc, 'doc_locatario', 'text', 'Documento locatario', true, 1, 'lleno', true, v_sys, v_sys)
    ON CONFLICT (section_id, field_key) DO UPDATE SET is_trigger = EXCLUDED.is_trigger, updated_by = EXCLUDED.updated_by, updated_at = now();
  END IF;

  IF v_type_traspaso IS NOT NULL AND v_doc_cc IS NOT NULL THEN
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_traspaso, v_edge_prop, v_doc_cc, 'upload', true, 1, 'propietario', v_sys, v_sys
    WHERE NOT EXISTS (
      SELECT 1 FROM procedures_config.required_documents
      WHERE procedure_type_id = v_type_traspaso AND actor_role = 'propietario' AND kind = 'upload'
    );
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_traspaso, v_edge_comp, v_doc_cc, 'upload', true, 2, 'comprador', v_sys, v_sys
    WHERE NOT EXISTS (
      SELECT 1 FROM procedures_config.required_documents
      WHERE procedure_type_id = v_type_traspaso AND actor_role = 'comprador' AND kind = 'upload'
    );
  END IF;

  IF v_type_leasing IS NOT NULL AND v_doc_cc IS NOT NULL AND v_edge_loc IS NOT NULL THEN
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_leasing, v_edge_loc, v_doc_cc, 'upload', true, 1, 'locatario', v_sys, v_sys
    WHERE NOT EXISTS (
      SELECT 1 FROM procedures_config.required_documents rd
      WHERE rd.procedure_type_id = v_type_leasing AND rd.actor_role = 'locatario' AND rd.kind = 'upload'
    );
    INSERT INTO procedures_config.required_documents (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, created_by, updated_by)
    SELECT v_type_leasing, v_edge_veh, v_doc_cc, 'upload', true, 2, 'vehiculo', v_sys, v_sys
    WHERE NOT EXISTS (
      SELECT 1 FROM procedures_config.required_documents rd
      WHERE rd.procedure_type_id = v_type_leasing AND rd.actor_role = 'vehiculo' AND rd.kind = 'upload'
    );
  END IF;

  -- Runtime trámites (#9408) — 3 instancias del escenario
  IF v_type_traspaso IS NOT NULL AND v_type_color IS NOT NULL THEN
    INSERT INTO procedures.procedure_instances (
      id, tenant_id, procedure_type_id, traffic_agency_id, reference_number, state,
      config_snapshot, assigned_to_user_id, radicated_at, total_amount, created_by, updated_by)
    VALUES
      (v_inst_draft, v_t_andina, v_type_traspaso, v_ot_bogota, 'AND-2026-00042', 'borrador',
       '{"scenario":"andina-draft","plate":"ABC123"}'::jsonb, v_u_and_op1, NULL, 0, v_u_and_op1, v_u_and_op1),
      (v_inst_sent, v_t_andina, v_type_traspaso, v_ot_bogota, 'AND-2026-00038', 'enviado',
       '{"scenario":"andina-sent","plate":"ABC123"}'::jsonb, v_u_and_op1, now() - interval '2 days', 125000.00, v_u_and_op1, v_u_and_op1),
      (v_inst_pending, v_t_caribe, v_type_color, v_ot_medellin, 'CAR-2026-00015', 'pendiente',
       '{"scenario":"caribe-pending","plate":"XYZ789"}'::jsonb, v_u_car_op1, now() - interval '1 day', 89000.00, v_u_car_op1, v_u_car_op1)
    ON CONFLICT (id) DO NOTHING;
  END IF;

  -- Integraciones mock (#9467) — sin llamadas reales a Verifik/QX
  INSERT INTO integrations.external_query_calls (id, tenant_id, procedure_instance_id, query_connector_code, request, response, http_status, latency_ms, succeeded, called_at)
  VALUES
    ('01930401-0001-7001-8001-000000000001', v_t_andina, v_inst_draft, 'RUNT',
     '{"plate":"ABC123","mock":true}'::jsonb,
     '{"make":"TOYOTA","line":"COROLLA","model_year":2020,"mock_provider":"verifik-stub"}'::jsonb,
     200, 142, true, now() - interval '1 hour'),
    ('01930401-0001-7001-8001-000000000002', v_t_andina, v_inst_sent, 'SIMIT',
     '{"document":"1234567890","mock":true}'::jsonb,
     '{"fines":[],"mock_provider":"simit-stub"}'::jsonb,
     200, 89, true, now() - interval '3 hours')
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO integrations.runt_sync_log (id, tenant_id, provider, operation, outcome, failover_from, payload, synced_at)
  VALUES
    ('01930402-0001-7001-8001-000000000001', v_t_andina, 'verifik', 'vehicle_lookup', 'ok', 'runt',
     '{"plate":"ABC123","mock":true}'::jsonb, now() - interval '2 hours')
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO integrations.webhook_events (id, tenant_id, traffic_agency_id, direction, event_type, payload, idempotency_key, status, processed_at)
  VALUES
    ('01930403-0001-7001-8001-000000000001', v_t_caribe, v_ot_medellin, 'inbound', 'procedure.state_changed',
     '{"instance_id":"01930301-0001-7001-8001-000000000203","state":"pendiente","mock":true}'::jsonb,
     'qx-med-2026-00015-1', 'processed', now() - interval '30 minutes')
  ON CONFLICT (id) DO NOTHING;

  -- IDSecure mock (#9469)
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'identity_verification' AND table_name = 'verification_sessions') THEN
    INSERT INTO identity_verification.verification_sessions (
      id, tenant_id, subject_document_type_id, subject_document_number, provider, status, verdict, score,
      performed_at, expires_at, created_by, updated_by)
    SELECT
      '01930501-0001-7001-8001-000000000001', v_t_andina, dt.id, '1234567890', 'mock', 'passed', 'approved', 98.50,
      now() - interval '1 day', now() + interval '30 days', v_sys, v_sys
    FROM catalogs.document_types dt WHERE dt.code = 'CC' LIMIT 1
    ON CONFLICT (id) DO NOTHING;
  END IF;

  RAISE NOTICE 'Tramites20_DevSeed: Andina/Caribe + OT + form_sections/fields + activaciones MAT_LEASING/CAMBIO_COLOR. Login: *@transportes-andina.com / FlitDev2026!';
END $$;
