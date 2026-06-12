import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuthMe } from "../../auth/api/auth.api.js";
import { useProcedureInstances } from "../api/procedure-instances.api.js";
import { StatusChip } from "../../../shared/components/flit/StatusChip.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import {
  formatProcedureState,
  procedureStateVariant,
} from "../lib/procedureStateBadge.js";

const STATE_FILTER_OPTIONS = [
  { value: "", label: "Todos los estados" },
  { value: "borrador", label: "Borrador" },
  { value: "pendiente", label: "Pendiente" },
  { value: "aprobado", label: "Aprobado" },
  { value: "rechazado", label: "Rechazado" },
  { value: "entregado", label: "Entregado" },
] as const;

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

export function ProcedureInstancesPage() {
  const { data: session } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const [stateFilter, setStateFilter] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const queryParams = useMemo(
    () =>
      tenantId
        ? {
            tenantId,
            page,
            pageSize,
            ...(stateFilter ? { state: stateFilter } : {}),
          }
        : undefined,
    [tenantId, page, pageSize, stateFilter],
  );

  const { data, isLoading, isError, error, refetch } =
    useProcedureInstances(queryParams);

  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / pageSize))
    : 1;

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <label
            htmlFor="procedure-state-filter"
            className="block text-sm font-semibold text-flit-blueDark"
          >
            Filtrar por estado
          </label>
          <select
            id="procedure-state-filter"
            value={stateFilter}
            onChange={(event) => {
              setStateFilter(event.target.value);
              setPage(1);
            }}
            className="mt-2 h-11 min-w-[220px] rounded-[10px] border border-flit-border bg-white px-3 text-sm text-flit-blueDark focus:border-flit-blue focus:outline-none focus:ring-2 focus:ring-flit-blue/20"
          >
            {STATE_FILTER_OPTIONS.map((option) => (
              <option key={option.value || "all"} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {isLoading && <LoadingSkeleton rows={6} />}
      {isError && <ErrorState error={error} onRetry={() => refetch()} />}
      {!isLoading && !isError && !data?.items.length && (
        <EmptyState
          title="Sin trámites"
          description="Aún no hay instancias radicadas para este tenant. Inicie un trámite desde el catálogo de tipos."
        />
      )}
      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50">
                <tr>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-slate-700"
                  >
                    ID compuesto
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-slate-700"
                  >
                    Estado
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-slate-700"
                  >
                    Tipo
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-slate-700"
                  >
                    Radicado
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-slate-700"
                  >
                    Acciones
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.map((item) => (
                  <tr key={item.id}>
                    <td className="px-4 py-3 font-mono text-xs text-slate-800">
                      {item.compositeId}
                    </td>
                    <td className="px-4 py-3">
                      <StatusChip
                        label={formatProcedureState(item.state)}
                        variant={procedureStateVariant(item.state)}
                      />
                    </td>
                    <td className="px-4 py-3 text-slate-900">
                      {item.procedureTypeCode}
                    </td>
                    <td className="px-4 py-3 text-slate-600">
                      {formatDate(item.radicatedAt)}
                    </td>
                    <td className="px-4 py-3">
                      <Link to={`/tramites/${item.id}`}>
                        <GradientButton
                          type="button"
                          className="!h-10 !w-auto !px-5 !text-xs"
                        >
                          Continuar
                        </GradientButton>
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <nav
              className="flex items-center justify-between gap-3"
              aria-label="Paginación de trámites"
            >
              <p className="text-sm text-flit-muted">
                Página {data.page} de {totalPages} — {data.totalCount} registros
              </p>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  className="rounded-[10px] border border-flit-border px-3 py-2 text-sm font-semibold text-flit-blueDark disabled:opacity-50"
                >
                  Anterior
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() =>
                    setPage((current) => Math.min(totalPages, current + 1))
                  }
                  className="rounded-[10px] border border-flit-border px-3 py-2 text-sm font-semibold text-flit-blueDark disabled:opacity-50"
                >
                  Siguiente
                </button>
              </div>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
