import { useEffect, useMemo, useState } from "react";
import {
  useAuthorizedTrafficAgencies,
  useOtAgenciesCatalog,
  useUpsertAuthorizedTrafficAgency,
} from "../api/companies.api.js";
import { useCanEditCompanyConfig } from "../hooks/useCanEditCompanyConfig.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type OtMatrixSectionProps = {
  tenantId: string;
  embedded?: boolean;
  onDirtyChange: (dirty: boolean) => void;
};

type OtMatrixRow = {
  trafficAgencyId: string;
  code: string;
  name: string;
  city: string | null;
  isEnabled: boolean;
};

export function OtMatrixSection({
  tenantId,
  embedded = false,
  onDirtyChange,
}: OtMatrixSectionProps) {
  const canEdit = useCanEditCompanyConfig();
  const agenciesQuery = useOtAgenciesCatalog();
  const authorizedQuery = useAuthorizedTrafficAgencies(tenantId);
  const saveMutation = useUpsertAuthorizedTrafficAgency(tenantId);
  const [localEnabled, setLocalEnabled] = useState<Record<string, boolean>>({});
  const [savedEnabled, setSavedEnabled] = useState<Record<string, boolean>>({});
  const [saveMessage, setSaveMessage] = useState<string | null>(null);

  const authorizedMap = useMemo(() => {
    const map = new Map<string, boolean>();
    for (const entry of authorizedQuery.data ?? []) {
      map.set(entry.trafficAgencyId, entry.isEnabled);
    }
    return map;
  }, [authorizedQuery.data]);

  useEffect(() => {
    if (agenciesQuery.isLoading || authorizedQuery.isLoading) {
      return;
    }

    const next: Record<string, boolean> = {};
    for (const agency of agenciesQuery.data?.items ?? []) {
      next[agency.id] = authorizedMap.get(agency.id) ?? false;
    }
    setLocalEnabled(next);
    setSavedEnabled(next);
  }, [
    agenciesQuery.data,
    agenciesQuery.isLoading,
    authorizedMap,
    authorizedQuery.isLoading,
    tenantId,
  ]);

  const rows: OtMatrixRow[] = useMemo(
    () =>
      (agenciesQuery.data?.items ?? []).map((agency) => ({
        trafficAgencyId: agency.id,
        code: agency.code,
        name: agency.name,
        city: agency.city ?? null,
        isEnabled: localEnabled[agency.id] ?? false,
      })),
    [agenciesQuery.data?.items, localEnabled],
  );

  const isDirty = useMemo(() => {
    const keys = new Set([
      ...Object.keys(localEnabled),
      ...Object.keys(savedEnabled),
    ]);
    for (const key of keys) {
      if (localEnabled[key] !== savedEnabled[key]) {
        return true;
      }
    }
    return false;
  }, [localEnabled, savedEnabled]);

  useEffect(() => {
    onDirtyChange(isDirty);
  }, [isDirty, onDirtyChange]);

  function toggleAgency(trafficAgencyId: string) {
    if (!canEdit) {
      return;
    }
    setLocalEnabled((current) => ({
      ...current,
      [trafficAgencyId]: !current[trafficAgencyId],
    }));
    setSaveMessage(null);
  }

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canEdit || !isDirty) {
      return;
    }

    const changes = Object.keys(localEnabled).filter(
      (id) => localEnabled[id] !== savedEnabled[id],
    );

    for (const trafficAgencyId of changes) {
      await saveMutation.mutateAsync({
        trafficAgencyId,
        isEnabled: localEnabled[trafficAgencyId] ?? false,
      });
    }

    setSavedEnabled({ ...localEnabled });
    setSaveMessage("Matriz OT actualizada.");
  }

  const isLoading = agenciesQuery.isLoading || authorizedQuery.isLoading;
  const isError = agenciesQuery.isError || authorizedQuery.isError;
  const error = agenciesQuery.error ?? authorizedQuery.error;

  if (isLoading) {
    return <LoadingSkeleton rows={5} />;
  }

  if (isError) {
    const resolvedError =
      error instanceof Error
        ? error
        : new Error("Error al cargar la matriz OT.");
    return (
      <ErrorState
        error={resolvedError}
        onRetry={() => {
          void agenciesQuery.refetch();
          void authorizedQuery.refetch();
        }}
      />
    );
  }

  if (!rows.length) {
    return (
      <EmptyState
        title="Sin organismos de tránsito"
        description="No hay OT disponibles en el catálogo."
      />
    );
  }

  return (
    <form className="space-y-4" onSubmit={handleSave}>
      {!embedded && (
        <div>
          <h3 className="text-base font-semibold text-flit-blueDark">
            Matriz OT autorizados
          </h3>
          <p className="mt-1 text-sm text-flit-muted">
            Habilita los organismos de tránsito permitidos para este tenant.
          </p>
        </div>
      )}

      <div className="overflow-hidden rounded-[10px] border border-flit-draft/20">
        <table className="min-w-full text-sm">
          <caption className="sr-only">
            Matriz de organismos de tránsito autorizados por tenant
          </caption>
          <thead className="bg-flit-bg">
            <tr>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Código
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Organismo
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-left font-semibold text-flit-blueDark"
              >
                Ciudad
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-center font-semibold text-flit-blueDark"
              >
                Habilitado
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-flit-draft/10 bg-white">
            {rows.map((row) => (
              <tr key={row.trafficAgencyId}>
                <td className="px-4 py-3 font-mono text-xs text-flit-blueDark">
                  {row.code}
                </td>
                <td className="px-4 py-3 text-flit-blueDark">{row.name}</td>
                <td className="px-4 py-3 text-flit-muted">{row.city ?? "—"}</td>
                <td className="px-4 py-3 text-center">
                  <input
                    type="checkbox"
                    checked={row.isEnabled}
                    disabled={!canEdit}
                    onChange={() => toggleAgency(row.trafficAgencyId)}
                    aria-label={`Habilitar ${row.name}`}
                    className="h-4 w-4 rounded border-flit-draft/40 text-flit-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {saveMessage && (
        <p className="text-sm text-flit-green" role="status">
          {saveMessage}
        </p>
      )}

      {saveMutation.isError && (
        <p className="text-sm text-flit-danger" role="alert">
          {saveMutation.error instanceof Error
            ? saveMutation.error.message
            : "No se pudo guardar la matriz OT."}
        </p>
      )}

      {canEdit && (
        <GradientButton
          type="submit"
          className="!h-11 !w-auto !px-8 !text-sm"
          disabled={!isDirty || saveMutation.isPending}
        >
          {saveMutation.isPending ? "Guardando…" : "Guardar matriz OT"}
        </GradientButton>
      )}
    </form>
  );
}
