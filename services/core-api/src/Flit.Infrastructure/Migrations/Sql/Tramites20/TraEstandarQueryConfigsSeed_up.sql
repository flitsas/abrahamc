-- HU #9427 — consultas Tabla 2.4 para TRA_ESTANDAR (idempotente, DEV)
DO $$
DECLARE
  v_sys uuid := '00000000-0000-7000-8000-000000000001';
BEGIN
  INSERT INTO procedures_config.procedure_type_query_configs (procedure_type_id, edge_id, query_connector_id, is_mandatory, is_omitible, person_kind_filter, display_order, created_by, updated_by)
  SELECT t.id, e.id, qc.id, q.is_mandatory, q.is_omitible, q.person_kind_filter, q.display_order, v_sys, v_sys
  FROM (VALUES
    ('TRA_ESTANDAR','vehiculo','RUNT',true,false,'any',1),
    ('TRA_ESTANDAR','propietario','RUNT',true,false,'natural',1),
    ('TRA_ESTANDAR','propietario','SIMIT',true,false,'natural',2),
    ('TRA_ESTANDAR','propietario','RUES',true,false,'juridica',3),
    ('TRA_ESTANDAR','propietario','SIMIT',true,false,'juridica',4),
    ('TRA_ESTANDAR','comprador','SIMIT',true,false,'natural',2),
    ('TRA_ESTANDAR','comprador','RUES',true,false,'juridica',3)
  ) AS q(type_code, edge_code, connector_code, is_mandatory, is_omitible, person_kind_filter, display_order)
  JOIN procedures_config.procedure_types t ON t.code = q.type_code
  JOIN procedures_config.edges e ON e.code = q.edge_code
  JOIN procedures_config.procedure_type_edges m ON m.procedure_type_id = t.id AND m.edge_id = e.id
  JOIN procedures_config.query_connectors qc ON qc.code = q.connector_code
  ON CONFLICT (procedure_type_id, edge_id, query_connector_id, person_kind_filter) DO UPDATE SET
    is_mandatory = EXCLUDED.is_mandatory,
    is_omitible = EXCLUDED.is_omitible,
    display_order = EXCLUDED.display_order,
    updated_by = EXCLUDED.updated_by,
    updated_at = now();
END $$;
