-- =====================================================================================
-- FLIT Trámites 2.0 — Seed DEV Features #9549 #9550 #9553 #9557
-- Complementa Tramites20_DevSeed con excepciones, OT matrix, reglas OT, QX mock
-- Idempotente. Password dev: FlitDev2026!
-- =====================================================================================

DO $$
DECLARE
  v_sys          uuid := '00000000-0000-7000-8000-000000000001';
  v_t_andina     uuid := '01930101-0001-7001-8001-000000000001';
  v_t_caribe     uuid := '01930101-0001-7001-8001-000000000002';
  v_ot_bogota    uuid := '01930102-0001-7001-8001-000000000101';
  v_ot_medellin  uuid := '01930102-0001-7001-8001-000000000102';
  v_u_super      uuid := '01930201-0001-7001-8001-000000000010';
  v_u_and_admin  uuid := '01930201-0001-7001-8001-000000000011';
  v_u_and_op1    uuid := '01930201-0001-7001-8001-000000000012';
  v_u_car_op1    uuid := '01930201-0001-7001-8001-000000000022';
  v_type_traspaso uuid;
  v_perm_ui_cons uuid;
  v_perm_ui_ot   uuid;
  v_order_id     uuid;
  v_doc_cc       uuid;
  v_cat_id       uuid;
  v_pos          int := 0;
  v_code         text;
  v_codes        text[] := ARRAY['FUN', 'SOAT', 'TARJETA_PROPIEDAD'];
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'identity' AND table_name = 'users' AND column_name = 'permissions_epoch'
  ) THEN
    RAISE NOTICE 'Features9549_9557_DevSeed: omitido — aplicar Features9549_9557_FoundationGaps primero';
    RETURN;
  END IF;

  SELECT id INTO v_type_traspaso FROM procedures_config.procedure_types WHERE code = 'TRA_ESTANDAR' LIMIT 1;
  SELECT id INTO v_perm_ui_cons FROM identity.permissions WHERE slug = 'ui.tramites.generar-consolidado' LIMIT 1;
  SELECT id INTO v_perm_ui_ot FROM identity.permissions WHERE slug = 'ui.ot.reordenar-documentos' LIMIT 1;
  SELECT id INTO v_doc_cc FROM catalogs.document_types WHERE code = 'CC' LIMIT 1;

  -- Usuario pendiente onboarding mock
  INSERT INTO identity.users (id, tenant_id, email, password_hash, account_state, created_by, updated_by)
  VALUES (
    '01930201-0001-7001-8001-000000000099', v_t_andina,
    'pendiente@transportes-andina.com', NULL, 'pending', v_sys, v_sys
  )
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO identity.onboarding_invitations (
    id, tenant_id, email, invited_role_id, token_hash, signature, status, expires_at, created_by, updated_by)
  VALUES (
    '01930203-0001-7001-8001-000000000001', v_t_andina, 'pendiente@transportes-andina.com',
    '00000000-0000-7000-8000-0000000000a3',
    'mock_hash_pendiente_dev_only', 'mock_sig_dev', 'pending', now() + interval '24 hours',
    v_u_and_admin, v_u_and_admin
  )
  ON CONFLICT (id) DO NOTHING;

  -- #9550 — Config empresa ampliada (only_own_vehicles, firmas, notificaciones, recaudo)
  INSERT INTO companies.company_module_configs (tenant_id, module_key, config, created_by, updated_by)
  VALUES
    (v_t_andina, 'company', jsonb_build_object(
      'only_own_vehicles', true,
      'seller_signature_mode', 'digital_identity',
      'buyer_signature_mode', 'on_screen',
      'signature_vault_enabled', true,
      'notification_channel', 'smtp',
      'notification_target', 'radicador',
      'payment_methods', jsonb_build_array('flit_gateway', 'ot', 'other')
    ), v_sys, v_sys),
    (v_t_caribe, 'company', jsonb_build_object(
      'only_own_vehicles', false,
      'seller_signature_mode', 'preassigned',
      'buyer_signature_mode', 'digital_identity',
      'notification_channel', 'client_api',
      'notification_target', 'comprador',
      'payment_methods', jsonb_build_array('flit_gateway')
    ), v_sys, v_sys)
  ON CONFLICT (tenant_id, module_key) DO UPDATE SET
    config = EXCLUDED.config, updated_by = EXCLUDED.updated_by, updated_at = now();

  -- Lista blanca excepción vehicular
  INSERT INTO companies.tenant_user_exceptions (id, tenant_id, user_id, reason, created_by, updated_by)
  VALUES (
    '01930121-0001-7001-8001-000000000001', v_t_andina, v_u_and_op1,
    'Operador autorizado traspaso vehículos de terceros (mock DEV)', v_u_and_admin, v_u_and_admin
  )
  ON CONFLICT (tenant_id, user_id) DO NOTHING;

  -- Matriz OT autorizados
  INSERT INTO companies.tenant_authorized_traffic_agencies (tenant_id, traffic_agency_id, is_enabled, created_by, updated_by)
  VALUES
    (v_t_andina, v_ot_bogota, true, v_sys, v_sys),
    (v_t_andina, v_ot_medellin, false, v_sys, v_sys),
    (v_t_caribe, v_ot_medellin, true, v_sys, v_sys)
  ON CONFLICT (tenant_id, traffic_agency_id) DO UPDATE SET
    is_enabled = EXCLUDED.is_enabled, updated_by = EXCLUDED.updated_by, updated_at = now();

  -- RUNT mock Verifik primario
  INSERT INTO companies.company_module_configs (tenant_id, module_key, config, created_by, updated_by)
  VALUES (v_t_andina, 'runt_contingency', '{"primary":"verifik","failover":["intempo"],"timeout_ms":4000,"mock_dev":true}'::jsonb, v_sys, v_sys)
  ON CONFLICT (tenant_id, module_key) DO UPDATE SET config = EXCLUDED.config, updated_at = now();

  -- #9553 — Regla de negocio ejemplo en procedures_config (si tabla rules existe)
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'procedures_config' AND table_name = 'rules')
     AND v_type_traspaso IS NOT NULL THEN
    INSERT INTO procedures_config.rules (
      id, tenant_id, procedure_type_id, name, condition_tree, actions, is_active, created_by, updated_by)
    VALUES (
      '01930701-0001-7001-8001-000000000001', v_t_andina, v_type_traspaso,
      'Ocultar comprador si placa restringida',
      '{"op":"AND","children":[{"field":"placa","operator":"contains","value":{"kind":"static","value":"XYZ"}}]}'::jsonb,
      '[{"type":"inject_section","target":"comprador_datos","mode":"hide"}]'::jsonb,
      true, v_sys, v_sys
    )
    ON CONFLICT (id) DO NOTHING;
  END IF;

  -- #9557 — Regla OT + orden consolidado + etiquetas + webhook QX mock adicional
  INSERT INTO ot.ot_rules (id, traffic_agency_id, name, trigger_event, condition_tree, actions, priority, is_active, created_by, updated_by)
  VALUES (
    '01930801-0001-7001-8001-000000000001', v_ot_medellin,
    'Bloquear traspaso sin SOAT vigente (mock)', 'on_submit',
    '{"op":"AND","children":[{"field":"soat_vigente","operator":"equal","value":{"kind":"static","value":"false"}}]}'::jsonb,
    '[{"type":"block","message":"SOAT no vigente"}]'::jsonb,
    10, true, v_sys, v_sys
  )
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO ot.document_labels (id, traffic_agency_id, label_key, display_name, exclude_from_bundle, created_by, updated_by)
  VALUES
    ('01930802-0001-7001-8001-000000000001', v_ot_medellin, 'certificado_custom', 'Certificado adicional OT', false, v_sys, v_sys),
    ('01930802-0001-7001-8001-000000000002', v_ot_medellin, 'borrador_obsoleto', 'Documento obsoleto (excluido)', true, v_sys, v_sys)
  ON CONFLICT (traffic_agency_id, label_key) DO NOTHING;

  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'procedures_config' AND table_name = 'procedure_document_catalog'
  ) THEN
    INSERT INTO ot.ot_consolidated_doc_orders (id, traffic_agency_id, version, is_active, created_by, updated_by)
    VALUES ('01930803-0001-7001-8001-000000000001', v_ot_medellin, 1, true, v_sys, v_sys)
    ON CONFLICT (id) DO NOTHING;

    SELECT id INTO v_order_id FROM ot.ot_consolidated_doc_orders
    WHERE traffic_agency_id = v_ot_medellin AND is_active = true AND deleted_at IS NULL
    LIMIT 1;

    IF v_order_id IS NOT NULL THEN
      FOREACH v_code IN ARRAY v_codes LOOP
        SELECT id INTO v_cat_id
        FROM procedures_config.procedure_document_catalog
        WHERE code = v_code AND is_active = true
        LIMIT 1;
        IF v_cat_id IS NULL THEN CONTINUE; END IF;
        v_pos := v_pos + 1;
        IF NOT EXISTS (
          SELECT 1 FROM ot.ot_consolidated_doc_order_items
          WHERE order_id = v_order_id AND procedure_document_catalog_id = v_cat_id
        ) THEN
          INSERT INTO ot.ot_consolidated_doc_order_items (
            traffic_agency_id, order_id, procedure_document_catalog_id, position, source, created_by, updated_by)
          VALUES (v_ot_medellin, v_order_id, v_cat_id, v_pos, 'global', v_sys, v_sys);
        END IF;
      END LOOP;
    END IF;
  END IF;

  INSERT INTO integrations.webhook_events (id, tenant_id, traffic_agency_id, direction, event_type, payload, idempotency_key, status, processed_at)
  VALUES (
    '01930403-0001-7001-8001-000000000002', v_t_caribe, v_ot_medellin, 'inbound', 'qx.procedure.approved',
    '{"mock":true,"provider":"quipux-stub","state":"aprobado"}'::jsonb,
    'qx-med-mock-approve-1', 'processed', now() - interval '10 minutes'
  )
  ON CONFLICT (id) DO NOTHING;

  -- Permiso UI OT al rol operador global
  IF v_perm_ui_ot IS NOT NULL THEN
    INSERT INTO identity.role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
    SELECT NULL, '00000000-0000-7000-8000-0000000000a3', v_perm_ui_ot, v_sys, v_sys
    WHERE NOT EXISTS (
      SELECT 1 FROM identity.role_permissions
      WHERE role_id = '00000000-0000-7000-8000-0000000000a3' AND permission_id = v_perm_ui_ot
    );
  END IF;

  RAISE NOTICE 'Features9549_9557_DevSeed: excepciones, OT matrix, reglas, QX webhook aplicados.';
END $$;
