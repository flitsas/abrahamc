SET search_path TO identity, public;

DELETE FROM role_permissions rp
USING permissions p
WHERE rp.permission_id = p.id
  AND p.slug = 'modulo.parametrizacion.gestionar';

DELETE FROM permissions
WHERE slug = 'modulo.parametrizacion.gestionar';
