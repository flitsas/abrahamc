import { useState } from "react";
import {
  useCreateVehicleException,
  useDeleteVehicleException,
  useVehicleExceptions,
} from "../api/companies.api.js";
import { useCanEditCompanyConfig } from "../hooks/useCanEditCompanyConfig.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type VehicleExceptionsSectionProps = {
  tenantId: string;
  embedded?: boolean;
};

function shortUserId(userId: string): string {
  return `${userId.slice(0, 8)}…`;
}

export function VehicleExceptionsSection({
  tenantId,
  embedded = false,
}: VehicleExceptionsSectionProps) {
  const canEdit = useCanEditCompanyConfig();
  const exceptionsQuery = useVehicleExceptions(tenantId);
  const createMutation = useCreateVehicleException(tenantId);
  const deleteMutation = useDeleteVehicleException(tenantId);
  const [userId, setUserId] = useState("");
  const [reason, setReason] = useState("");
  const [expiresAt, setExpiresAt] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  async function handleAdd(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    setStatusMessage(null);

    if (!canEdit) {
      return;
    }

    const trimmedUserId = userId.trim();
    if (!trimmedUserId) {
      setFormError("El ID de usuario es obligatorio.");
      return;
    }

    try {
      await createMutation.mutateAsync({
        userId: trimmedUserId,
        reason: reason.trim() || undefined,
        expiresAt: expiresAt.trim() || undefined,
      });
      setUserId("");
      setReason("");
      setExpiresAt("");
      setStatusMessage("Excepción vehicular agregada.");
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : "No se pudo crear la excepción.",
      );
    }
  }

  async function handleDelete(exceptionId: string) {
    if (!canEdit) {
      return;
    }
    setStatusMessage(null);
    try {
      await deleteMutation.mutateAsync(exceptionId);
      setStatusMessage("Excepción eliminada.");
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : "No se pudo eliminar la excepción.",
      );
    }
  }

  return (
    <section
      className="space-y-4"
      aria-label={embedded ? "Excepciones vehiculares" : undefined}
      aria-labelledby={embedded ? undefined : "vehicle-exceptions-heading"}
    >
      {!embedded && (
        <div>
          <h3
            id="vehicle-exceptions-heading"
            className="text-base font-semibold text-flit-blueDark"
          >
            Excepciones vehiculares
          </h3>
          <p className="mt-1 text-sm text-flit-muted">
            Lista blanca de usuarios autorizados para operar vehículos de terceros.
          </p>
        </div>
      )}

      {canEdit && (
        <form
          className="grid gap-3 rounded-[10px] border border-flit-draft/20 bg-flit-bg p-4 sm:grid-cols-2"
          onSubmit={handleAdd}
        >
          <div className="sm:col-span-2">
            <label
              htmlFor="exception-user-id"
              className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
            >
              ID de usuario (UUID)
            </label>
            <input
              id="exception-user-id"
              type="text"
              value={userId}
              onChange={(event) => setUserId(event.target.value)}
              className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 font-mono text-xs text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
            />
          </div>
          <div>
            <label
              htmlFor="exception-reason"
              className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
            >
              Motivo
            </label>
            <input
              id="exception-reason"
              type="text"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 text-sm text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
            />
          </div>
          <div>
            <label
              htmlFor="exception-expires"
              className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
            >
              Expira (ISO 8601, opcional)
            </label>
            <input
              id="exception-expires"
              type="text"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.target.value)}
              placeholder="2026-12-31T23:59:59Z"
              className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 font-mono text-xs text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
            />
          </div>
          {formError && (
            <p className="sm:col-span-2 text-sm text-flit-danger" role="alert">
              {formError}
            </p>
          )}
          <div className="sm:col-span-2">
            <GradientButton
              type="submit"
              className="!h-11 !w-auto !px-8 !text-sm"
              disabled={createMutation.isPending}
            >
              {createMutation.isPending ? "Agregando…" : "Agregar excepción"}
            </GradientButton>
          </div>
        </form>
      )}

      {statusMessage && (
        <p className="text-sm text-flit-green" role="status">
          {statusMessage}
        </p>
      )}

      {exceptionsQuery.isLoading && <LoadingSkeleton rows={4} />}
      {exceptionsQuery.isError && (
        <ErrorState
          error={exceptionsQuery.error}
          onRetry={() => exceptionsQuery.refetch()}
        />
      )}

      {!exceptionsQuery.isLoading &&
        !exceptionsQuery.isError &&
        exceptionsQuery.data?.length === 0 && (
          <EmptyState
            title="Sin excepciones"
            description="No hay usuarios en la lista blanca vehicular de este tenant."
          />
        )}

      {!exceptionsQuery.isLoading &&
        !exceptionsQuery.isError &&
        exceptionsQuery.data &&
        exceptionsQuery.data.length > 0 && (
          <div className="overflow-hidden rounded-[10px] border border-flit-draft/20">
            <table className="min-w-full text-sm">
              <caption className="sr-only">
                Excepciones vehiculares del tenant
              </caption>
              <thead className="bg-flit-bg">
                <tr>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                  >
                    Usuario
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                  >
                    Motivo
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                  >
                    Expira
                  </th>
                  {canEdit && (
                    <th
                      scope="col"
                      className="px-4 py-3 text-right font-semibold text-flit-blueDark"
                    >
                      Acción
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-flit-draft/10 bg-white">
                {exceptionsQuery.data.map((item) => (
                  <tr key={item.id}>
                    <td
                      className="px-4 py-3 font-mono text-xs text-flit-blueDark"
                      title={item.userId}
                    >
                      {shortUserId(item.userId)}
                    </td>
                    <td className="px-4 py-3 text-flit-muted">
                      {item.reason ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-flit-muted">
                      {item.expiresAt ?? "—"}
                    </td>
                    {canEdit && (
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          onClick={() => handleDelete(item.id)}
                          disabled={deleteMutation.isPending}
                          className="text-sm font-semibold text-flit-danger hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                        >
                          Eliminar
                        </button>
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
