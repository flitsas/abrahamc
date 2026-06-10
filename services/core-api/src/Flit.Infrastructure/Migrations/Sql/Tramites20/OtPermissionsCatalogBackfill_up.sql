-- Asegura slugs OT en catálogo y en roles maestros (BDs creadas antes de modulo.ot.operador / superadmin).
DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
  v_role_super uuid := '00000000-0000-7000-8000-0000000000a1';
  v_role_tenant_admin uuid := '00000000-0000-7000-8000-0000000000a2';
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'identity' AND table_name = 'permissions'
  ) THEN
    RAISE NOTICE 'OtPermissionsCatalogBackfill: omitido — schema identity no aplicado';
    RETURN;
  END IF;

  INSERT INTO identity.permissions (slug, module, action, is_system, is_assignable, is_active)
  VALUES
    ('modulo.ot.operador', 'ot', 'operador', true, true, true),
    ('modulo.ot.superadmin', 'ot', 'superadmin', true, true, true)
  ON CONFLICT (slug) DO NOTHING;

  INSERT INTO identity.role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
  SELECT NULL, v_role_super, p.id, v_sys, v_sys
  FROM identity.permissions p
  WHERE p.slug IN ('modulo.ot.operador', 'modulo.ot.superadmin', 'modulo.ot.crud-total')
    AND p.is_active = true
  ON CONFLICT (role_id, permission_id) DO NOTHING;

  INSERT INTO identity.role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
  SELECT NULL, v_role_tenant_admin, p.id, v_sys, v_sys
  FROM identity.permissions p
  WHERE p.slug = 'modulo.ot.operador'
    AND p.is_active = true
  ON CONFLICT (role_id, permission_id) DO NOTHING;
END $$;
