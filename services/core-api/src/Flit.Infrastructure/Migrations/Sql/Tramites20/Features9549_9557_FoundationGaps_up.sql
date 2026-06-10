-- =====================================================================================
-- FLIT Trámites 2.0 — Gaps schema Features #9549 #9550 #9553 #9557
-- ABAC, desalojo de sesión, excepciones vehiculares, matriz OT tenant, plantillas SMTP
-- Idempotente: IF NOT EXISTS / ADD COLUMN IF NOT EXISTS
-- Prerrequisito: Tramites20_Foundation aplicado
-- =====================================================================================

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'identity' AND table_name = 'users'
  ) THEN
    RAISE NOTICE 'Features9549_9557_FoundationGaps: omitido — aplicar Tramites20_Foundation primero';
    RETURN;
  END IF;
END $$;

-- -------------------------------------------------------------------------------------
-- #9549 — ABAC en role_permissions + epoch de permisos en users (desalojo JWT RF14)
-- -------------------------------------------------------------------------------------
ALTER TABLE identity.role_permissions
  ADD COLUMN IF NOT EXISTS abac_conditions jsonb NULL;

COMMENT ON COLUMN identity.role_permissions.abac_conditions IS
  '@context:identity Condiciones ABAC opcionales (atributos tenant/usuario/recurso). #9549 RF09-RF10.';

ALTER TABLE identity.users
  ADD COLUMN IF NOT EXISTS permissions_epoch integer NOT NULL DEFAULT 1;

COMMENT ON COLUMN identity.users.permissions_epoch IS
  'Incrementa ante cambio de roles/permisos; JWT debe coincidir o sesión inválida (#9549 RF14).';

-- Ampliar estados de cuenta para onboarding pendiente
ALTER TABLE identity.users DROP CONSTRAINT IF EXISTS users_account_state_check;
ALTER TABLE identity.users
  ADD CONSTRAINT users_account_state_check
  CHECK (account_state IN ('active','inactive','pending','temp_blocked','permanent_blocked'));

-- Permisos UI string adicionales (#9549 RF10)
INSERT INTO identity.permissions (slug, module, action, description, is_system) VALUES
  ('ui.tramites.generar-consolidado', 'tramites', 'ui', 'Botón generar consolidado', false),
  ('ui.tramites.cambiar-estado', 'tramites', 'ui', 'Acción cambiar estado trámite', false),
  ('ui.companias.editar-config', 'companias', 'ui', 'Editar configuración compañía', false),
  ('ui.ot.reordenar-documentos', 'ot', 'ui', 'Drag-and-drop orden consolidado', false),
  ('ui.parametrizacion.simular-reglas', 'parametrizacion', 'ui', 'Simulador de reglas', false)
ON CONFLICT (slug) DO NOTHING;

-- -------------------------------------------------------------------------------------
-- #9550 — Lista blanca excepciones vehiculares + matriz OT autorizados por tenant
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS companies.tenant_user_exceptions (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id     uuid        NOT NULL,
  user_id       uuid        NOT NULL,
  reason        text        NULL,
  expires_at    timestamptz NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL,
  deleted_at    timestamptz NULL,
  deleted_by    uuid        NULL,
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_tenant_user_exceptions_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_tenant_user_exceptions_users FOREIGN KEY (user_id)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_tenant_user_exceptions_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_tenant_user_exceptions_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_tenant_user_exceptions_tenant_user UNIQUE (tenant_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_tenant_user_exceptions_tenant_id
  ON companies.tenant_user_exceptions (tenant_id) WHERE deleted_at IS NULL;

ALTER TABLE companies.tenant_user_exceptions ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON companies.tenant_user_exceptions;
CREATE POLICY tenant_isolation ON companies.tenant_user_exceptions
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

DROP TRIGGER IF EXISTS tr_tenant_user_exceptions_before_update_row_version ON companies.tenant_user_exceptions;
CREATE TRIGGER tr_tenant_user_exceptions_before_update_row_version
  BEFORE UPDATE ON companies.tenant_user_exceptions
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

DROP TRIGGER IF EXISTS tr_tenant_user_exceptions_audit ON companies.tenant_user_exceptions;
CREATE TRIGGER tr_tenant_user_exceptions_audit
  AFTER INSERT OR UPDATE OR DELETE ON companies.tenant_user_exceptions
  FOR EACH ROW EXECUTE FUNCTION audit.log_change();

COMMENT ON TABLE companies.tenant_user_exceptions IS
  '@context:companies Lista blanca only_own_vehicles (#9550 RF05).';

CREATE TABLE IF NOT EXISTS companies.tenant_authorized_traffic_agencies (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id           uuid        NOT NULL,
  traffic_agency_id   uuid        NOT NULL,
  is_enabled          boolean     NOT NULL DEFAULT true,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid        NOT NULL,
  updated_at          timestamptz NOT NULL DEFAULT now(),
  updated_by          uuid        NOT NULL,
  deleted_at          timestamptz NULL,
  deleted_by          uuid        NULL,
  row_version         integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_tenant_ot_matrix_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_tenant_ot_matrix_agencies FOREIGN KEY (traffic_agency_id)
    REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_tenant_ot_matrix_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_tenant_ot_matrix_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_tenant_ot_matrix_tenant_agency UNIQUE (tenant_id, traffic_agency_id)
);

CREATE INDEX IF NOT EXISTS ix_tenant_ot_matrix_tenant_id
  ON companies.tenant_authorized_traffic_agencies (tenant_id) WHERE deleted_at IS NULL;

ALTER TABLE companies.tenant_authorized_traffic_agencies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON companies.tenant_authorized_traffic_agencies;
CREATE POLICY tenant_isolation ON companies.tenant_authorized_traffic_agencies
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

DROP TRIGGER IF EXISTS tr_tenant_ot_matrix_before_update_row_version ON companies.tenant_authorized_traffic_agencies;
CREATE TRIGGER tr_tenant_ot_matrix_before_update_row_version
  BEFORE UPDATE ON companies.tenant_authorized_traffic_agencies
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

COMMENT ON TABLE companies.tenant_authorized_traffic_agencies IS
  '@context:companies Matriz gobierno OT habilitados por tenant (#9550 RF13).';

-- -------------------------------------------------------------------------------------
-- Servicio global SMTP — plantillas transaccionales (#9549 / #9550 RF08)
-- -------------------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS notifications;

CREATE TABLE IF NOT EXISTS notifications.email_templates (
  id            uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  template_key  text        NOT NULL,
  locale        text        NOT NULL DEFAULT 'es-CO',
  subject       text        NOT NULL,
  html_body     text        NOT NULL,
  is_active     boolean     NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid        NOT NULL DEFAULT '00000000-0000-7000-8000-000000000001',
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid        NOT NULL DEFAULT '00000000-0000-7000-8000-000000000001',
  row_version   integer     NOT NULL DEFAULT 1,
  CONSTRAINT uq_email_templates_key_locale UNIQUE (template_key, locale)
);

COMMENT ON TABLE notifications.email_templates IS
  '@context:notifications Catálogo global plantillas SMTP (onboarding, reset, invitación). #9549';

INSERT INTO notifications.email_templates (template_key, locale, subject, html_body) VALUES
  ('onboarding_invitation', 'es-CO', 'Invitación a FLIT',
   '<p>Hola,</p><p>Has sido invitado a FLIT. Usa el enlace para activar tu cuenta.</p>'),
  ('password_reset', 'es-CO', 'Restablecer contraseña FLIT',
   '<p>Solicitaste restablecer tu contraseña.</p><p>El enlace expira en 30 minutos.</p><p><a href="{{reset_url}}">Restablecer contraseña</a></p>'),
  ('forced_password_reset', 'es-CO', 'Cambio de contraseña requerido',
   '<p>Un administrador solicitó el cambio de tu contraseña. Inicia sesión con el enlace.</p>'),
  ('procedure_notification', 'es-CO', 'Actualización de trámite FLIT',
   '<p>Tu trámite ha sido actualizado.</p>')
ON CONFLICT (template_key, locale) DO NOTHING;

-- -------------------------------------------------------------------------------------
-- #9557 — Etiquetas documentales personalizadas OT
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ot.document_labels (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  traffic_agency_id   uuid        NOT NULL,
  label_key           text        NOT NULL,
  display_name        text        NOT NULL,
  exclude_from_bundle boolean     NOT NULL DEFAULT false,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid        NOT NULL,
  updated_at          timestamptz NOT NULL DEFAULT now(),
  updated_by          uuid        NOT NULL,
  deleted_at          timestamptz NULL,
  deleted_by          uuid        NULL,
  row_version         integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_document_labels_agencies FOREIGN KEY (traffic_agency_id)
    REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_document_labels_users_creator FOREIGN KEY (created_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_document_labels_users_updater FOREIGN KEY (updated_by)
    REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_document_labels_agency_key UNIQUE (traffic_agency_id, label_key)
);

CREATE INDEX IF NOT EXISTS ix_document_labels_agency_id
  ON ot.document_labels (traffic_agency_id) WHERE deleted_at IS NULL;

ALTER TABLE ot.document_labels ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS agency_isolation ON ot.document_labels;
CREATE POLICY agency_isolation ON ot.document_labels
  USING (true)
  WITH CHECK (true);

COMMENT ON TABLE ot.document_labels IS
  '@context:ot Etiquetas documentales personalizadas (#9557 RF11).';
