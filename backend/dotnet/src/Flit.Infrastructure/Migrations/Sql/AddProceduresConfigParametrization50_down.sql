-- Rollback AddProceduresConfigParametrization50 (#9408 #9409 #9410)
-- No elimina rules/endpoint_catalog/procedure_families/procedure_types (RGL-01 #9437).

DROP TABLE IF EXISTS procedures_config.procedure_type_activations;
DROP TABLE IF EXISTS procedures_config.required_documents;
DROP TABLE IF EXISTS procedures_config.document_template_versions;
DROP TABLE IF EXISTS procedures_config.document_templates;
DROP TABLE IF EXISTS procedures_config.procedure_type_query_configs;
DROP TABLE IF EXISTS procedures_config.query_connectors;
DROP TABLE IF EXISTS procedures_config.form_fields;
DROP TABLE IF EXISTS procedures_config.form_sections;
DROP TABLE IF EXISTS procedures_config.procedure_type_edges;
DROP TABLE IF EXISTS procedures_config.edges;
