import { useEffect, useId, useState } from "react";
import type { CompanyRow } from "../api/companies.schemas.js";
import { useUpdateCompany } from "../api/companies.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";

type EditCompanyDialogProps = {
  open: boolean;
  company: CompanyRow;
  onClose: () => void;
  onSuccess: (legalName: string) => void;
};

export function EditCompanyDialog({
  open,
  company,
  onClose,
  onSuccess,
}: EditCompanyDialogProps) {
  const titleId = useId();
  const {
    mutateAsync: updateCompanyAsync,
    reset: resetUpdateCompany,
    isPending,
    isError,
    error,
  } = useUpdateCompany(company.id);
  const [legalName, setLegalName] = useState(company.legalName);
  const [commercialName, setCommercialName] = useState(
    company.commercialName ?? "",
  );

  useEffect(() => {
    if (!open) {
      return;
    }
    setLegalName(company.legalName);
    setCommercialName(company.commercialName ?? "");
    resetUpdateCompany();
  }, [
    open,
    company.id,
    company.legalName,
    company.commercialName,
    resetUpdateCompany,
  ]);

  if (!open) {
    return null;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      await updateCompanyAsync({
        legalName: legalName.trim(),
        commercialName: commercialName.trim() || undefined,
        modulesEnabledJson: company.modulesEnabledJson,
      });
      onSuccess(legalName.trim());
      onClose();
    } catch {
      // Error shown via updateCompany.isError
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-flit-blueDark/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      <div className="w-full max-w-lg rounded-flit-card bg-flit-card p-6 shadow-flit-card">
        <h2 id={titleId} className="text-lg font-bold text-flit-blueText">
          Editar datos de la compañía
        </h2>
        <p className="mt-2 text-sm text-flit-draft">
          El NIT no se puede modificar desde esta pantalla. Para corregir la
          configuración por módulo, use las pestañas de abajo.
        </p>

        <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
          <TextField
            label="NIT"
            name="company-nit"
            value={company.nit}
            readOnly
            disabled
          />

          <TextField
            label="Razón social"
            name="company-legal-name"
            required
            autoComplete="organization"
            value={legalName}
            onChange={(event) => setLegalName(event.target.value)}
          />

          <TextField
            label="Nombre comercial"
            name="company-commercial-name"
            autoComplete="off"
            value={commercialName}
            onChange={(event) => setCommercialName(event.target.value)}
            placeholder="Opcional"
          />

          {isError && (
            <p className="text-sm text-flit-danger" role="alert">
              {error.message}
            </p>
          )}

          <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={onClose}
              className="rounded-flit-pill border border-flit-draft/30 px-6 py-3 text-sm font-semibold text-flit-blueDark hover:bg-flit-bg focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
            >
              Cancelar
            </button>
            <GradientButton
              type="submit"
              disabled={isPending}
              className="!h-12 !w-auto !px-8 !text-sm"
            >
              {isPending ? "Guardando…" : "Guardar cambios"}
            </GradientButton>
          </div>
        </form>
      </div>
    </div>
  );
}
