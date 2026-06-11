SET search_path TO identity, public;

DELETE FROM role_permissions rp
USING permissions p
WHERE rp.permission_id = p.id
  AND p.slug IN ('modulo.companias.ver', 'modulo.companias.gestionar');

DELETE FROM permissions
WHERE slug IN ('modulo.companias.ver', 'modulo.companias.gestionar');
