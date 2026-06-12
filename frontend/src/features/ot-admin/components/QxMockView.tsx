import type { OtRecentProcedure } from "../api/ot-admin.schemas.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";

type QxMockViewProps = {
  procedures: OtRecentProcedure[];
  readOnly: boolean;
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

export function QxMockView({ procedures, readOnly }: QxMockViewProps) {
  return (
    <section
      aria-labelledby="qx-mock-heading"
      className="rounded-flit-card border border-amber-300/60 bg-amber-50/80 p-5 shadow-flit-card"
    >
      <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.15em] text-amber-800">
            Simulación
          </p>
          <h2
            id="qx-mock-heading"
            className="text-lg font-bold text-flit-blueText"
          >
            Vista Quipux (mock)
          </h2>
          <p className="mt-1 text-sm text-flit-muted">
            Representación visual de la cola QX. No ejecuta callbacks reales.
            {readOnly
              ? " Modo solo lectura: acciones de aprobación deshabilitadas."
              : ""}
          </p>
        </div>
      </div>

      {procedures.length === 0 ? (
        <EmptyState
          title="Cola QX vacía"
          description="No hay trámites recientes para mostrar en la simulación."
        />
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <caption className="sr-only">
              Trámites recientes en simulación Quipux
            </caption>
            <thead>
              <tr className="border-b border-amber-200 text-flit-muted">
                <th scope="col" className="px-3 py-2 font-semibold">
                  Referencia
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Estado
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Radicado
                </th>
                {!readOnly && (
                  <th scope="col" className="px-3 py-2 font-semibold">
                    Acciones
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {procedures.map((procedure) => (
                <tr
                  key={procedure.id}
                  className="border-b border-amber-100/80 last:border-0"
                >
                  <td className="px-3 py-3 font-medium text-flit-blueText">
                    {procedure.reference_number}
                  </td>
                  <td className="px-3 py-3 capitalize">{procedure.state}</td>
                  <td className="px-3 py-3">
                    {formatDate(procedure.radicated_at)}
                  </td>
                  {!readOnly && (
                    <td className="px-3 py-3">
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          className="rounded-flit-pill bg-flit-blue px-3 py-1.5 text-xs font-semibold text-white"
                        >
                          Aprobar
                        </button>
                        <button
                          type="button"
                          className="rounded-flit-pill border border-flit-border px-3 py-1.5 text-xs font-semibold text-flit-blueText"
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
  );
}
