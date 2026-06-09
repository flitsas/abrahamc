-- HU #9454 OT-01 — Rollback catálogo OT
SET search_path TO ot, public;
DELETE FROM ot.traffic_agencies WHERE code LIKE 'OT-CAT-%';

INSERT INTO ot.traffic_agencies (code, name, department_name, municipality_name, dane_municipality_code, nit,
  notifier_email, runt_agency_code, mandate_document_applies, virtual_process_applies, requires_preassignment_plate,
  requires_peace_and_safe, allows_runt_approval_queries, external_refs, created_by) VALUES
  ('OT-BUCARAMANGA','DIR TTOyTTE BUCARAMANGA','SANTANDER','BUCARAMANGA','68001','890201222',
   'notificaciones@bucaramanga.gov.co','68001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"68"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-BARBOSA','DIR TTEyTTO MCPAL BARBOSA','ANTIOQUIA','BARBOSA','05079','890980445',
   'asistenteadmsatt@gmail.com','5079000', true,true,true,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"05"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-CARTAGENA','DPTO ADTVO TTOyTTE DIST CARTAGENA','BOLIVAR','CARTAGENA','13001','890480184',
   'notificacionesjudicialesadministrativo@cartagena.gov.co','13001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1,"divipo":"13"}'::jsonb,'00000000-0000-7000-8000-000000000001'),
  ('OT-PASTO','DPTO ADTVO TTOYTTE MCPAL PASTO','NARIÑO','PASTO','52001','8912800003',
   'contactenos@pasto.gov.co','52001000', false,false,false,false,false,
   '{"parint_transfer":1,"parint_registration":1,"parint_otherservice":1}'::jsonb,'00000000-0000-7000-8000-000000000001')
ON CONFLICT (code) DO NOTHING;
RESET search_path;
