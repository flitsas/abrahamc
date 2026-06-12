-- HU #9732 Feature — runtime: copropiedad + permiso SuperMaestro (#10079/#10081)
SET search_path TO procedures, identity, public;

ALTER TABLE procedure_actors
  ADD COLUMN IF NOT EXISTS ownership_percentage numeric(5,2) NULL,
  ADD COLUMN IF NOT EXISTS owner_sequence smallint NOT NULL DEFAULT 0;

ALTER TABLE procedure_actors DROP CONSTRAINT IF EXISTS uq_procedure_actors_instance_edge;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'uq_procedure_actors_instance_edge_seq'
      AND conrelid = 'procedures.procedure_actors'::regclass
  ) THEN
    ALTER TABLE procedure_actors
      ADD CONSTRAINT uq_procedure_actors_instance_edge_seq
      UNIQUE (procedure_instance_id, edge_role, owner_sequence);
  END IF;
END $$;

INSERT INTO permissions (slug, module, action, description, is_system) VALUES
  ('tramites.admin.maestro', 'tramites', 'administrar', 'Transición forzada de estado (SuperMaestro)', false)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_by, updated_by)
SELECT NULL, r.id, p.id,
       '00000000-0000-7000-8000-000000000001', '00000000-0000-7000-8000-000000000001'
FROM roles r
CROSS JOIN permissions p
WHERE r.slug = 'super-admin'
  AND p.slug = 'tramites.admin.maestro'
ON CONFLICT (role_id, permission_id) DO NOTHING;
