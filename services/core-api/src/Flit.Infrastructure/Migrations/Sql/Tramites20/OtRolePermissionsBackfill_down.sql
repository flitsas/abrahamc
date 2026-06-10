-- Reversa selectiva: solo quita OT slugs añadidos por el backfill (no toca crud-total preexistente en 0a1).
DELETE FROM identity.role_permissions rp
USING identity.permissions p
WHERE rp.permission_id = p.id
  AND rp.role_id IN (
    '00000000-0000-7000-8000-0000000000a1',
    '00000000-0000-7000-8000-0000000000a2'
  )
  AND rp.tenant_id IS NULL
  AND p.slug IN ('modulo.ot.operador', 'modulo.ot.superadmin');
