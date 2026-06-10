-- =====================================================================================
-- FLIT Trámites 2.0 — HU #9684 permiso gestión usuarios identidad
-- Idempotente
-- =====================================================================================

INSERT INTO identity.permissions (slug, module, action, description, is_system) VALUES
  ('modulo.identidad.gestionar-usuarios', 'identidad', 'gestionar-usuarios',
   'Gestionar usuarios, invitaciones y roles del tenant', false)
ON CONFLICT (slug) DO NOTHING;

-- Asignar a tenant-admin y super-admin
INSERT INTO identity.role_permissions (role_id, permission_id, created_by, updated_by)
SELECT r.id, p.id, '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM identity.roles r
CROSS JOIN identity.permissions p
WHERE r.slug IN ('tenant-admin', 'super-admin')
  AND p.slug = 'modulo.identidad.gestionar-usuarios'
  AND NOT EXISTS (
    SELECT 1 FROM identity.role_permissions rp
    WHERE rp.role_id = r.id AND rp.permission_id = p.id AND rp.deleted_at IS NULL
  );
