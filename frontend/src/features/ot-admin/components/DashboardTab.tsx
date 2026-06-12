import { useOtDashboard } from "../api/ot-admin.api.js";
import { useCanAdministerOt } from "../hooks/useCanAdministerOt.js";
import { isQuipuxMode } from "../lib/integrationMode.js";
import { IntegrationModeToggle } from "./IntegrationModeToggle.js";
import { QxMockView } from "./QxMockView.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type DashboardTabProps = {
  agencyId: string;
};

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

export function DashboardTab({ agencyId }: DashboardTabProps) {
  const canAdminister = useCanAdministerOt();
  const dashboardQuery = useOtDashboard(agencyId);

  if (dashboardQuery.isLoading) {
    return <LoadingSkeleton rows={5} />;
  }

  if (dashboardQuery.isError) {
    return (
      <ErrorState
        error={dashboardQuery.error}
        onRetry={() => dashboardQuery.refetch()}
      />
    );
  }

  const dashboard = dashboardQuery.data;
  if (!dashboard) {
    return (
      <EmptyState
        title="Sin datos de dashboard"
        description="No se recibió información del organismo de tránsito."
      />
    );
  }

  const quipuxMode = isQuipuxMode(dashboard.integration_mode);
  const hasMetrics = dashboard.total_procedures > 0;

  return (
    <div className="space-y-6">
      <div className="rounded-flit-card bg-flit-card p-5 shadow-flit-card">
        <IntegrationModeToggle
          agencyId={agencyId}
          currentMode={dashboard.integration_mode}
          canAdminister={canAdminister}
        />
      </div>

      {!hasMetrics ? (
        <EmptyState
          title="Sin trámites registrados"
          description="Este OT aún no tiene trámites en FLIT. Las métricas aparecerán cuando existan radicaciones."
        />
      ) : (
        <>
          <section
            aria-labelledby="ot-metrics-heading"
            className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4"
          >
            <h2 id="ot-metrics-heading" className="sr-only">
              Métricas del dashboard OT
            </h2>
            <article className="rounded-flit-card bg-flit-card p-4 shadow-flit-card">
              <p className="text-xs font-semibold uppercase tracking-wide text-flit-muted">
                Total trámites
              </p>
              <p className="mt-2 text-3xl font-bold text-flit-blueText">
                {dashboard.total_procedures}
              </p>
            </article>
            {dashboard.by_state.map((entry) => (
              <article
                key={entry.state}
                className="rounded-flit-card bg-flit-card p-4 shadow-flit-card"
              >
                <p className="text-xs font-semibold uppercase tracking-wide text-flit-muted capitalize">
                  {entry.state}
                </p>
                <p className="mt-2 text-3xl font-bold text-flit-blueText">
                  {entry.count}
                </p>
              </article>
            ))}
          </section>

          <section
            aria-labelledby="ot-recent-heading"
            className="rounded-flit-card bg-flit-card p-5 shadow-flit-card"
          >
            <h2
              id="ot-recent-heading"
              className="text-lg font-bold text-flit-blueText"
            >
              Trámites recientes
            </h2>
            {dashboard.recent_procedures.length === 0 ? (
              <div className="mt-4">
                <EmptyState
                  title="Sin trámites recientes"
                  description="No hay radicaciones recientes para este OT."
                />
              </div>
            ) : (
              <div className="mt-4 overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <caption className="sr-only">
                    Últimos trámites del organismo de tránsito
                  </caption>
                  <thead>
                    <tr className="border-b border-flit-border text-flit-muted">
                      <th scope="col" className="px-3 py-2 font-semibold">
                        Referencia
                      </th>
                      <th scope="col" className="px-3 py-2 font-semibold">
                        Estado
                      </th>
                      <th scope="col" className="px-3 py-2 font-semibold">
                        Radicado
                      </th>
                      {!quipuxMode && (
                        <th scope="col" className="px-3 py-2 font-semibold">
                          Acciones
                        </th>
                      )}
                    </tr>
                  </thead>
                  <tbody>
                    {dashboard.recent_procedures.map((procedure) => (
                      <tr
                        key={procedure.id}
                        className="border-b border-flit-border/60 last:border-0"
                      >
                        <td className="px-3 py-3 font-medium text-flit-blueText">
                          {procedure.reference_number}
                        </td>
                        <td className="px-3 py-3 capitalize">
                          {procedure.state}
                        </td>
                        <td className="px-3 py-3">
                          {formatDate(procedure.radicated_at)}
                        </td>
                        {!quipuxMode && (
                          <td className="px-3 py-3">
                            <div className="flex flex-wrap gap-2">
                              <button
                                type="button"
                                className="rounded-flit-pill bg-flit-blue px-3 py-1.5 text-xs font-semibold text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                              >
                                Aprobar
                              </button>
                              <button
                                type="button"
                                className="rounded-flit-pill border border-flit-border px-3 py-1.5 text-xs font-semibold text-flit-blueText focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                              >
                                Rechazar
                              </button>
                            </div>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}

      {quipuxMode && (
        <QxMockView
          procedures={dashboard.recent_procedures}
          readOnly={quipuxMode}
        />
      )}
    </div>
  );
}
