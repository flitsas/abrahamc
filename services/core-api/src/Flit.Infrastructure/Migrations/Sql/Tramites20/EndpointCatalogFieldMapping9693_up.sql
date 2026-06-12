-- HU #9693 — field_mapping jsonb en endpoint_catalog (mapeo semántico APIs)
ALTER TABLE procedures_config.endpoint_catalog
  ADD COLUMN IF NOT EXISTS field_mapping jsonb NOT NULL DEFAULT '{}'::jsonb;

COMMENT ON COLUMN procedures_config.endpoint_catalog.field_mapping IS
  '@context:procedures_config Mapeo semántico respuesta API → claves de formulario (dot-path).';
