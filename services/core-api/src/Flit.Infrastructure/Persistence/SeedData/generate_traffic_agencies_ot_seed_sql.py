#!/usr/bin/env python3
"""
HU #9454 — Genera SQL idempotente para ot.traffic_agencies desde traffic_secretaries.csv.
Uso: python generate_traffic_agencies_ot_seed_sql.py
Salida: ../../Migrations/Sql/Tramites20/OtTrafficAgenciesCatalogSeed_up.sql
"""

from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

HERE = Path(__file__).parent
CSV_PATH = HERE / "traffic_secretaries.csv"
SQL_DIR = HERE.parent.parent / "Migrations" / "Sql" / "Tramites20"
UP_PATH = SQL_DIR / "OtTrafficAgenciesCatalogSeed_up.sql"
DOWN_PATH = SQL_DIR / "OtTrafficAgenciesCatalogSeed_down.sql"
SYSTEM_USER = "00000000-0000-7000-8000-000000000001"
FOUNDATION_DEMO_CODES = (
    "OT-BUCARAMANGA",
    "OT-BARBOSA",
    "OT-CARTAGENA",
    "OT-PASTO",
)


def sql_str(v: str | None, *, nullable: bool = False) -> str:
    if v is None:
        return "NULL" if nullable else "''"
    s = str(v).strip()
    if not s or s.upper() == "NULL":
        return "NULL" if nullable else "''"
    return "'" + s.replace("'", "''") + "'"


def sql_bool(v: str | None, default: bool = False) -> str:
    if v is None or str(v).strip() in ("", "NULL"):
        return "true" if default else "false"
    return "true" if str(v).strip().lower() in ("true", "t", "1") else "false"


def sql_int(v: str | None, default: int = 0) -> int:
    if v is None or str(v).strip() in ("", "NULL"):
        return default
    try:
        return int(float(str(v).strip()))
    except ValueError:
        return default


def sql_jsonb(obj: dict) -> str:
    return "'" + json.dumps(obj, ensure_ascii=False).replace("'", "''") + "'::jsonb"


def dane_code(raw: str | None) -> str | None:
    if raw is None:
        return None
    s = str(raw).strip()
    if not s or s.upper() == "NULL":
        return None
    return s.zfill(5)[:5]


def build_code(row_id: str, dane: str | None) -> str:
    base = dane if dane else "00000"
    return f"OT-CAT-{base}-{int(row_id):05d}"


def main() -> None:
    if not CSV_PATH.exists():
        print(f"Missing {CSV_PATH}", file=sys.stderr)
        sys.exit(1)

    rows: list[str] = []
    with CSV_PATH.open("r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            legacy_id = row["id"]
            dane = dane_code(row.get("code_dane_municipality"))
            code = build_code(legacy_id, dane)
            external = {
                "parint_transfer": sql_int(row.get("id_parinttrasec_transfer"), 1),
                "parint_registration": sql_int(row.get("id_parinttrasec_registration"), 1),
                "parint_otherservice": sql_int(row.get("id_parinttrasec_otherservice"), 1),
                "legacy_csv_id": int(legacy_id),
            }
            divipo = (row.get("code_divipo") or row.get("department_dane_code") or "").strip()
            if divipo and divipo.upper() != "NULL":
                external["divipo"] = divipo

            cols = (
                sql_str(code),
                sql_str(row["name"]),
                sql_str(row.get("type") or "Organismos de Tránsito"),
                sql_str(row.get("address"), nullable=True),
                sql_str(row.get("phone"), nullable=True),
                sql_str(row.get("department"), nullable=True),
                sql_str(row.get("municipality"), nullable=True),
                sql_str(dane, nullable=True),
                sql_str(row.get("nit_municipality"), nullable=True),
                sql_str((row.get("notifier_email") or "").strip(), nullable=True),
                sql_str(row.get("contact_name"), nullable=True),
                sql_str(row.get("contact_phone"), nullable=True),
                sql_str(row.get("traffic_agency_code"), nullable=True),
                sql_bool(row.get("mandate_document_applies")),
                sql_bool(row.get("virtual_process_applies")),
                sql_bool(row.get("requires_peace_and_safe")),
                sql_bool(row.get("allows_runt_approval_queries")),
                sql_bool(row.get("traffic_secretary_requires_preassignment_plate")),
                sql_bool(row.get("request_issue_date_flag")),
                sql_jsonb(external),
                sql_bool(row.get("is_active_flit"), default=True),
                f"'{SYSTEM_USER}'",
                f"'{SYSTEM_USER}'",
            )
            rows.append("(" + ", ".join(cols) + ")")

    demo_delete = ", ".join(f"'{c}'" for c in FOUNDATION_DEMO_CODES)
    up_lines = [
        "-- HU #9454 OT-01 — Catálogo OT (~370) desde traffic_secretaries.csv",
        "-- AUTO-GENERATED — re-run: python generate_traffic_agencies_ot_seed_sql.py",
        "SET search_path TO ot, public;",
        f"DELETE FROM ot.traffic_agencies WHERE code IN ({demo_delete});",
        "",
        "INSERT INTO ot.traffic_agencies (",
        "  code, name, agency_type, address, phone, department_name, municipality_name,",
        "  dane_municipality_code, nit, notifier_email, contact_name, contact_phone,",
        "  runt_agency_code, mandate_document_applies, virtual_process_applies,",
        "  requires_peace_and_safe, allows_runt_approval_queries, requires_preassignment_plate,",
        "  request_issue_date_flag, external_refs, is_active, created_by, updated_by",
        ") VALUES",
        ",\n".join(rows),
        "ON CONFLICT (code) DO NOTHING;",
        "RESET search_path;",
        "",
    ]

    UP_PATH.parent.mkdir(parents=True, exist_ok=True)
    UP_PATH.write_text("\n".join(up_lines), encoding="utf-8")

    down_lines = [
        "-- HU #9454 OT-01 — Rollback catálogo OT",
        "SET search_path TO ot, public;",
        "DELETE FROM ot.traffic_agencies WHERE code LIKE 'OT-CAT-%';",
        "",
        "INSERT INTO ot.traffic_agencies (code, name, department_name, municipality_name, dane_municipality_code, nit,",
        "  notifier_email, runt_agency_code, mandate_document_applies, virtual_process_applies, requires_preassignment_plate,",
        "  requires_peace_and_safe, allows_runt_approval_queries, external_refs, created_by) VALUES",
        "  ('OT-BUCARAMANGA','DIR TTOyTTE BUCARAMANGA','SANTANDER','BUCARAMANGA','68001','890201222',",
        "   'notificaciones@bucaramanga.gov.co','68001000', false,false,false,false,false,",
        "   '{\"parint_transfer\":1,\"parint_registration\":1,\"parint_otherservice\":1,\"divipo\":\"68\"}'::jsonb,'00000000-0000-7000-8000-000000000001'),",
        "  ('OT-BARBOSA','DIR TTEyTTO MCPAL BARBOSA','ANTIOQUIA','BARBOSA','05079','890980445',",
        "   'asistenteadmsatt@gmail.com','5079000', true,true,true,false,false,",
        "   '{\"parint_transfer\":1,\"parint_registration\":1,\"parint_otherservice\":1,\"divipo\":\"05\"}'::jsonb,'00000000-0000-7000-8000-000000000001'),",
        "  ('OT-CARTAGENA','DPTO ADTVO TTOyTTE DIST CARTAGENA','BOLIVAR','CARTAGENA','13001','890480184',",
        "   'notificacionesjudicialesadministrativo@cartagena.gov.co','13001000', false,false,false,false,false,",
        "   '{\"parint_transfer\":1,\"parint_registration\":1,\"parint_otherservice\":1,\"divipo\":\"13\"}'::jsonb,'00000000-0000-7000-8000-000000000001'),",
        "  ('OT-PASTO','DPTO ADTVO TTOYTTE MCPAL PASTO','NARIÑO','PASTO','52001','8912800003',",
        "   'contactenos@pasto.gov.co','52001000', false,false,false,false,false,",
        "   '{\"parint_transfer\":1,\"parint_registration\":1,\"parint_otherservice\":1}'::jsonb,'00000000-0000-7000-8000-000000000001')",
        "ON CONFLICT (code) DO NOTHING;",
        "RESET search_path;",
        "",
    ]
    DOWN_PATH.write_text("\n".join(down_lines), encoding="utf-8")

    print(f"Generated {UP_PATH} ({len(rows)} rows)")
    print(f"Generated {DOWN_PATH}")


if __name__ == "__main__":
    main()
