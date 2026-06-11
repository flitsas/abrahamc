-- HU #9688 — module_key recaudo en company_module_configs
ALTER TABLE companies.company_module_configs
  DROP CONSTRAINT IF EXISTS company_module_configs_module_key_check;

ALTER TABLE companies.company_module_configs
  ADD CONSTRAINT company_module_configs_module_key_check
  CHECK (module_key IN ('registration', 'transfers', 'company', 'runt_contingency', 'recaudo'));
