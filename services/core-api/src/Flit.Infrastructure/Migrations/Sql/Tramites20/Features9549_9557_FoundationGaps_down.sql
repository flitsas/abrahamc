-- Reversión Features #9549 #9550 #9553 #9557 gaps (orden inverso)

DROP TABLE IF EXISTS ot.document_labels CASCADE;
DROP TABLE IF EXISTS notifications.email_templates CASCADE;
DROP TABLE IF EXISTS companies.tenant_authorized_traffic_agencies CASCADE;
DROP TABLE IF EXISTS companies.tenant_user_exceptions CASCADE;

DELETE FROM identity.permissions WHERE slug IN (
  'ui.tramites.generar-consolidado',
  'ui.tramites.cambiar-estado',
  'ui.companias.editar-config',
  'ui.ot.reordenar-documentos',
  'ui.parametrizacion.simular-reglas'
);

ALTER TABLE identity.users DROP COLUMN IF EXISTS permissions_epoch;
ALTER TABLE identity.role_permissions DROP COLUMN IF EXISTS abac_conditions;

ALTER TABLE identity.users DROP CONSTRAINT IF EXISTS users_account_state_check;
ALTER TABLE identity.users
  ADD CONSTRAINT users_account_state_check
  CHECK (account_state IN ('active','inactive','temp_blocked','permanent_blocked'));
