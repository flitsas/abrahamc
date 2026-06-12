-- HU #9732 Feature — rollback runtime (#10079/#10081)
SET search_path TO procedures, identity, public;

DELETE FROM role_permissions rp
USING permissions p
WHERE rp.permission_id = p.id
  AND p.slug = 'tramites.admin.maestro';

DELETE FROM permissions WHERE slug = 'tramites.admin.maestro';

ALTER TABLE procedure_actors DROP CONSTRAINT IF EXISTS uq_procedure_actors_instance_edge_seq;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'uq_procedure_actors_instance_edge'
      AND conrelid = 'procedures.procedure_actors'::regclass
  ) THEN
    ALTER TABLE procedure_actors
      ADD CONSTRAINT uq_procedure_actors_instance_edge
      UNIQUE (procedure_instance_id, edge_role);
  END IF;
END $$;

ALTER TABLE procedure_actors
  DROP COLUMN IF EXISTS ownership_percentage,
  DROP COLUMN IF EXISTS owner_sequence;
