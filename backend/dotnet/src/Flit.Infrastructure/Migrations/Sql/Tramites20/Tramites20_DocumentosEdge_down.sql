-- Revert documentos edge (DEV only — no restaura edge_id histórico en required_documents).
DELETE FROM procedures_config.procedure_type_edges m
USING procedures_config.edges e, procedures_config.procedure_types t
WHERE m.edge_id = e.id AND e.code = 'documentos';

DELETE FROM procedures_config.edges WHERE code = 'documentos';

ALTER TABLE procedures_config.edges DROP CONSTRAINT IF EXISTS edges_code_check;
ALTER TABLE procedures_config.edges ADD CONSTRAINT edges_code_check
  CHECK (code IN ('vehiculo','propietario','comprador','locatario'));

ALTER TABLE procedures_config.edges DROP CONSTRAINT IF EXISTS edges_edge_kind_check;
ALTER TABLE procedures_config.edges ADD CONSTRAINT edges_edge_kind_check
  CHECK (edge_kind IN ('vehicle','person'));
