# AGENTS.override.md — services/python-ml

> Override por servicio para Codex/Junie. Editar directamente en este archivo.
> Override por servicio. **Codex** y **Junie** leen este archivo + `AGENTS.md` raíz cuando se invocan desde `services/python-ml/`.

## Agente principal: `python-ml`

Agente principal para este servicio.

## Scope

`services/python-ml/**`

## Lectura obligatoria al trabajar en este servicio

- [`AGENTS.md` raíz](../../AGENTS.md) — convenciones globales FLIT
- [`.ai/agents/python-ml.prompt.md`](../../.ai/agents/python-ml.prompt.md) — prompt completo del agente
- [`.ai/agents/python-ml.agent.yaml`](../../.ai/agents/python-ml.agent.yaml) — metadata, quality_gates, triggers
- [`agent-templates/conventions.md`](../../agent-templates/conventions.md) — 18 reglas innegociables
- [`agent-templates/code-style-guide.md`](../../agent-templates/code-style-guide.md)

## Quality gates de este servicio

Ejecutar antes de cada commit (vía `pnpm quality` o el agente `quality`):

- `uv run ruff check app tests`
- `uv run ruff format app tests --check`
- `uv run pytest --cov=app`
- SonarCloud quality gate: PASS

## Mantenimiento

Actualizar este archivo cuando cambien quality gates o el agente principal del servicio.
