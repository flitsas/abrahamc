DELETE FROM identity.role_permissions rp
USING identity.permissions p, identity.roles r
WHERE rp.permission_id = p.id
  AND rp.role_id = r.id
  AND p.slug = 'modulo.identidad.gestionar-usuarios'
  AND r.slug IN ('tenant-admin', 'super-admin');

DELETE FROM identity.permissions WHERE slug = 'modulo.identidad.gestionar-usuarios';
