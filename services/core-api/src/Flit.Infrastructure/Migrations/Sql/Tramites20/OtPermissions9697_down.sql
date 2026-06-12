SET search_path TO identity, public;

DELETE FROM role_permissions rp
USING permissions p
WHERE rp.permission_id = p.id
  AND p.slug IN ('modulo.ot.administrar', 'modulo.ot.gestionar-reglas');

DELETE FROM permissions
WHERE slug IN ('modulo.ot.administrar', 'modulo.ot.gestionar-reglas');
