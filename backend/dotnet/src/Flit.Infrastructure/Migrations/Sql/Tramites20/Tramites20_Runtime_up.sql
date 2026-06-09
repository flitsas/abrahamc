-- =====================================================================================
-- FLIT Trámites 2.0 — Runtime layer (#9467 #9408 #9469 #9369)
-- 70 integrations (sin duplicar endpoint_call_log de RGL-02) → 75 → 76 → 80 → 90
-- =====================================================================================

-- =====================================================================================
-- FLIT 2.0 · DDL 70 — Integraciones (schema integrations)
-- Ejecución de conectores externos: logs de llamadas, RUNT/Verifik/Intempo failover, webhooks QX.
-- Tablas append-only (sin row_version/soft-delete: son bitácoras). RLS por tenant.
-- procedure_instance_id es referencia suave (sin FK; procedures es de mayor jerarquía).
-- =====================================================================================
SET search_path TO integrations, public;

-- external_query_calls — log crudo de cada consulta externa (#9409 CF-I1). Particionar por mes en PDN.
CREATE TABLE external_query_calls (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NULL,            -- soft ref a procedures.procedure_instances
  query_connector_code  text        NOT NULL,
  edge_role             text        NULL,
  request               jsonb       NOT NULL DEFAULT '{}'::jsonb,
  response              jsonb       NULL,
  http_status           integer     NULL,
  latency_ms            integer     NULL,
  succeeded             boolean     NOT NULL DEFAULT false,
  error_message         text        NULL,
  called_at             timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_external_query_calls_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX ix_external_query_calls_tenant_id_called_at ON external_query_calls (tenant_id, called_at DESC);
CREATE INDEX ix_external_query_calls_procedure_instance_id ON external_query_calls (procedure_instance_id);
ALTER TABLE external_query_calls ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON external_query_calls
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
COMMENT ON TABLE external_query_calls IS '@context:integrations Bitácora de consultas externas. @pii: evitar PII innecesaria en request/response (CF-I3).';

-- runt_sync_log — trazas RUNT con failover Verifik/Intempo (#9381 contingencia)
CREATE TABLE runt_sync_log (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  provider              text        NOT NULL CHECK (provider IN ('runt','verifik','intempo')),
  operation             text        NOT NULL,
  outcome               text        NOT NULL CHECK (outcome IN ('ok','failed','timeout','circuit_open')),
  failover_from         text        NULL CHECK (failover_from IN ('runt','verifik','intempo')),
  payload               jsonb       NOT NULL DEFAULT '{}'::jsonb,
  synced_at             timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_runt_sync_log_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX ix_runt_sync_log_tenant_id_synced_at ON runt_sync_log (tenant_id, synced_at DESC);
ALTER TABLE runt_sync_log ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON runt_sync_log
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- endpoint_call_log: omitido (creado por migración AddIntegrationsEndpointCallLogRgl02 / HU #9438)

-- webhook_events — sincronización QX (Quipux) inbound/outbound con idempotencia (#9378 QX)
CREATE TABLE webhook_events (
  id                uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id         uuid        NOT NULL,
  traffic_agency_id uuid        NULL,
  direction         text        NOT NULL CHECK (direction IN ('inbound','outbound')),
  event_type        text        NOT NULL,
  payload           jsonb       NOT NULL DEFAULT '{}'::jsonb,
  signature         text        NULL,
  idempotency_key   text        NOT NULL,
  status            text        NOT NULL DEFAULT 'received' CHECK (status IN ('received','processed','failed')),
  received_at       timestamptz NOT NULL DEFAULT now(),
  processed_at      timestamptz NULL,
  CONSTRAINT fk_webhook_events_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_webhook_events_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT uq_webhook_events_idempotency UNIQUE (idempotency_key)
);
CREATE INDEX ix_webhook_events_tenant_id_received_at ON webhook_events (tenant_id, received_at DESC);
CREATE INDEX ix_webhook_events_traffic_agency_id ON webhook_events (traffic_agency_id);
ALTER TABLE webhook_events ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON webhook_events
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS integrations.webhook_events, integrations.endpoint_call_log,
--   integrations.runt_sync_log, integrations.external_query_calls CASCADE;


-- >>> 75-identity_verification.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 75 — Validación de identidad (schema identity_verification)
-- Liveness/biometría (§8 reglas-estándar, #9408 FR-6). Corre una vez y se reutiliza.
-- Evidencia = dato sensible (@pii:high); el binario vive en files; aquí solo veredicto y refs.
-- =====================================================================================
SET search_path TO identity_verification, public;

-- verification_sessions — sesión de validación (liveness + captura). Reutilizable por el trámite.
CREATE TABLE verification_sessions (
  id                       uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id                uuid        NOT NULL,
  subject_document_type_id uuid        NOT NULL,
  subject_document_number  text        NOT NULL,
  provider                 text        NOT NULL DEFAULT 'verifik',
  status                   text        NOT NULL DEFAULT 'pending'
                                       CHECK (status IN ('pending','passed','failed','expired')),
  verdict                  text        NULL,
  score                    numeric(5,2) NULL,
  performed_at             timestamptz NULL,
  expires_at              timestamptz NULL,
  created_at               timestamptz NOT NULL DEFAULT now(),
  created_by               uuid        NOT NULL,
  updated_at               timestamptz NOT NULL DEFAULT now(),
  updated_by               uuid        NOT NULL,
  deleted_at               timestamptz NULL,
  deleted_by               uuid        NULL,
  row_version              integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_sessions_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_sessions_document_types FOREIGN KEY (subject_document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_sessions_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_sessions_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_verification_sessions_tenant_id ON verification_sessions (tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX ix_verification_sessions_tenant_id_subject ON verification_sessions (tenant_id, subject_document_type_id, subject_document_number);
ALTER TABLE verification_sessions ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_sessions
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_sessions_before_update_row_version BEFORE UPDATE ON verification_sessions FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_verification_sessions_audit AFTER INSERT OR UPDATE OR DELETE ON verification_sessions FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN verification_sessions.subject_document_number IS '@pii:high @retention:según política Habeas Data';
COMMENT ON COLUMN verification_sessions.verdict IS 'Resultado disponible para plantillas de documentos (marker_map) y reglas de consulta.';

-- verification_evidences — evidencias (liveness, fotos de documento). Binario en files.
CREATE TABLE verification_evidences (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  evidence_type           text        NOT NULL CHECK (evidence_type IN ('liveness','document_front','document_back','selfie')),
  file_id                 uuid        NOT NULL,
  captured_at             timestamptz NOT NULL DEFAULT now(),
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  deleted_at              timestamptz NULL,
  deleted_by              uuid        NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_evidences_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_evidences_sessions FOREIGN KEY (verification_session_id) REFERENCES verification_sessions (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_evidences_files FOREIGN KEY (file_id) REFERENCES files.files (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_evidences_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_evidences_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_verification_evidences_tenant_id_session ON verification_evidences (tenant_id, verification_session_id);
CREATE INDEX ix_verification_evidences_file_id ON verification_evidences (file_id);
ALTER TABLE verification_evidences ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_evidences
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_evidences_before_update_row_version BEFORE UPDATE ON verification_evidences FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_verification_evidences_audit AFTER INSERT OR UPDATE OR DELETE ON verification_evidences FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE verification_evidences IS '@context:identity_verification @pii:high Evidencia biométrica (dato sensible Ley 1581).';

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS identity_verification.verification_evidences,
--   identity_verification.verification_sessions CASCADE;


-- >>> 76-idsecure-tramites.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 76 — IDSecure-Trámites (extensión identity_verification)
-- Feature ADO #9469 · Evolución de IDSecure-Traspasos → validación multi-trámite.
-- Requiere: ddl/00, ddl/20, ddl/25, ddl/50 (procedure_types), ddl/75 (base), ddl/80 (instances/actors).
-- Provider producción: vertex_ai; DEV: mock (misma interfaz IIdentityVerificationProvider).
-- =====================================================================================
SET search_path TO identity_verification, public;

-- -------------------------------------------------------------------------------------
-- procedure_type_verification_configs — qué participantes/aristas exigen IDSecure por tipo
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_type_verification_configs (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  procedure_type_id       uuid        NOT NULL,   -- soft ref procedures_config.procedure_types
  participant_role        text        NOT NULL,   -- arista/rol: vendedor, comprador, locatario, propietario, etc.
  is_required             boolean     NOT NULL DEFAULT true,
  email_template_key      text        NOT NULL DEFAULT 'default',
  sort_order              integer     NOT NULL DEFAULT 0,
  is_active               boolean     NOT NULL DEFAULT true,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  deleted_at              timestamptz NULL,
  deleted_by              uuid        NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_ptv_configs_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ptv_configs_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_ptv_configs_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_ptv_configs_type_role UNIQUE (tenant_id, procedure_type_id, participant_role)
);
CREATE INDEX ix_ptv_configs_tenant_id_type ON procedure_type_verification_configs (tenant_id, procedure_type_id) WHERE deleted_at IS NULL;
ALTER TABLE procedure_type_verification_configs ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_type_verification_configs
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_ptv_configs_before_update_row_version BEFORE UPDATE ON procedure_type_verification_configs FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_ptv_configs_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_type_verification_configs FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- verification_invitations — token URL único, TTL 48h, un solo uso (RF-1.3/1.4)
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_invitations (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  procedure_instance_id   uuid        NOT NULL,   -- soft ref procedures.procedure_instances
  procedure_actor_id      uuid        NULL,       -- soft ref procedures.procedure_actors
  participant_role        text        NOT NULL,
  recipient_email         text        NOT NULL,
  token_hash              text        NOT NULL,
  status                  text        NOT NULL DEFAULT 'pending'
                                      CHECK (status IN ('pending','sent','opened','consumed','expired','revoked')),
  expires_at              timestamptz NOT NULL,
  consumed_at             timestamptz NULL,
  resent_count            integer     NOT NULL DEFAULT 0,
  last_sent_at            timestamptz NULL,
  idempotency_key         text        NULL,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  deleted_at              timestamptz NULL,
  deleted_by              uuid        NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_invitations_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_invitations_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_invitations_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_verification_invitations_token_hash UNIQUE (token_hash)
);
CREATE INDEX ix_verification_invitations_tenant_instance ON verification_invitations (tenant_id, procedure_instance_id) WHERE deleted_at IS NULL;
CREATE INDEX ix_verification_invitations_expires_at ON verification_invitations (expires_at) WHERE status IN ('pending','sent','opened');
ALTER TABLE verification_invitations ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_invitations
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_invitations_before_update_row_version BEFORE UPDATE ON verification_invitations FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_verification_invitations_audit AFTER INSERT OR UPDATE OR DELETE ON verification_invitations FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN verification_invitations.recipient_email IS '@pii:high';
COMMENT ON COLUMN verification_invitations.token_hash IS 'Solo hash; nunca almacenar token en claro.';

-- Extensión verification_sessions (IDSecure enlaza instancia + invitación)
ALTER TABLE verification_sessions
  ADD COLUMN IF NOT EXISTS procedure_instance_id uuid NULL,
  ADD COLUMN IF NOT EXISTS verification_invitation_id uuid NULL,
  ADD COLUMN IF NOT EXISTS participant_role text NULL,
  ADD COLUMN IF NOT EXISTS current_step smallint NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS provider text NOT NULL DEFAULT 'vertex_ai';
COMMENT ON COLUMN verification_sessions.provider IS 'vertex_ai | verifik | mock';

ALTER TABLE verification_sessions
  ADD CONSTRAINT fk_verification_sessions_invitations
    FOREIGN KEY (verification_invitation_id) REFERENCES verification_invitations (id) ON UPDATE CASCADE ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_verification_sessions_invitation_id ON verification_sessions (verification_invitation_id);
CREATE INDEX IF NOT EXISTS ix_verification_sessions_procedure_instance_id ON verification_sessions (tenant_id, procedure_instance_id);

-- -------------------------------------------------------------------------------------
-- verification_session_steps — progreso secuencial del stepper (RF-2)
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_session_steps (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  step_number             smallint    NOT NULL CHECK (step_number BETWEEN 1 AND 4),
  step_code               text        NOT NULL CHECK (step_code IN ('document_capture','selfie','liveness','signature')),
  status                  text        NOT NULL DEFAULT 'pending'
                                      CHECK (status IN ('pending','in_progress','completed','failed','skipped')),
  completed_at            timestamptz NULL,
  metadata                jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_session_steps_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_session_steps_sessions FOREIGN KEY (verification_session_id) REFERENCES verification_sessions (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_session_steps_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_session_steps_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_verification_session_steps_session_step UNIQUE (verification_session_id, step_number)
);
CREATE INDEX ix_verification_session_steps_session_id ON verification_session_steps (verification_session_id);
ALTER TABLE verification_session_steps ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_session_steps
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_session_steps_before_update_row_version BEFORE UPDATE ON verification_session_steps FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_verification_session_steps_audit AFTER INSERT OR UPDATE OR DELETE ON verification_session_steps FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- evidence_type ampliado (firma digital)
ALTER TABLE verification_evidences DROP CONSTRAINT IF EXISTS verification_evidences_evidence_type_check;
ALTER TABLE verification_evidences ADD CONSTRAINT verification_evidences_evidence_type_check
  CHECK (evidence_type IN ('liveness','document_front','document_back','selfie','signature_canvas'));

-- -------------------------------------------------------------------------------------
-- verification_ocr_results — extracción OCR (RF-3.1) @pii:high
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_ocr_results (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  extracted_fields        jsonb       NOT NULL DEFAULT '{}'::jsonb,
  confidence_scores       jsonb       NOT NULL DEFAULT '{}'::jsonb,
  raw_provider_response   jsonb       NULL,
  processed_at            timestamptz NOT NULL DEFAULT now(),
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_ocr_results_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_ocr_results_sessions FOREIGN KEY (verification_session_id) REFERENCES verification_sessions (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_ocr_results_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_verification_ocr_results_session_id ON verification_ocr_results (verification_session_id);
ALTER TABLE verification_ocr_results ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_ocr_results
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_ocr_results_audit AFTER INSERT OR UPDATE OR DELETE ON verification_ocr_results FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE verification_ocr_results IS '@context:identity_verification @pii:high OCR de documento de identidad.';

-- -------------------------------------------------------------------------------------
-- verification_ai_verdicts — dictamen Approved/Rejected (RF-3.5)
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_ai_verdicts (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  verdict                 text        NOT NULL CHECK (verdict IN ('approved','rejected','manual_review')),
  biometric_score         numeric(5,4) NULL,
  liveness_passed         boolean     NULL,
  cross_match_passed      boolean     NULL,
  failure_reasons         jsonb       NOT NULL DEFAULT '[]'::jsonb,
  dictamen                jsonb       NOT NULL DEFAULT '{}'::jsonb,
  decided_at              timestamptz NOT NULL DEFAULT now(),
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_ai_verdicts_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_ai_verdicts_sessions FOREIGN KEY (verification_session_id) REFERENCES verification_sessions (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_ai_verdicts_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_verification_ai_verdicts_session_id ON verification_ai_verdicts (verification_session_id);
ALTER TABLE verification_ai_verdicts ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_ai_verdicts
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_ai_verdicts_audit AFTER INSERT OR UPDATE OR DELETE ON verification_ai_verdicts FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- verification_manual_overrides — override auditado (RF-4.7)
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_manual_overrides (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  previous_verdict        text        NOT NULL,
  new_verdict             text        NOT NULL CHECK (new_verdict IN ('approved','rejected')),
  reason                  text        NOT NULL,
  overridden_by           uuid        NOT NULL,
  overridden_at           timestamptz NOT NULL DEFAULT now(),
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_manual_overrides_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_manual_overrides_sessions FOREIGN KEY (verification_session_id) REFERENCES verification_sessions (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_verification_manual_overrides_users FOREIGN KEY (overridden_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_manual_overrides_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_verification_manual_overrides_session_id ON verification_manual_overrides (verification_session_id);
ALTER TABLE verification_manual_overrides ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_manual_overrides
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_verification_manual_overrides_audit AFTER INSERT OR UPDATE OR DELETE ON verification_manual_overrides FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- verification_email_templates — SMTP HTML por tipo de trámite (RF-1.5)
-- -------------------------------------------------------------------------------------
CREATE TABLE verification_email_templates (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NULL,       -- NULL = plantilla global SA
  template_key            text        NOT NULL,
  procedure_type_id       uuid        NULL,
  subject                 text        NOT NULL,
  html_body               text        NOT NULL,
  locale                  text        NOT NULL DEFAULT 'es-CO',
  is_active               boolean     NOT NULL DEFAULT true,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  deleted_at              timestamptz NULL,
  deleted_by              uuid        NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_verification_email_templates_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_verification_email_templates_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_verification_email_templates_key UNIQUE (tenant_id, template_key, procedure_type_id, locale)
);
CREATE INDEX ix_verification_email_templates_lookup ON verification_email_templates (tenant_id, template_key) WHERE deleted_at IS NULL;
ALTER TABLE verification_email_templates ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_email_templates
  USING (tenant_id IS NULL OR tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id IS NULL OR tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- Bitácora de eventos IDSecure (idempotencia TRAMITE_CREATED — RF-1.7)
CREATE TABLE verification_domain_events (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  event_type              text        NOT NULL,
  procedure_instance_id   uuid        NOT NULL,
  payload                 jsonb       NOT NULL DEFAULT '{}'::jsonb,
  idempotency_key         text        NOT NULL,
  processed_at            timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_verification_domain_events_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT uq_verification_domain_events_idempotency UNIQUE (tenant_id, idempotency_key)
);
CREATE INDEX ix_verification_domain_events_instance ON verification_domain_events (tenant_id, procedure_instance_id);
ALTER TABLE verification_domain_events ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON verification_domain_events
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
COMMENT ON TABLE verification_domain_events IS 'Append-only; sin soft-delete. Idempotencia de TRAMITE_CREATED.';

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS identity_verification.verification_domain_events,
--   identity_verification.verification_email_templates,
--   identity_verification.verification_manual_overrides,
--   identity_verification.verification_ai_verdicts,
--   identity_verification.verification_ocr_results,
--   identity_verification.verification_session_steps,
--   identity_verification.verification_invitations,
--   identity_verification.procedure_type_verification_configs CASCADE;
-- ALTER TABLE identity_verification.verification_sessions DROP COLUMN IF EXISTS procedure_instance_id, ...;


-- >>> 80-procedures.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 80 — Runtime de trámites (schema procedures)
-- Instancia radicada con SNAPSHOT inmutable de la config resuelta (ADR-0010).
-- Editable solo en 'borrador'. Transiciones de estado validadas por trigger.
-- =====================================================================================
SET search_path TO procedures, public;

-- Guard de transiciones de la máquina de estados (#9408 FR-5, #9409 CF-G4)
CREATE OR REPLACE FUNCTION procedures.validate_state_transition()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
DECLARE allowed text[];
BEGIN
  IF NEW.state = OLD.state THEN RETURN NEW; END IF;
  allowed := CASE OLD.state
    WHEN 'borrador'     THEN ARRAY['asignado','q_validacion','anulado']
    WHEN 'asignado'     THEN ARRAY['q_validacion','borrador','anulado']
    WHEN 'q_validacion' THEN ARRAY['pendiente','rechazado','borrador','anulado']
    WHEN 'pendiente'    THEN ARRAY['aprobado','rechazado','anulado']
    WHEN 'aprobado'     THEN ARRAY['enviado','anulado']
    WHEN 'enviado'      THEN ARRAY['entregado','rechazado']
    WHEN 'rechazado'    THEN ARRAY['borrador','anulado']
    ELSE ARRAY[]::text[]  -- entregado / anulado = terminales
  END;
  IF NOT (NEW.state = ANY(allowed)) THEN
    RAISE EXCEPTION 'Transición de estado inválida: % -> %', OLD.state, NEW.state
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END;
$$;

-- -------------------------------------------------------------------------------------
-- procedure_instances — trámite radicado con snapshot de config inmutable
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_instances (
  id                  uuid          PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id           uuid          NOT NULL,
  procedure_type_id   uuid          NOT NULL,
  traffic_agency_id   uuid          NULL,
  reference_number    text          NOT NULL,
  state               text          NOT NULL DEFAULT 'borrador'
                      CHECK (state IN ('borrador','asignado','q_validacion','pendiente','aprobado','enviado','entregado','rechazado','anulado')),
  config_snapshot     jsonb         NOT NULL DEFAULT '{}'::jsonb,  -- config resuelta congelada (aristas/campos/docs/reglas)
  config_schema_version integer     NOT NULL DEFAULT 1,
  assigned_to_user_id uuid          NULL,
  radicated_at        timestamptz   NULL,
  completed_at        timestamptz   NULL,
  total_amount        numeric(15,2) NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
  currency_code       char(3)       NOT NULL DEFAULT 'COP',
  metadata            jsonb         NOT NULL DEFAULT '{}'::jsonb,
  created_at          timestamptz   NOT NULL DEFAULT now(),
  created_by          uuid          NOT NULL,
  updated_at          timestamptz   NOT NULL DEFAULT now(),
  updated_by          uuid          NOT NULL,
  deleted_at          timestamptz   NULL,
  deleted_by          uuid          NULL,
  row_version         integer       NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_instances_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_instances_types FOREIGN KEY (procedure_type_id) REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_instances_traffic_agencies FOREIGN KEY (traffic_agency_id) REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_instances_users_assignee FOREIGN KEY (assigned_to_user_id) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_instances_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_instances_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_procedure_instances_reference_number UNIQUE (tenant_id, reference_number)
);
CREATE INDEX ix_procedure_instances_tenant_id_state ON procedure_instances (tenant_id, state) WHERE deleted_at IS NULL;
CREATE INDEX ix_procedure_instances_tenant_id_agency_created ON procedure_instances (tenant_id, traffic_agency_id, created_at DESC);
CREATE INDEX ix_procedure_instances_tenant_id_assignee ON procedure_instances (tenant_id, assigned_to_user_id);
CREATE INDEX ix_procedure_instances_procedure_type_id ON procedure_instances (procedure_type_id);
CREATE INDEX ix_procedure_instances_created_at ON procedure_instances (created_at DESC);
ALTER TABLE procedure_instances ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_instances
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_instances_before_update_state
  BEFORE UPDATE ON procedure_instances FOR EACH ROW EXECUTE FUNCTION procedures.validate_state_transition();
CREATE TRIGGER tr_procedure_instances_before_update_row_version
  BEFORE UPDATE ON procedure_instances FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_procedure_instances_audit
  AFTER INSERT OR UPDATE OR DELETE ON procedure_instances FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON TABLE procedure_instances IS '@context:procedures @entity:trámite Instancia radicada; config_snapshot inmutable (ADR-0010).';
COMMENT ON COLUMN procedure_instances.config_snapshot IS '@semi-structured Config resuelta y congelada al radicar; el runtime no depende de ediciones posteriores.';

-- -------------------------------------------------------------------------------------
-- procedure_field_values — valores capturados (normalizado, Híbrido)
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_field_values (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  edge_role             text        NULL,
  field_key             text        NOT NULL,
  value                 jsonb       NOT NULL DEFAULT 'null'::jsonb,
  data_type             text        NOT NULL,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid        NOT NULL,
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            uuid        NOT NULL,
  deleted_at            timestamptz NULL,
  deleted_by            uuid        NULL,
  row_version           integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_field_values_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_field_values_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_field_values_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_field_values_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE UNIQUE INDEX uq_procedure_field_values_instance_edge_field
  ON procedure_field_values (procedure_instance_id, COALESCE(edge_role,''), field_key) WHERE deleted_at IS NULL;
CREATE INDEX ix_procedure_field_values_tenant_id_instance ON procedure_field_values (tenant_id, procedure_instance_id);
ALTER TABLE procedure_field_values ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_field_values
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_field_values_before_update_row_version BEFORE UPDATE ON procedure_field_values FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

-- -------------------------------------------------------------------------------------
-- procedure_actors — actores por arista (#9409). Jurídica → sub-actor representante legal.
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_actors (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  edge_role             text        NOT NULL CHECK (edge_role IN ('propietario','comprador','locatario')),
  person_kind           text        NOT NULL CHECK (person_kind IN ('natural','juridica')),
  document_type_id      uuid        NOT NULL,
  document_number       text        NOT NULL,
  full_name             text        NULL,
  email                 text        NULL,
  phone                 text        NULL,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid        NOT NULL,
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            uuid        NOT NULL,
  deleted_at            timestamptz NULL,
  deleted_by            uuid        NULL,
  row_version           integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_actors_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_actors_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_actors_document_types FOREIGN KEY (document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_actors_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_actors_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_procedure_actors_instance_edge UNIQUE (procedure_instance_id, edge_role)
);
CREATE INDEX ix_procedure_actors_tenant_id_instance ON procedure_actors (tenant_id, procedure_instance_id);
ALTER TABLE procedure_actors ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_actors
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_actors_before_update_row_version BEFORE UPDATE ON procedure_actors FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_procedure_actors_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_actors FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN procedure_actors.document_number IS '@pii:high';
COMMENT ON COLUMN procedure_actors.full_name IS '@pii:medium';

-- procedure_actor_representatives — representante legal (jurídica). 1:1 con actor jurídico.
CREATE TABLE procedure_actor_representatives (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id           uuid        NOT NULL,
  procedure_actor_id  uuid        NOT NULL,
  document_type_id    uuid        NOT NULL,
  document_number     text        NOT NULL,
  full_name           text        NOT NULL,
  email               text        NULL,
  phone               text        NULL,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid        NOT NULL,
  updated_at          timestamptz NOT NULL DEFAULT now(),
  updated_by          uuid        NOT NULL,
  deleted_at          timestamptz NULL,
  deleted_by          uuid        NULL,
  row_version         integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_actor_representatives_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_actor_representatives_actors FOREIGN KEY (procedure_actor_id) REFERENCES procedure_actors (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_actor_representatives_document_types FOREIGN KEY (document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_actor_representatives_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_actor_representatives_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_actor_representatives_actor UNIQUE (procedure_actor_id)
);
CREATE INDEX ix_actor_representatives_tenant_id ON procedure_actor_representatives (tenant_id);
ALTER TABLE procedure_actor_representatives ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_actor_representatives
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_actor_representatives_before_update_row_version BEFORE UPDATE ON procedure_actor_representatives FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_actor_representatives_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_actor_representatives FOR EACH ROW EXECUTE FUNCTION audit.log_change();
COMMENT ON COLUMN procedure_actor_representatives.document_number IS '@pii:high';

-- -------------------------------------------------------------------------------------
-- procedure_vehicles — arista vehículo (placa o VIN). 1:1 con instancia.
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_vehicles (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  vehicle_subkind       text        NOT NULL CHECK (vehicle_subkind IN ('automovil','moto','otro','maquinaria','remolque')),
  license_plate         text        NULL,
  vin                   text        NULL,
  make_id               uuid        NULL,
  line_id               uuid        NULL,
  class_id              uuid        NULL,
  color_id              uuid        NULL,
  fuel_id               uuid        NULL,
  runt_snapshot         jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid        NOT NULL,
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            uuid        NOT NULL,
  deleted_at            timestamptz NULL,
  deleted_by            uuid        NULL,
  row_version           integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_vehicles_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_vehicles_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_vehicles_makes FOREIGN KEY (make_id) REFERENCES catalogs.vehicle_makes (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_vehicles_lines FOREIGN KEY (line_id) REFERENCES catalogs.vehicle_lines (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_vehicles_classes FOREIGN KEY (class_id) REFERENCES catalogs.vehicle_classes (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_vehicles_colors FOREIGN KEY (color_id) REFERENCES catalogs.colors (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_vehicles_fuel FOREIGN KEY (fuel_id) REFERENCES catalogs.fuel_types (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_vehicles_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_vehicles_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_procedure_vehicles_instance UNIQUE (procedure_instance_id),
  CONSTRAINT ck_procedure_vehicles_plate_or_vin CHECK (license_plate IS NOT NULL OR vin IS NOT NULL),
  CONSTRAINT ck_procedure_vehicles_plate_format CHECK (
    license_plate IS NULL
    OR license_plate ~ '^[A-Z]{3}[0-9]{3}$'
    OR license_plate ~ '^[A-Z]{3}[0-9]{2}[A-Z]$'
    OR license_plate ~ '^[A-Z]{1,2}[0-9]{4,5}$'
  )
);
CREATE INDEX ix_procedure_vehicles_tenant_id ON procedure_vehicles (tenant_id);
CREATE INDEX ix_procedure_vehicles_license_plate ON procedure_vehicles (license_plate) WHERE license_plate IS NOT NULL;
CREATE INDEX ix_procedure_vehicles_make_id ON procedure_vehicles (make_id);
CREATE INDEX ix_procedure_vehicles_line_id ON procedure_vehicles (line_id);
CREATE INDEX ix_procedure_vehicles_class_id ON procedure_vehicles (class_id);
CREATE INDEX ix_procedure_vehicles_color_id ON procedure_vehicles (color_id);
CREATE INDEX ix_procedure_vehicles_fuel_id ON procedure_vehicles (fuel_id);
ALTER TABLE procedure_vehicles ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_vehicles
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_vehicles_before_update_row_version BEFORE UPDATE ON procedure_vehicles FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_procedure_vehicles_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_vehicles FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- procedure_query_results — SNAPSHOT de resultados de consultas externas (1 por fuente)
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_query_results (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  query_connector_code  text        NOT NULL,
  edge_role             text        NULL,
  source                text        NOT NULL,
  status                text        NOT NULL CHECK (status IN ('ok','failed','partial')),
  result                jsonb       NOT NULL DEFAULT '{}'::jsonb,
  requested_at          timestamptz NOT NULL DEFAULT now(),
  responded_at          timestamptz NULL,
  integration_call_id   uuid        NULL,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid        NOT NULL,
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            uuid        NOT NULL,
  deleted_at            timestamptz NULL,
  deleted_by            uuid        NULL,
  row_version           integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_query_results_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_query_results_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_query_results_calls FOREIGN KEY (integration_call_id) REFERENCES integrations.external_query_calls (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_query_results_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_query_results_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
-- Unicidad por (instancia, fuente, arista) tratando edge_role NULL como '' (expresión → índice único)
CREATE UNIQUE INDEX uq_procedure_query_results_instance_source
  ON procedure_query_results (procedure_instance_id, query_connector_code, COALESCE(edge_role,'')) WHERE deleted_at IS NULL;
CREATE INDEX ix_procedure_query_results_tenant_id_instance ON procedure_query_results (tenant_id, procedure_instance_id);
ALTER TABLE procedure_query_results ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_query_results
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_query_results_before_update_row_version BEFORE UPDATE ON procedure_query_results FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
COMMENT ON TABLE procedure_query_results IS '@context:procedures Snapshot por fuente (fallo aislado #9409 CF-D7). Re-ejecución solo en borrador (app).';

-- -------------------------------------------------------------------------------------
-- procedure_documents — adjuntos (upload) + generados (auto_generated)
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_documents (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  kind                  text        NOT NULL CHECK (kind IN ('upload','auto_generated')),
  document_type_id      uuid        NOT NULL,
  edge_role             text        NULL,
  actor_id              uuid        NULL,
  file_id               uuid        NULL,
  template_version_id   uuid        NULL,
  data_snapshot         jsonb       NOT NULL DEFAULT '{}'::jsonb,  -- valores de marcadores resueltos (doc generado)
  is_required           boolean     NOT NULL DEFAULT true,
  display_order         integer     NOT NULL DEFAULT 0,
  status                text        NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','uploaded','generated','rejected')),
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid        NOT NULL,
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            uuid        NOT NULL,
  deleted_at            timestamptz NULL,
  deleted_by            uuid        NULL,
  row_version           integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_procedure_documents_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_documents_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_documents_document_types FOREIGN KEY (document_type_id) REFERENCES catalogs.document_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_documents_actors FOREIGN KEY (actor_id) REFERENCES procedure_actors (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_documents_files FOREIGN KEY (file_id) REFERENCES files.files (id) ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_procedure_documents_template_versions FOREIGN KEY (template_version_id) REFERENCES procedures_config.document_template_versions (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_documents_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_procedure_documents_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_procedure_documents_tenant_id_instance ON procedure_documents (tenant_id, procedure_instance_id);
CREATE INDEX ix_procedure_documents_file_id ON procedure_documents (file_id);
CREATE INDEX ix_procedure_documents_template_version_id ON procedure_documents (template_version_id);
CREATE INDEX ix_procedure_documents_document_type_id ON procedure_documents (document_type_id);
CREATE INDEX ix_procedure_documents_actor_id ON procedure_documents (actor_id);
ALTER TABLE procedure_documents ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_documents
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_procedure_documents_before_update_row_version BEFORE UPDATE ON procedure_documents FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_procedure_documents_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_documents FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- -------------------------------------------------------------------------------------
-- procedure_state_history — historial de transiciones (append-only)
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_state_history (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_instance_id uuid        NOT NULL,
  from_state            text        NULL,
  to_state              text        NOT NULL,
  reason                text        NULL,
  metadata              jsonb       NOT NULL DEFAULT '{}'::jsonb,
  changed_by            uuid        NOT NULL,
  changed_at            timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_procedure_state_history_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_state_history_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_procedure_state_history_users FOREIGN KEY (changed_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT
);
CREATE INDEX ix_procedure_state_history_tenant_id_instance ON procedure_state_history (tenant_id, procedure_instance_id, changed_at DESC);
ALTER TABLE procedure_state_history ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_state_history
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- -------------------------------------------------------------------------------------
-- procedure_identity_validations — enlace a validación de identidad reutilizable
-- -------------------------------------------------------------------------------------
CREATE TABLE procedure_identity_validations (
  id                      uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id               uuid        NOT NULL,
  procedure_instance_id   uuid        NOT NULL,
  verification_session_id uuid        NOT NULL,
  actor_id                uuid        NULL,
  verdict                 text        NULL,
  linked_at               timestamptz NOT NULL DEFAULT now(),
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid        NOT NULL,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid        NOT NULL,
  deleted_at              timestamptz NULL,
  deleted_by              uuid        NULL,
  row_version             integer     NOT NULL DEFAULT 1,
  CONSTRAINT fk_proc_id_validations_tenants FOREIGN KEY (tenant_id) REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_proc_id_validations_instances FOREIGN KEY (procedure_instance_id) REFERENCES procedure_instances (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_proc_id_validations_sessions FOREIGN KEY (verification_session_id) REFERENCES identity_verification.verification_sessions (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_proc_id_validations_actors FOREIGN KEY (actor_id) REFERENCES procedure_actors (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_proc_id_validations_users_creator FOREIGN KEY (created_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_proc_id_validations_users_updater FOREIGN KEY (updated_by) REFERENCES identity.users (id) ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT uq_proc_id_validations_instance_actor UNIQUE (procedure_instance_id, actor_id)
);
CREATE INDEX ix_proc_id_validations_tenant_id_instance ON procedure_identity_validations (tenant_id, procedure_instance_id);
CREATE INDEX ix_proc_id_validations_session_id ON procedure_identity_validations (verification_session_id);
CREATE INDEX ix_proc_id_validations_actor_id ON procedure_identity_validations (actor_id);
ALTER TABLE procedure_identity_validations ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON procedure_identity_validations
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
CREATE TRIGGER tr_proc_id_validations_before_update_row_version BEFORE UPDATE ON procedure_identity_validations FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();
CREATE TRIGGER tr_proc_id_validations_audit AFTER INSERT OR UPDATE OR DELETE ON procedure_identity_validations FOR EACH ROW EXECUTE FUNCTION audit.log_change();

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS procedures.procedure_identity_validations, procedures.procedure_state_history,
--   procedures.procedure_documents, procedures.procedure_query_results, procedures.procedure_vehicles,
--   procedures.procedure_actor_representatives, procedures.procedure_actors,
--   procedures.procedure_field_values, procedures.procedure_instances CASCADE;
-- DROP FUNCTION IF EXISTS procedures.validate_state_transition();


-- >>> 90-dashboard.sql

-- =====================================================================================
-- FLIT 2.0 · DDL 90 — Dashboard read model (schema dashboard) (#9369)
-- Estrategia: vistas SQL con security_invoker (la RLS de procedure_instances aplica por usuario;
-- SuperAdmin ve cross-tenant vía identity.is_super_admin()). MV opcional para alto volumen.
-- KPIs por estado: Total, Borrador, Asignado, Enviado, Entregado, Rechazado, Anulado (+ otros).
-- =====================================================================================
SET search_path TO dashboard, public;

-- v_procedure_kpis — conteos por tenant / OT / estado / día (filtrable por rango de fechas)
CREATE VIEW dashboard.v_procedure_kpis
WITH (security_invoker = true) AS
SELECT
  pi.tenant_id,
  pi.traffic_agency_id,
  pi.state,
  date_trunc('day', pi.created_at)::date AS day,
  count(*) AS total
FROM procedures.procedure_instances pi
WHERE pi.deleted_at IS NULL
GROUP BY pi.tenant_id, pi.traffic_agency_id, pi.state, date_trunc('day', pi.created_at)::date;
COMMENT ON VIEW dashboard.v_procedure_kpis IS '@context:dashboard KPIs por estado/OT/día (#9369). RLS aplicada vía security_invoker.';

-- v_user_productivity — productividad por usuario (Radicador/Gestor/Operario) y estado
CREATE VIEW dashboard.v_user_productivity
WITH (security_invoker = true) AS
SELECT
  pi.tenant_id,
  pi.assigned_to_user_id,
  pi.state,
  count(*) AS total
FROM procedures.procedure_instances pi
WHERE pi.deleted_at IS NULL
GROUP BY pi.tenant_id, pi.assigned_to_user_id, pi.state;
COMMENT ON VIEW dashboard.v_user_productivity IS '@context:dashboard Tabla de productividad por usuario (#9369 reporte PDF).';

-- v_ot_distribution — distribución por Organismo de Tránsito (cantidad + base para %)
CREATE VIEW dashboard.v_ot_distribution
WITH (security_invoker = true) AS
SELECT
  pi.tenant_id,
  pi.traffic_agency_id,
  count(*) AS total
FROM procedures.procedure_instances pi
WHERE pi.deleted_at IS NULL
GROUP BY pi.tenant_id, pi.traffic_agency_id;
COMMENT ON VIEW dashboard.v_ot_distribution IS '@context:dashboard Distribución por secretaría (#9369). El % se calcula en la capa de reporte.';

-- -------------------------------------------------------------------------------------
-- Escala (opcional, diferido): MV de KPIs refrescada por evento + bitácora de refresco.
-- La MV NO aplica RLS (datos precomputados): la app filtra por tenant_id, o la consume el
-- consolidado SuperAdmin. Habilitar cuando el volumen lo justifique (ver supuesto #8 del plan).
-- -------------------------------------------------------------------------------------
CREATE MATERIALIZED VIEW dashboard.mv_procedure_kpis AS
SELECT
  pi.tenant_id,
  pi.traffic_agency_id,
  pi.state,
  date_trunc('day', pi.created_at)::date AS day,
  count(*) AS total
FROM procedures.procedure_instances pi
WHERE pi.deleted_at IS NULL
GROUP BY pi.tenant_id, pi.traffic_agency_id, pi.state, date_trunc('day', pi.created_at)::date
WITH NO DATA;
CREATE UNIQUE INDEX uq_mv_procedure_kpis_key
  ON dashboard.mv_procedure_kpis (tenant_id, COALESCE(traffic_agency_id,'00000000-0000-7000-8000-000000000000'), state, day);
COMMENT ON MATERIALIZED VIEW dashboard.mv_procedure_kpis IS
  '@context:dashboard MV opcional para alto volumen. REFRESH MATERIALIZED VIEW CONCURRENTLY por evento/cron.';

CREATE TABLE dashboard.read_model_refresh_log (
  id           uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  view_name    text        NOT NULL,
  refreshed_at timestamptz NOT NULL DEFAULT now(),
  row_count    bigint      NULL,
  duration_ms  integer     NULL
);
CREATE INDEX ix_read_model_refresh_log_view_name ON dashboard.read_model_refresh_log (view_name, refreshed_at DESC);

-- =====================================================================================
-- DOWN (reversa)
-- =====================================================================================
-- DROP TABLE IF EXISTS dashboard.read_model_refresh_log;
-- DROP MATERIALIZED VIEW IF EXISTS dashboard.mv_procedure_kpis;
-- DROP VIEW IF EXISTS dashboard.v_ot_distribution, dashboard.v_user_productivity, dashboard.v_procedure_kpis;

