-- HU #9692 — permiso granular parametrización (gestionar)
SET search_path TO identity, public;

INSERT INTO permissions (slug, module, action, description, is_system) VALUES
  ('modulo.parametrizacion.gestionar', 'parametrizacion', 'gestionar', 'Crear y gobernar familias/tipos de trámite (SuperAdmin)', false)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, rp.role_id, p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM role_permissions rp
INNER JOIN permissions existing ON existing.id = rp.permission_id
  AND existing.slug = 'modulo.parametrizacion.crud-total'
CROSS JOIN permissions p
WHERE p.slug = 'modulo.parametrizacion.gestionar'
ON CONFLICT (role_id, permission_id) DO NOTHING;
