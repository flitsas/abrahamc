import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useOtAgencies } from "../api/ot-admin.api.js";
import { useDebouncedValue } from "../../companies-admin/hooks/useDebouncedValue.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

const SEARCH_DEBOUNCE_MS = 300;

export function OtAgencySelector() {
  const navigate = useNavigate();
  const [searchInput, setSearchInput] = useState("");
  const debouncedSearch = useDebouncedValue(
    searchInput.trim(),
    SEARCH_DEBOUNCE_MS,
  );
  const agenciesQuery = useOtAgencies({
    q: debouncedSearch || undefined,
    page: 1,
    pageSize: 50,
  });

  if (agenciesQuery.isLoading) {
    return <LoadingSkeleton rows={5} />;
  }

  if (agenciesQuery.isError) {
    return (
      <ErrorState
        error={agenciesQuery.error}
        onRetry={() => agenciesQuery.refetch()}
      />
    );
  }

  const agencies = agenciesQuery.data?.items ?? [];

  return (
    <div className="space-y-4">
      <label
        htmlFor="ot-agency-search"
        className="block text-sm font-medium text-flit-blueText"
      >
        Buscar organismo de tránsito
      </label>
      <input
        id="ot-agency-search"
        type="search"
        value={searchInput}
        onChange={(event) => setSearchInput(event.target.value)}
        placeholder="Nombre, código o municipio"
        className="w-full max-w-md rounded-flit-pill border border-flit-border bg-white px-4 py-2.5 text-sm text-flit-blueText focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
      />

      {agencies.length === 0 ? (
        <EmptyState
          title="Sin organismos de tránsito"
          description="No se encontraron OT activos con el criterio de búsqueda."
        />
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {agencies.map((agency) => (
            <li key={agency.id}>
              <button
                type="button"
                onClick={() => navigate(`/admin/ot/${agency.id}/tramites`)}
                className="w-full rounded-flit-card border border-flit-border bg-flit-card p-4 text-left shadow-flit-card transition hover:border-flit-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
              >
                <p className="font-semibold text-flit-blueText">
                  {agency.name}
                </p>
                <p className="mt-1 text-sm text-flit-muted">
                  {agency.code}
                  {agency.city ? ` · ${agency.city}` : ""}
                </p>
                <p className="mt-2 text-xs uppercase tracking-wide text-flit-muted">
                  Modo: {agency.mode}
                </p>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
