-- Rollback HU #9437 RGL-01 (solo objetos de este script; no toca shell ni DDL 50 completo).

DROP TABLE IF EXISTS procedures_config.rules;
DROP TABLE IF EXISTS procedures_config.endpoint_catalog;
DROP TABLE IF EXISTS procedures_config.procedure_types;
DROP TABLE IF EXISTS procedures_config.procedure_families;
DROP FUNCTION IF EXISTS public.is_valid_rule_actions(jsonb);
DROP FUNCTION IF EXISTS public.is_valid_rule_condition(jsonb);
