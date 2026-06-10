-- HU #9438 RGL-02 — integrations.endpoint_call_log (bitácora invocaciones desde reglas)
-- Prerrequisito: identity.tenants (AddProceduresConfigRulesRgl01).

CREATE SCHEMA IF NOT EXISTS integrations;

CREATE TABLE IF NOT EXISTS integrations.endpoint_call_log (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  endpoint_code         text        NOT NULL,
  procedure_instance_id uuid        NULL,
  request               jsonb       NOT NULL DEFAULT '{}'::jsonb,
  response              jsonb       NULL,
  http_status           integer     NULL,
  succeeded             boolean     NOT NULL DEFAULT false,
  called_at             timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_endpoint_call_log_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_endpoint_call_log_tenant_id_called_at
  ON integrations.endpoint_call_log (tenant_id, called_at DESC);

ALTER TABLE integrations.endpoint_call_log ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON integrations.endpoint_call_log;
CREATE POLICY tenant_isolation ON integrations.endpoint_call_log
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
