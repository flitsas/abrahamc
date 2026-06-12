import { useState } from "react";
import type { ProcedureTypeSummary } from "../api/parametrizacion.schemas.js";
import {
  useProcedureMatrix,
  useProcedureRules,
  useSimulateRules,
} from "../api/parametrizacion.api.js";
import { useCanSimulateRules } from "../hooks/useCanManageParametrizacion.js";
import {
  parseApiFieldErrors,
  type FieldErrors,
} from "../lib/parseApiFieldErrors.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type RulesSimulatorTabProps = {
  tenantId: string;
  selectedType: ProcedureTypeSummary | null;
};

export function RulesSimulatorTab({
  tenantId,
  selectedType,
}: RulesSimulatorTabProps) {
  const canSimulate = useCanSimulateRules();
  const matrixQuery = useProcedureMatrix(
    selectedType ? { tenantId, typeCode: selectedType.code } : null,
    Boolean(selectedType),
  );
  const rulesQuery = useProcedureRules(
    tenantId,
    selectedType?.id,
    Boolean(selectedType),
  );
  const simulateMutation = useSimulateRules();

  const [payloadJson, setPayloadJson] = useState('{\n  "placa": "ABC123"\n}');
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  if (!selectedType) {
    return (
      <EmptyState
        title="Seleccione un tipo de trámite"
        description="Elija un tipo en Familias / Tipos para consultar reglas y simular evaluación."
      />
    );
  }

  if (matrixQuery.isLoading || rulesQuery.isLoading) {
    return <LoadingSkeleton rows={6} />;
  }

  if (matrixQuery.isError) {
    return (
      <ErrorState
        error={matrixQuery.error}
        onRetry={() => matrixQuery.refetch()}
      />
    );
  }

  const procedureTypeId = matrixQuery.data?.procedureTypeId ?? selectedType.id;
  const rules = rulesQuery.data ?? [];
  const simulation = simulateMutation.data;

  async function handleSimulate(event: React.FormEvent) {
    event.preventDefault();
    setJsonError(null);
    setFieldErrors({});

    let capturedFields: Record<string, string | null>;
    try {
      const parsed = JSON.parse(payloadJson) as unknown;
      if (
        typeof parsed !== "object" ||
        parsed === null ||
        Array.isArray(parsed)
      ) {
        throw new Error("El payload debe ser un objeto JSON.");
      }
      capturedFields = Object.fromEntries(
        Object.entries(parsed as Record<string, unknown>).map(
          ([key, value]) => [
            key,
            value === null || value === undefined ? null : String(value),
          ],
        ),
      );
    } catch (parseError) {
      setJsonError(
        parseError instanceof Error
          ? parseError.message
          : "JSON inválido en el payload.",
      );
      return;
    }

    try {
      await simulateMutation.mutateAsync({
        tenantId,
        procedureTypeId,
        capturedFields,
      });
    } catch (simulateError) {
      const parsed = parseApiFieldErrors(simulateError);
      setFieldErrors(parsed.fields);
      if (parsed.general) {
        setJsonError(parsed.general);
      }
    }
  }

  return (
    <div className="space-y-6">
      <section className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card">
        <h2 className="text-base font-bold text-flit-blueText">
          Reglas — {selectedType.name}
        </h2>
        {rulesQuery.isError ? (
          <p className="mt-3 text-sm text-flit-muted" role="status">
            No se pudo cargar el catálogo de reglas (permiso
            modulo.tramites.crud-total). Use el simulador si tiene permiso
            ui.parametrizacion.simular-reglas.
          </p>
        ) : rules.length === 0 ? (
          <p className="mt-3 text-sm text-flit-muted">
            No hay reglas configuradas para este tipo.
          </p>
        ) : (
          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-flit-bg">
                <tr>
                  <th scope="col" className="px-3 py-2 text-left font-semibold">
                    Nombre
                  </th>
                  <th scope="col" className="px-3 py-2 text-left font-semibold">
                    Prioridad
                  </th>
                  <th scope="col" className="px-3 py-2 text-left font-semibold">
                    Activa
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-flit-draft/10">
                {rules.map((rule) => (
                  <tr key={rule.id}>
                    <td className="px-3 py-2 text-flit-blueDark">
                      {rule.name}
                    </td>
                    <td className="px-3 py-2 text-flit-muted">
                      {rule.priority}
                    </td>
                    <td className="px-3 py-2 text-flit-muted">
                      {rule.isActive ? "Sí" : "No"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {canSimulate ? (
        <section
          aria-labelledby="simulator-heading"
          className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card"
        >
          <h2
            id="simulator-heading"
            className="text-base font-bold text-flit-blueText"
          >
            Simulador de reglas
          </h2>
          <form className="mt-4 space-y-3" onSubmit={handleSimulate} noValidate>
            <div>
              <label
                htmlFor="rules-payload"
                className="text-sm font-semibold text-flit-blueDark"
              >
                Payload JSON (capturedFields)
              </label>
              <textarea
                id="rules-payload"
                value={payloadJson}
                onChange={(event) => setPayloadJson(event.target.value)}
                rows={6}
                className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 font-mono text-xs"
                aria-invalid={Boolean(jsonError || fieldErrors.tenantId)}
                aria-describedby={
                  jsonError || fieldErrors.tenantId
                    ? "rules-payload-error"
                    : undefined
                }
              />
              {(jsonError || fieldErrors.tenantId) && (
                <p
                  id="rules-payload-error"
                  className="mt-1 text-xs text-flit-danger"
                  role="alert"
                >
                  {jsonError ?? fieldErrors.tenantId}
                </p>
              )}
            </div>
            <GradientButton
              type="submit"
              className="!h-10 !w-auto !px-6 !text-sm"
              disabled={simulateMutation.isPending}
            >
              {simulateMutation.isPending ? "Simulando…" : "Simular evaluación"}
            </GradientButton>
          </form>

          {simulation && <SimulationResults simulation={simulation} />}
        </section>
      ) : (
        <p className="text-sm text-flit-muted">
          Requiere permiso ui.parametrizacion.simular-reglas para ejecutar el
          simulador.
        </p>
      )}
    </div>
  );
}

type SimulationResultsProps = {
  simulation: NonNullable<ReturnType<typeof useSimulateRules>["data"]>;
};

function SimulationResults({ simulation }: SimulationResultsProps) {
  return (
    <div className="mt-6 space-y-4">
      <ResultTable
        title="Reglas coincidentes"
        emptyLabel="Ninguna regla coincidió."
        headers={["Regla", "Prioridad", "Acciones"]}
        rows={simulation.matchedRules.map((rule) => [
          rule.ruleName,
          String(rule.priority),
          rule.actions.map((action) => action.type).join(", ") || "—",
        ])}
      />
      <ResultTable
        title="Acciones resultantes"
        emptyLabel="Sin acciones."
        headers={["Tipo", "Parámetros"]}
        rows={simulation.actions.map((action) => [
          action.type,
          action.params ? JSON.stringify(action.params) : "—",
        ])}
      />
      <ResultTable
        title="Conflictos"
        emptyLabel="Sin conflictos detectados."
        headers={["Tipo", "Target", "Acciones", "Reglas"]}
        rows={simulation.conflicts.map((conflict) => [
          conflict.conflictType,
          conflict.target,
          conflict.actionTypes.join(", ") || "—",
          conflict.ruleIds.join(", ") || "—",
        ])}
      />
    </div>
  );
}

type ResultTableProps = {
  title: string;
  emptyLabel: string;
  headers: string[];
  rows: string[][];
};

function ResultTable({ title, emptyLabel, headers, rows }: ResultTableProps) {
  return (
    <div>
      <h3 className="text-sm font-bold text-flit-blueText">{title}</h3>
      {rows.length === 0 ? (
        <p className="mt-2 text-sm text-flit-muted">{emptyLabel}</p>
      ) : (
        <div className="mt-2 overflow-x-auto rounded-[8px] border border-flit-draft/15">
          <table className="min-w-full text-xs">
            <thead className="bg-flit-bg">
              <tr>
                {headers.map((header) => (
                  <th
                    key={header}
                    scope="col"
                    className="px-3 py-2 text-left font-semibold text-flit-blueDark"
                  >
                    {header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-flit-draft/10">
              {rows.map((row, index) => (
                <tr key={`${title}-${index}`}>
                  {row.map((cell, cellIndex) => (
                    <td
                      key={`${title}-${index}-${cellIndex}`}
                      className="px-3 py-2 text-flit-blueDark"
                    >
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
