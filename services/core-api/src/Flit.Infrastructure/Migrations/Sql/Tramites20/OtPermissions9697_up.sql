-- HU #9697 / #9699 — permisos OT administrar y gestionar reglas
SET search_path TO identity, public;

INSERT INTO permissions (slug, module, action, description, is_system) VALUES
  ('modulo.ot.administrar', 'ot', 'administrar', 'Cambiar modo integración Dashboard/QX del OT', false),
  ('modulo.ot.gestionar-reglas', 'ot', 'gestionar-reglas', 'CRUD reglas de negocio OT', false)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, rp.role_id, p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM role_permissions rp
INNER JOIN permissions existing ON existing.id = rp.permission_id
  AND existing.slug = 'modulo.ot.superadmin'
CROSS JOIN permissions p
WHERE p.slug IN ('modulo.ot.administrar', 'modulo.ot.gestionar-reglas')
ON CONFLICT (role_id, permission_id) DO NOTHING;
