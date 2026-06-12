-- HU #9694 — procedures_config.rule_execution_logs (bitácora evaluaciones reales de reglas)
-- Prerrequisito: procedures_config.rules (AddProceduresConfigRulesRgl01).

CREATE TABLE IF NOT EXISTS procedures_config.rule_execution_logs (
  id                    uuid        PRIMARY KEY DEFAULT public.uuidv7(),
  tenant_id             uuid        NOT NULL,
  procedure_type_id     uuid        NOT NULL,
  procedure_instance_id uuid        NULL,
  payload               jsonb       NOT NULL DEFAULT '{}'::jsonb,
  matched_rules         jsonb       NOT NULL DEFAULT '[]'::jsonb,
  result                jsonb       NOT NULL DEFAULT '{}'::jsonb,
  evaluated_at          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT fk_rule_execution_logs_tenants FOREIGN KEY (tenant_id)
    REFERENCES identity.tenants (id) ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_rule_execution_logs_types FOREIGN KEY (procedure_type_id)
    REFERENCES procedures_config.procedure_types (id) ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_rule_execution_logs_tenant_type_evaluated_at
  ON procedures_config.rule_execution_logs (tenant_id, procedure_type_id, evaluated_at DESC);

CREATE INDEX IF NOT EXISTS ix_rule_execution_logs_tenant_instance
  ON procedures_config.rule_execution_logs (tenant_id, procedure_instance_id)
  WHERE procedure_instance_id IS NOT NULL;

ALTER TABLE procedures_config.rule_execution_logs ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON procedures_config.rule_execution_logs;
CREATE POLICY tenant_isolation ON procedures_config.rule_execution_logs
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid OR identity.is_super_admin())
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
