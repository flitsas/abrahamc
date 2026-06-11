import type { CompanyRow } from "../api/companies.schemas.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import { StatusChip } from "../../../shared/components/flit/StatusChip.js";

type CompanyGridProps = {
  companies: CompanyRow[] | undefined;
  totalCount: number;
  page: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  selectedTenantId: string | null;
  onSelect: (company: CompanyRow) => void;
  onPageChange: (page: number) => void;
  onRetry: () => void;
};

function companyStatusVariant(status: string): "success" | "warning" | "draft" {
  const normalized = status.toLowerCase();
  if (normalized === "active" || normalized === "activo") {
    return "success";
  }
  if (normalized === "inactive" || normalized === "inactivo") {
    return "warning";
  }
  return "draft";
}

export function CompanyGrid({
  companies,
  totalCount,
  page,
  pageSize,
  isLoading,
  isError,
  error,
  selectedTenantId,
  onSelect,
  onPageChange,
  onRetry,
}: CompanyGridProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (isLoading) {
    return <LoadingSkeleton rows={6} />;
  }

  if (isError) {
    const resolvedError =
      error instanceof Error
        ? error
        : new Error(
            typeof error === "string" ? error : "Error al cargar compañías.",
          );
    return <ErrorState error={resolvedError} onRetry={onRetry} />;
  }

  if (!companies?.length) {
    return (
      <EmptyState
        title="Sin compañías"
        description="No hay compañías que coincidan con la búsqueda."
      />
    );
  }

  return (
    <div className="overflow-hidden rounded-flit-card border border-flit-draft/20 bg-flit-card shadow-flit-card">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <caption className="sr-only">
            Listado de compañías B2B indexadas en FLIT
          </caption>
          <thead className="bg-flit-bg">
            <tr>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Razón social
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                NIT
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Tenant
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Estado
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-right font-semibold text-flit-blueDark"
              >
                Acción
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-flit-draft/10">
            {companies.map((company) => {
              const isSelected = company.tenantId === selectedTenantId;
              return (
                <tr
                  key={company.id}
                  className={isSelected ? "bg-flit-blue/5" : undefined}
                >
                  <td className="px-4 py-3 text-flit-blueDark">
                    <span className="font-semibold">{company.legalName}</span>
                    {company.commercialName && (
                      <span className="mt-0.5 block text-xs text-flit-muted">
                        {company.commercialName}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-flit-blueDark">
                    {company.nit}
                  </td>
                  <td
                    className="px-4 py-3 font-mono text-xs text-flit-muted"
                    title={company.tenantName}
                  >
                    {company.tenantName}
                  </td>
                  <td className="px-4 py-3">
                    <StatusChip
                      label={company.status}
                      variant={companyStatusVariant(company.status)}
                    />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      type="button"
                      aria-pressed={isSelected}
                      aria-label={`Configurar ${company.legalName}`}
                      onClick={() => onSelect(company)}
                      className="text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                    >
                      {isSelected ? "Seleccionada" : "Configurar"}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between border-t border-flit-draft/10 px-4 py-3">
        <p className="text-xs text-flit-muted">
          {totalCount} compañía{totalCount === 1 ? "" : "s"} · Página {page} de{" "}
          {totalPages}
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => onPageChange(Math.max(1, page - 1))}
            aria-label="Página anterior"
            className="rounded-flit-pill border border-flit-draft/30 px-4 py-2 text-xs font-semibold text-flit-blueDark disabled:opacity-40"
          >
            Anterior
          </button>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => onPageChange(Math.min(totalPages, page + 1))}
            aria-label="Página siguiente"
            className="rounded-flit-pill border border-flit-draft/30 px-4 py-2 text-xs font-semibold text-flit-blueDark disabled:opacity-40"
          >
            Siguiente
          </button>
        </div>
      </div>
    </div>
  );
}
