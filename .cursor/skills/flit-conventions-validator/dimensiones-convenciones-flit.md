# Dimensiones FLIT (detalle)

## 1 — Título PR

Regex: `^\[US #\d+\] \[(BACKEND|FRONTEND|INFRA|QA|DOCS)\] – .+`

Válido: `[US #4521] [BACKEND] – Personas – Endpoint registro`  
Inválido: `feat: add endpoint`, `[BACKEND] Personas`

## 2 — Rama

Regex: `^agent/(backend|frontend|infra|qa|docs)/(\d+)-[a-z0-9-]+$`

Válido: `agent/backend/4521-personas-registro`

## 3 — Commits

Formato: `<type>(<scope>): <subject> [#US-ID]`  
Tipos: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `build`, `ci`

## 4 — Rutas

- Backend: `backend/src/modules/<modulo>/...`
- Frontend: `frontend/src/features/<feature>/...`
- ADRs: `docs/decisions/ADR-NNNN-<slug>.md`

## 5 — Campos ADO

- `Custom.Modulo`, `Custom.Refinement`, Story Points Fibonacci, sprint **siguiente**, tag `DOR`, Area `FLIT`, AssignedTo humano

## 6 — Denylist

Rechazar diff que toque: `.env` (sin example), `node_modules`, `dist`, `coverage`, `*.key`, `secrets/*`, migraciones ya aplicadas

## 7 — Estilo

`eslint`, `prettier --check`, `tsc --noEmit`, sin `console.log` en código no test, sin TODOs nuevos en el diff
