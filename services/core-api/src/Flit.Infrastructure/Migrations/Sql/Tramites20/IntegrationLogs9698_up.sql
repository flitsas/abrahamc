-- HU #9698 — bitácora técnica integrations.integration_logs
SET search_path TO integrations, public;

CREATE TABLE IF NOT EXISTS integration_logs (
  id                  uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id           uuid        NOT NULL,
  traffic_agency_id   uuid        NULL,
  provider            text        NOT NULL,
  direction           text        NOT NULL CHECK (direction IN ('inbound','outbound')),
  event_type          text        NULL,
  payload             jsonb       NOT NULL DEFAULT '{}'::jsonb,
  http_status         integer     NULL,
  result              text        NOT NULL DEFAULT 'ok',
  latency_ms          integer     NULL,
  called_at           timestamptz NOT NULL DEFAULT now(),
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid        NOT NULL DEFAULT '00000000-0000-7000-8000-000000000001',
  CONSTRAINT fk_integration_logs_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_integration_logs_agencies FOREIGN KEY (traffic_agency_id)
    REFERENCES ot.traffic_agencies (id) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_integration_logs_tenant_agency_called
  ON integration_logs (tenant_id, traffic_agency_id, called_at DESC);

ALTER TABLE integration_logs ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON integration_logs;
CREATE POLICY tenant_isolation ON integration_logs
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

COMMENT ON TABLE integration_logs IS
  '@context:integrations Bitcora tcnica webhooks/API (#9557 HU #9698).';
