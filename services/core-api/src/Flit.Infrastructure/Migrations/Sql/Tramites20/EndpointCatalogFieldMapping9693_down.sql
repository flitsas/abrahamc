-- HU #9693 — revert field_mapping
ALTER TABLE procedures_config.endpoint_catalog
  DROP COLUMN IF EXISTS field_mapping;
