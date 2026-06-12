import { useState } from "react";
import { useIntegrationLogs } from "../api/ot-admin.api.js";
import { ApiError } from "../../../shared/api/client.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type IntegrationLogsTabProps = {
  agencyId: string;
};

const PAGE_SIZE = 10;

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }
  try {
    return new Intl.DateTimeFormat("es-CO", {
      dateStyle: "short",
      timeStyle: "short",
    }).format(new Date(value));
  } catch {
    return value;
  }
}

export function IntegrationLogsTab({ agencyId }: IntegrationLogsTabProps) {
  const [page, setPage] = useState(1);
  const logsQuery = useIntegrationLogs({
    trafficAgencyId: agencyId,
    page,
    pageSize: PAGE_SIZE,
  });

  const isNotFound =
    logsQuery.isError &&
    logsQuery.error instanceof ApiError &&
    logsQuery.error.status === 404;

  if (logsQuery.isLoading) {
    return <LoadingSkeleton rows={6} />;
  }

  if (logsQuery.isError && !isNotFound) {
    return (
      <ErrorState error={logsQuery.error} onRetry={() => logsQuery.refetch()} />
    );
  }

  if (isNotFound) {
    return (
      <EmptyState
        title="Bitácora no disponible"
        description="El endpoint GET /api/v1/integrations/logs aún no está expuesto en este ambiente. Los eventos QX se registran en integrations.webhook_events."
      />
    );
  }

  const data = logsQuery.data;
  if (!data || data.items.length === 0) {
    return (
      <EmptyState
        title="Sin eventos de integración"
        description="No hay registros de webhooks o llamadas externas para este OT."
      />
    );
  }

  const totalPages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <div className="space-y-4">
      <div className="overflow-x-auto rounded-flit-card bg-flit-card shadow-flit-card">
        <table className="min-w-full text-left text-sm">
          <caption className="sr-only">
            Bitácora de integración del organismo de tránsito
          </caption>
          <thead>
            <tr className="border-b border-flit-border bg-flit-bg text-flit-muted">
              <th scope="col" className="px-4 py-3 font-semibold">
                Fecha
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Dirección
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Evento
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Estado
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Idempotencia
              </th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((log) => (
              <tr
                key={log.id}
                className="border-b border-flit-border/60 last:border-0"
              >
                <td className="px-4 py-3 whitespace-nowrap">
                  {formatDate(log.receivedAt)}
                </td>
                <td className="px-4 py-3 capitalize">{log.direction}</td>
                <td className="px-4 py-3">{log.eventType}</td>
                <td className="px-4 py-3 capitalize">{log.status}</td>
                <td className="max-w-[12rem] truncate px-4 py-3 font-mono text-xs">
                  {log.idempotencyKey ?? "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <nav
        aria-label="Paginación de logs de integración"
        className="flex flex-wrap items-center justify-between gap-3"
      >
        <p className="text-sm text-flit-muted">
          Página {data.page} de {totalPages} · {data.total} registros
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            className="rounded-flit-pill border border-flit-border px-4 py-2 text-sm font-semibold text-flit-blueText transition hover:bg-flit-bg disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          >
            Anterior
          </button>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() =>
              setPage((current) => Math.min(totalPages, current + 1))
            }
            className="rounded-flit-pill border border-flit-border px-4 py-2 text-sm font-semibold text-flit-blueText transition hover:bg-flit-bg disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          >
            Siguiente
          </button>
        </div>
      </nav>
    </div>
  );
}
