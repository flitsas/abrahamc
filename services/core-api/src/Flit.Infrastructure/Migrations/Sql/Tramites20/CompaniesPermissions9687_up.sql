-- HU #9687 — permisos granulares consola compañías (ver / gestionar)
SET search_path TO identity, public;

INSERT INTO permissions (slug, module, action, description, is_system) VALUES
  ('modulo.companias.ver', 'companias', 'ver', 'Ver indexación de compañías B2B', false),
  ('modulo.companias.gestionar', 'companias', 'gestionar', 'Crear y gobernar compañías maestro (SuperAdmin)', false)
ON CONFLICT (slug) DO NOTHING;

-- Roles con crud-total reciben ver + gestionar (misma cobertura operativa)
INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, rp.role_id, p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM role_permissions rp
INNER JOIN permissions existing ON existing.id = rp.permission_id
  AND existing.slug = 'modulo.companias.crud-total'
CROSS JOIN permissions p
WHERE p.slug IN ('modulo.companias.ver', 'modulo.companias.gestionar')
ON CONFLICT (role_id, permission_id) DO NOTHING;
