import { useEffect, useState, type DragEvent } from "react";
import {
  useOtConsolidatedOrder,
  useSaveOtConsolidatedOrder,
} from "../api/ot-admin.api.js";
import type { OtConsolidatedOrderItem } from "../api/ot-admin.schemas.js";
import { useCanReorderOtDocuments } from "../hooks/useCanAdministerOt.js";
import { itemDisplayLabel, reorderItems } from "../lib/documentOrderUtils.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type DocumentOrderTabProps = {
  agencyId: string;
};

export function DocumentOrderTab({ agencyId }: DocumentOrderTabProps) {
  const canReorder = useCanReorderOtDocuments();
  const orderQuery = useOtConsolidatedOrder(agencyId);
  const saveMutation = useSaveOtConsolidatedOrder(agencyId);
  const [localItems, setLocalItems] = useState<OtConsolidatedOrderItem[]>([]);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  useEffect(() => {
    if (orderQuery.data?.items) {
      setLocalItems(orderQuery.data.items);
    }
  }, [orderQuery.data?.items]);

  const isDirty =
    JSON.stringify(localItems) !== JSON.stringify(orderQuery.data?.items ?? []);

  if (orderQuery.isLoading) {
    return <LoadingSkeleton rows={6} />;
  }

  if (orderQuery.isError) {
    return (
      <ErrorState
        error={orderQuery.error}
        onRetry={() => orderQuery.refetch()}
      />
    );
  }

  if (!orderQuery.data) {
    return (
      <EmptyState
        title="Sin orden configurado"
        description="No se pudo cargar el orden del consolidado."
      />
    );
  }

  function handleDragStart(index: number) {
    if (!canReorder) {
      return;
    }
    setDragIndex(index);
  }

  function handleDragOver(event: DragEvent, index: number) {
    event.preventDefault();
    if (!canReorder || dragIndex === null || dragIndex === index) {
      return;
    }
    setLocalItems((current) => reorderItems(current, dragIndex, index));
    setDragIndex(index);
  }

  function handleDragEnd() {
    setDragIndex(null);
  }

  function handleSave() {
    setStatusMessage(null);
    saveMutation.mutate(
      {
        items: localItems.map((item) => ({
          id: item.id,
          position: item.position,
          source: item.source,
          procedureDocumentCatalogId:
            item.procedureDocumentCatalogId ?? undefined,
          customLabel: item.customLabel ?? undefined,
        })),
      },
      {
        onSuccess: () => {
          setStatusMessage("Orden guardado correctamente.");
        },
        onError: () => {
          setStatusMessage("No se pudo guardar el orden. Intente nuevamente.");
        },
      },
    );
  }

  return (
    <div className="space-y-4">
      {!canReorder && (
        <p className="rounded-flit-card border border-flit-border bg-flit-bg px-4 py-3 text-sm text-flit-muted">
          Solo lectura: requiere permiso{" "}
          <span className="font-mono text-xs">ui.ot.reordenar-documentos</span>{" "}
          para arrastrar y guardar cambios.
        </p>
      )}

      {localItems.length === 0 ? (
        <EmptyState
          title="Sin documentos en el consolidado"
          description="Agregue ítems desde el backend o configure el catálogo de documentos del OT."
        />
      ) : (
        <ol
          aria-label="Orden de documentos del consolidado"
          className="space-y-2"
        >
          {localItems.map((item, index) => (
            <li key={item.id}>
              <div
                draggable={canReorder}
                onDragStart={() => handleDragStart(index)}
                onDragOver={(event) => handleDragOver(event, index)}
                onDragEnd={handleDragEnd}
                className={`flex items-center gap-3 rounded-flit-card border bg-flit-card px-4 py-3 shadow-flit-card ${
                  dragIndex === index
                    ? "border-flit-blue ring-2 ring-flit-blue/30"
                    : "border-flit-border"
                } ${canReorder ? "cursor-grab active:cursor-grabbing" : ""}`}
              >
                <span
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-flit-bg text-sm font-bold text-flit-blue"
                  aria-hidden="true"
                >
                  {item.position}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-flit-blueText">
                    {itemDisplayLabel(item)}
                  </p>
                  <p className="text-xs text-flit-muted">
                    Fuente: {item.source}
                    {item.catalogCode ? ` · ${item.catalogCode}` : ""}
                  </p>
                </div>
                {canReorder && (
                  <span
                    className="text-flit-muted"
                    aria-label={`Arrastrar documento ${itemDisplayLabel(item)}`}
                  >
                    <GripIcon />
                  </span>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}

      {canReorder && localItems.length > 0 && (
        <div className="flex flex-wrap items-center gap-4">
          <GradientButton
            type="button"
            className="!h-11 !w-auto !px-6 !text-sm"
            disabled={!isDirty || saveMutation.isPending}
            onClick={handleSave}
          >
            {saveMutation.isPending ? "Guardando…" : "Guardar orden"}
          </GradientButton>
          {statusMessage && (
            <p
              className="text-sm text-flit-blueText"
              role="status"
              aria-live="polite"
            >
              {statusMessage}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function GripIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="currentColor"
      aria-hidden="true"
    >
      <circle cx="9" cy="7" r="1.5" />
      <circle cx="15" cy="7" r="1.5" />
      <circle cx="9" cy="12" r="1.5" />
      <circle cx="15" cy="12" r="1.5" />
      <circle cx="9" cy="17" r="1.5" />
      <circle cx="15" cy="17" r="1.5" />
    </svg>
  );
}
