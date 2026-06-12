import { useProcedureQueryResults } from "../api/procedure-wizard.api.js";
import type { QueryResultItem } from "../api/procedure-wizard.schemas.js";
import { StatusChip } from "../../../shared/components/flit/StatusChip.js";

type QueryBannersProps = {
  instanceId: string;
  tenantId: string;
  polling: boolean;
};

function bannerVariant(
  result: QueryResultItem,
): "success" | "active" | "warning" | "danger" | "draft" {
  if (result.circuitOpen) {
    return "warning";
  }
  if (result.succeeded) {
    return "success";
  }
  if (result.mandatory && result.status === "failed") {
    return "danger";
  }
  if (result.status === "pending" || result.status === "running") {
    return "active";
  }
  return "warning";
}

function bannerMessage(result: QueryResultItem): string {
  if (result.circuitOpen) {
    return `${result.connectorCode}: circuito abierto — puede continuar sin bloqueo.`;
  }
  if (result.succeeded) {
    return `${result.connectorCode}: consulta exitosa.`;
  }
  if (result.status === "pending" || result.status === "running") {
    return `${result.connectorCode}: consulta en progreso…`;
  }
  if (result.mandatory) {
    return `${result.connectorCode}: consulta obligatoria fallida.`;
  }
  return `${result.connectorCode}: consulta no exitosa (no bloqueante).`;
}

export function QueryBanners({
  instanceId,
  tenantId,
  polling,
}: QueryBannersProps) {
  const query = useProcedureQueryResults(instanceId, tenantId, {
    enabled: Boolean(instanceId && tenantId),
    refetchInterval: polling ? 2_000 : false,
  });

  if (query.isLoading) {
    return (
      <div
        className="rounded-flit-card border border-flit-border bg-flit-card p-4 text-sm text-flit-muted"
        role="status"
        aria-live="polite"
      >
        Cargando resultados de consultas externas…
      </div>
    );
  }

  if (query.isError) {
    return (
      <div
        className="rounded-flit-card border border-flit-danger/30 bg-flit-danger/5 p-4 text-sm text-flit-danger"
        role="alert"
        aria-live="assertive"
      >
        No se pudieron cargar los resultados de consultas.
      </div>
    );
  }

  const results = query.data?.results ?? [];
  if (results.length === 0) {
    return null;
  }

  const canContinue = query.data?.canContinue ?? true;

  return (
    <div className="space-y-3" aria-live="polite" aria-atomic="false">
      <p className="text-sm font-semibold text-flit-blueDark">
        Consultas externas
        {!canContinue && (
          <span className="ml-2 text-flit-danger">
            — revise las consultas obligatorias antes de continuar
          </span>
        )}
      </p>
      <ul className="space-y-2" role="list">
        {results.map((result) => (
          <li
            key={result.connectorCode}
            className="flex flex-wrap items-center gap-3 rounded-[10px] border border-flit-border bg-white px-4 py-3"
          >
            <StatusChip label={result.status} variant={bannerVariant(result)} />
            <span className="text-sm text-flit-blueDark">
              {bannerMessage(result)}
            </span>
            {result.mandatory && (
              <span className="text-xs font-semibold uppercase text-flit-muted">
                Obligatoria
              </span>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
