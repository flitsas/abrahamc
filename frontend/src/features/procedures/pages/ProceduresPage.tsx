import { useAuthMe } from "../../auth/api/auth.api.js";
import { useProcedureTypes } from "../api/procedures.api.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

export function ProceduresPage() {
  const { data: session } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const { data, isLoading, isError, error, refetch } =
    useProcedureTypes(tenantId);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-slate-900">
          Tipos de trámite
        </h1>
        <p className="mt-2 text-sm text-slate-600">
          Catálogo activo para el tenant de tu sesión.
        </p>
      </header>

      {isLoading && <LoadingSkeleton rows={5} />}
      {isError && <ErrorState error={error} onRetry={() => refetch()} />}
      {!isLoading && !isError && !data?.items.length && (
        <EmptyState
          title="Sin tipos de trámite"
          description="No hay tipos activos para este tenant. Verifica las migraciones y el seed DEV."
        />
      )}
      {!isLoading && !isError && data && data.items.length > 0 && (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th
                  scope="col"
                  className="px-4 py-3 text-left font-semibold text-slate-700"
                >
                  Código
                </th>
                <th
                  scope="col"
                  className="px-4 py-3 text-left font-semibold text-slate-700"
                >
                  Nombre
                </th>
                <th
                  scope="col"
                  className="px-4 py-3 text-left font-semibold text-slate-700"
                >
                  Familia
                </th>
                <th
                  scope="col"
                  className="px-4 py-3 text-left font-semibold text-slate-700"
                >
                  Pasos máx.
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.items.map((item) => (
                <tr key={item.id}>
                  <td className="px-4 py-3 font-mono text-xs text-slate-800">
                    {item.code}
                  </td>
                  <td className="px-4 py-3 text-slate-900">{item.name}</td>
                  <td className="px-4 py-3 text-slate-600">
                    {item.familyName}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{item.maxSteps}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
