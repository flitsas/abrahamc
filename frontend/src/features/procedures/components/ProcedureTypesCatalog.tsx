import { Link } from "react-router-dom";
import { PermissionGate } from "../../auth/components/PermissionGate.js";
import { PERMISSIONS } from "../../auth/lib/permissions.js";
import { useProcedureTypes } from "../api/procedures.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type ProcedureTypesCatalogProps = {
  tenantId: string | undefined;
};

export function ProcedureTypesCatalog({
  tenantId,
}: ProcedureTypesCatalogProps) {
  const { data, isLoading, isError, error, refetch } =
    useProcedureTypes(tenantId);

  if (isLoading) {
    return <LoadingSkeleton rows={5} />;
  }

  if (isError) {
    return <ErrorState error={error} onRetry={() => refetch()} />;
  }

  if (!data?.items.length) {
    return (
      <EmptyState
        title="Sin tipos de trámite"
        description="No hay tipos activos para este tenant. Verifica las migraciones y el seed DEV."
      />
    );
  }

  return (
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
                {item.code}
              </td>
              <td className="px-4 py-3 text-slate-900">{item.name}</td>
              <td className="px-4 py-3 text-slate-600">{item.familyName}</td>
              <td className="px-4 py-3 text-slate-600">{item.maxSteps}</td>
              <td className="px-4 py-3">
                <PermissionGate permission={PERMISSIONS.viewTramites}>
                  <Link to={`/tramites/nuevo/${encodeURIComponent(item.code)}`}>
                    <GradientButton
                      type="button"
                      className="!h-10 !w-auto !px-5 !text-xs"
                    >
                      Nuevo trámite
                    </GradientButton>
                  </Link>
                </PermissionGate>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
