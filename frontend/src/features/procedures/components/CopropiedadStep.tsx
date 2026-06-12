import { TextField } from "../../../shared/components/flit/TextField.js";
import type { ProcedureOwner } from "../api/procedure-wizard.schemas.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";

type CopropiedadStepProps = {
  owners: ProcedureOwner[];
  totalPercentage: number;
  ownershipValid: boolean | null;
  validating: boolean;
  onChange: (index: number, owner: ProcedureOwner) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
};

const EMPTY_OWNER: ProcedureOwner = {
  documentTypeCode: "CC",
  documentNumber: "",
  fullName: "",
  ownershipPercentage: 0,
};

export function createEmptyOwner(): ProcedureOwner {
  return { ...EMPTY_OWNER };
}

export function CopropiedadStep({
  owners,
  totalPercentage,
  ownershipValid,
  validating,
  onChange,
  onAdd,
  onRemove,
}: CopropiedadStepProps) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-flit-muted">
        Registre los copropietarios del vehículo. La suma de porcentajes debe
        ser exactamente 100 %.
      </p>

      <ul className="space-y-4" role="list">
        {owners.map((owner, index) => (
          <li
            key={`owner-${index}`}
            className="rounded-[10px] border border-flit-border bg-white p-4"
          >
            <div className="mb-3 flex items-center justify-between gap-2">
              <h3 className="text-sm font-semibold text-flit-blueDark">
                Copropietario {index + 1}
              </h3>
              {owners.length > 1 && (
                <button
                  type="button"
                  onClick={() => onRemove(index)}
                  className="text-xs font-semibold text-flit-danger underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                >
                  Quitar
                </button>
              )}
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                id={`owner-doc-type-${index}`}
                label="Tipo documento"
                value={owner.documentTypeCode}
                onChange={(event) =>
                  onChange(index, {
                    ...owner,
                    documentTypeCode: event.target.value,
                  })
                }
              />
              <TextField
                id={`owner-doc-number-${index}`}
                label="Número documento"
                value={owner.documentNumber}
                onChange={(event) =>
                  onChange(index, {
                    ...owner,
                    documentNumber: event.target.value,
                  })
                }
              />
              <TextField
                id={`owner-name-${index}`}
                label="Nombre completo"
                value={owner.fullName}
                onChange={(event) =>
                  onChange(index, { ...owner, fullName: event.target.value })
                }
                className="sm:col-span-2"
              />
              <TextField
                id={`owner-percentage-${index}`}
                label="Porcentaje (%)"
                type="number"
                min={0}
                max={100}
                value={String(owner.ownershipPercentage)}
                onChange={(event) =>
                  onChange(index, {
                    ...owner,
                    ownershipPercentage: Number(event.target.value) || 0,
                  })
                }
              />
            </div>
          </li>
        ))}
      </ul>

      <GradientButton
        type="button"
        variant="success"
        className="!h-11 !w-auto !px-6 !text-sm"
        onClick={onAdd}
      >
        Agregar copropietario
      </GradientButton>

      <div
        className="rounded-[10px] border border-flit-border bg-flit-bg px-4 py-3"
        role="status"
        aria-live="polite"
      >
        <p className="text-sm font-semibold text-flit-blueDark">
          Total: {totalPercentage.toFixed(1)} %
        </p>
        {validating && (
          <p className="mt-1 text-xs text-flit-muted">Validando copropiedad…</p>
        )}
        {!validating && ownershipValid === false && (
          <p className="mt-1 text-xs text-flit-danger" role="alert">
            La suma debe ser 100 % para continuar.
          </p>
        )}
        {!validating && ownershipValid === true && (
          <p className="mt-1 text-xs text-flit-green">
            Copropiedad válida — puede continuar.
          </p>
        )}
      </div>
    </div>
  );
}

export function sumOwnershipPercentages(owners: ProcedureOwner[]): number {
  return owners.reduce(
    (total, owner) => total + (owner.ownershipPercentage || 0),
    0,
  );
}
