import { useEffect, useMemo, useState } from "react";
import type { ProcedureTypeSummary } from "../api/parametrizacion.schemas.js";
import {
  useCatalogEdges,
  useCatalogFamilies,
  useCreateProcedureType,
  usePatchProcedureEdge,
  useProcedureMatrix,
  useProcedureTypes,
} from "../api/parametrizacion.api.js";
import { useCanManageParametrizacion } from "../hooks/useCanManageParametrizacion.js";
import { parseApiFieldErrors, type FieldErrors } from "../lib/parseApiFieldErrors.js";
import { validateMinActiveDataEdges } from "../lib/pipelineValidation.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type OrderedEdge = {
  edgeCode: string;
  name: string;
  isActive: boolean;
  displayOrder: number;
};

type PipelineModelerTabProps = {
  tenantId: string;
  selectedType: ProcedureTypeSummary | null;
  onTypeCreated: (code: string) => void;
};

export function PipelineModelerTab({
  tenantId,
  selectedType,
  onTypeCreated,
}: PipelineModelerTabProps) {
  const canManage = useCanManageParametrizacion();
  const edgesCatalogQuery = useCatalogEdges();
  const familiesQuery = useCatalogFamilies();
  const typesQuery = useProcedureTypes(tenantId);
  const matrixQuery = useProcedureMatrix(
    selectedType
      ? { tenantId, typeCode: selectedType.code }
      : null,
    Boolean(selectedType),
  );
  const patchEdge = usePatchProcedureEdge();
  const createType = useCreateProcedureType();

  const [orderedEdges, setOrderedEdges] = useState<OrderedEdge[]>([]);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const [newTypeCode, setNewTypeCode] = useState("");
  const [newTypeName, setNewTypeName] = useState("");
  const [newTypeSlug, setNewTypeSlug] = useState("");
  const [newTypeFamily, setNewTypeFamily] = useState("");

  useEffect(() => {
    if (matrixQuery.data) {
      setOrderedEdges(
        [...matrixQuery.data.edges]
          .sort((a, b) => a.displayOrder - b.displayOrder)
          .map((edge) => ({
            edgeCode: edge.code,
            name: edge.name,
            isActive: edge.isActive,
            displayOrder: edge.displayOrder,
          })),
      );
      return;
    }

    if (edgesCatalogQuery.data && !selectedType) {
      setOrderedEdges(
        [...edgesCatalogQuery.data.items]
          .sort((a, b) => a.displayOrder - b.displayOrder)
          .map((edge, index) => ({
            edgeCode: edge.code,
            name: edge.name,
            isActive: index < 5,
            displayOrder: index + 1,
          })),
      );
    }
  }, [matrixQuery.data, edgesCatalogQuery.data, selectedType]);

  const pipelineValidation = useMemo(
    () =>
      validateMinActiveDataEdges(
        orderedEdges.map((edge) => ({
          edgeCode: edge.edgeCode,
          isActive: edge.isActive,
        })),
      ),
    [orderedEdges],
  );

  function reorderEdges(fromIndex: number, toIndex: number) {
    setOrderedEdges((current) => {
      const next = [...current];
      const [moved] = next.splice(fromIndex, 1);
      next.splice(toIndex, 0, moved);
      return next.map((edge, index) => ({
        ...edge,
        displayOrder: index + 1,
      }));
    });
  }

  function toggleEdgeActive(edgeCode: string) {
    setOrderedEdges((current) =>
      current.map((edge) =>
        edge.edgeCode === edgeCode
          ? { ...edge, isActive: !edge.isActive }
          : edge,
      ),
    );
  }

  async function handleSaveExistingPipeline() {
    if (!selectedType || !canManage) {
      return;
    }
    setFieldErrors({});
    setStatusMessage(null);

    if (!pipelineValidation.isValid) {
      setFieldErrors({ edges: pipelineValidation.message ?? "Pipeline inválido." });
      return;
    }

    const original = new Map(
      (matrixQuery.data?.edges ?? []).map((edge) => [edge.code, edge.isActive]),
    );
    const changed = orderedEdges.filter(
      (edge) => original.get(edge.edgeCode) !== edge.isActive,
    );

    try {
      for (const edge of changed) {
        await patchEdge.mutateAsync({
          tenantId,
          typeCode: selectedType.code,
          edgeCode: edge.edgeCode,
          isActive: edge.isActive,
        });
      }
      setStatusMessage("Pipeline actualizado correctamente.");
    } catch (saveError) {
      const parsed = parseApiFieldErrors(saveError);
      setFieldErrors(parsed.fields);
      if (parsed.general) {
        setFieldErrors((current) => ({ ...current, edges: parsed.general! }));
      }
    }
  }

  async function handleCreateType(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setStatusMessage(null);

    if (!pipelineValidation.isValid) {
      setFieldErrors({ edges: pipelineValidation.message ?? "Pipeline inválido." });
      return;
    }

    try {
      const created = await createType.mutateAsync({
        tenantId,
        familyCode: newTypeFamily,
        code: newTypeCode.trim().toUpperCase(),
        slug: newTypeSlug.trim().toLowerCase(),
        name: newTypeName.trim(),
        maxSteps: 4,
        edges: orderedEdges.map((edge, index) => ({
          edgeCode: edge.edgeCode,
          isActive: edge.isActive,
          isRequired: edge.isActive,
          displayOrder: index + 1,
        })),
      });
      setStatusMessage(`Tipo «${created.name}» creado.`);
      onTypeCreated(created.code);
      void typesQuery.refetch();
    } catch (createError) {
      const parsed = parseApiFieldErrors(createError);
      setFieldErrors(parsed.fields);
    }
  }

  if (edgesCatalogQuery.isLoading || (selectedType && matrixQuery.isLoading)) {
    return <LoadingSkeleton rows={6} />;
  }

  const loadError = edgesCatalogQuery.error ?? matrixQuery.error;

  if (edgesCatalogQuery.isError || matrixQuery.isError) {
    return (
      <ErrorState
        error={loadError ?? new Error("Error al cargar el pipeline.")}
        onRetry={() => {
          void edgesCatalogQuery.refetch();
          void matrixQuery.refetch();
        }}
      />
    );
  }

  if (orderedEdges.length === 0) {
    return (
      <EmptyState
        title="Sin pasos de pipeline"
        description="El catálogo de aristas no está disponible. Verifique la configuración del backend."
      />
    );
  }

  const families = familiesQuery.data?.items ?? [];

  return (
    <div className="space-y-6">
      {statusMessage && (
        <div
          className="rounded-[10px] border border-flit-green/30 bg-flit-green/10 px-4 py-3 text-sm text-flit-green"
          role="status"
        >
          {statusMessage}
        </div>
      )}

      <section
        aria-labelledby="pipeline-steps-heading"
        className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card"
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2
            id="pipeline-steps-heading"
            className="text-base font-bold text-flit-blueText"
          >
            {selectedType
              ? `Pipeline — ${selectedType.name}`
              : "Nuevo pipeline (crear tipo)"}
          </h2>
          <p
            className={`text-xs font-semibold ${
              pipelineValidation.isValid ? "text-flit-green" : "text-flit-danger"
            }`}
            role="status"
          >
            {pipelineValidation.isValid
              ? "Mínimo 4 pasos activos cumplido"
              : pipelineValidation.message}
          </p>
        </div>

        {fieldErrors.edges && (
          <p className="mt-3 text-sm text-flit-danger" role="alert">
            {fieldErrors.edges}
          </p>
        )}

        <ol className="mt-4 space-y-2" aria-label="Pasos del pipeline">
          {orderedEdges.map((edge, index) => (
            <PipelineStepRow
              key={edge.edgeCode}
              edge={edge}
              index={index}
              dragIndex={dragIndex}
              canManage={canManage}
              onDragStart={() => setDragIndex(index)}
              onDragOver={(event) => event.preventDefault()}
              onDrop={() => {
                if (dragIndex !== null && dragIndex !== index) {
                  reorderEdges(dragIndex, index);
                }
                setDragIndex(null);
              }}
              onToggleActive={() => toggleEdgeActive(edge.edgeCode)}
            />
          ))}
        </ol>

        {selectedType ? (
          canManage && (
            <GradientButton
              type="button"
              className="mt-4 !h-10 !w-auto !px-6 !text-sm"
              disabled={patchEdge.isPending || !pipelineValidation.isValid}
              onClick={() => void handleSaveExistingPipeline()}
            >
              {patchEdge.isPending ? "Guardando…" : "Guardar pipeline"}
            </GradientButton>
          )
        ) : (
          canManage && (
            <form
              className="mt-6 grid gap-4 border-t border-flit-draft/10 pt-4 sm:grid-cols-2"
              onSubmit={handleCreateType}
              noValidate
            >
              <FieldInput
                id="type-code"
                label="Código tipo"
                name="code"
                value={newTypeCode}
                error={fieldErrors.code}
                onChange={setNewTypeCode}
              />
              <FieldInput
                id="type-slug"
                label="Slug"
                name="slug"
                value={newTypeSlug}
                error={fieldErrors.slug}
                onChange={setNewTypeSlug}
              />
              <FieldInput
                id="type-name"
                label="Nombre"
                name="name"
                value={newTypeName}
                error={fieldErrors.name}
                onChange={setNewTypeName}
              />
              <div>
                <label
                  htmlFor="type-family"
                  className="text-sm font-semibold text-flit-blueDark"
                >
                  Familia
                </label>
                <select
                  id="type-family"
                  name="familyCode"
                  value={newTypeFamily}
                  onChange={(event) => setNewTypeFamily(event.target.value)}
                  className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
                  aria-invalid={Boolean(fieldErrors.familyCode)}
                >
                  <option value="">Seleccione…</option>
                  {families.map((family) => (
                    <option key={family.id} value={family.code}>
                      {family.name}
                    </option>
                  ))}
                </select>
                {fieldErrors.familyCode && (
                  <p className="mt-1 text-xs text-flit-danger" role="alert">
                    {fieldErrors.familyCode}
                  </p>
                )}
              </div>
              <div className="sm:col-span-2">
                <GradientButton
                  type="submit"
                  className="!h-10 !w-auto !px-6 !text-sm"
                  disabled={createType.isPending || !pipelineValidation.isValid}
                >
                  {createType.isPending ? "Creando…" : "Crear tipo con pipeline"}
                </GradientButton>
              </div>
            </form>
          )
        )}
      </section>
    </div>
  );
}

type PipelineStepRowProps = {
  edge: OrderedEdge;
  index: number;
  dragIndex: number | null;
  canManage: boolean;
  onDragStart: () => void;
  onDragOver: (event: React.DragEvent) => void;
  onDrop: () => void;
  onToggleActive: () => void;
};

function PipelineStepRow({
  edge,
  index,
  dragIndex,
  canManage,
  onDragStart,
  onDragOver,
  onDrop,
  onToggleActive,
}: PipelineStepRowProps) {
  return (
    <li
      draggable={canManage}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDrop={onDrop}
      className={`flex items-center gap-3 rounded-[10px] border px-3 py-3 ${
        dragIndex === index
          ? "border-flit-blue bg-flit-blue/5"
          : "border-flit-draft/20 bg-white"
      }`}
    >
      <span
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-flit-bg text-xs font-bold text-flit-blue"
        aria-hidden="true"
      >
        {index + 1}
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-semibold text-flit-blueDark">{edge.name}</p>
        <p className="font-mono text-xs text-flit-muted">{edge.edgeCode}</p>
      </div>
      <label className="inline-flex items-center gap-2 text-xs font-semibold text-flit-blueDark">
        <input
          type="checkbox"
          checked={edge.isActive}
          disabled={!canManage}
          onChange={onToggleActive}
          aria-label={`Activar paso ${edge.name}`}
        />
        Activo
      </label>
      {canManage && (
        <span className="cursor-grab text-flit-muted" aria-hidden="true">
          ⠿
        </span>
      )}
    </li>
  );
}

type FieldInputProps = {
  id: string;
  label: string;
  name: string;
  value: string;
  error?: string;
  onChange: (value: string) => void;
};

function FieldInput({
  id,
  label,
  name,
  value,
  error,
  onChange,
}: FieldInputProps) {
  return (
    <div>
      <label htmlFor={id} className="text-sm font-semibold text-flit-blueDark">
        {label}
      </label>
      <input
        id={id}
        name={name}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
      />
      {error && (
        <p id={`${id}-error`} className="mt-1 text-xs text-flit-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
