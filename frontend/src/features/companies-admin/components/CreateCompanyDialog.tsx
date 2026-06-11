import { useEffect, useId, useState } from "react";
import { useCreateCompany } from "../api/companies.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";

type CreateCompanyDialogProps = {
  open: boolean;
  onClose: () => void;
  onSuccess: (legalName: string) => void;
};

export function CreateCompanyDialog({
  open,
  onClose,
  onSuccess,
}: CreateCompanyDialogProps) {
  const titleId = useId();
  const {
    mutateAsync: createCompanyAsync,
    reset: resetCreateCompany,
    isPending,
    isError,
    error,
  } = useCreateCompany();
  const [nit, setNit] = useState("");
  const [legalName, setLegalName] = useState("");
  const [commercialName, setCommercialName] = useState("");
  const [contactEmail, setContactEmail] = useState("");

  useEffect(() => {
    if (!open) {
      return;
    }
    setNit("");
    setLegalName("");
    setCommercialName("");
    setContactEmail("");
    resetCreateCompany();
  }, [open, resetCreateCompany]);

  if (!open) {
    return null;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      const created = await createCompanyAsync({
        nit: nit.trim(),
        legalName: legalName.trim(),
        commercialName: commercialName.trim() || undefined,
        contactEmail: contactEmail.trim() || undefined,
      });
      onSuccess(created.legalName);
      onClose();
    } catch {
      // Error shown via createCompany.isError
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
          Nueva compañía B2B
        </h2>
        <p className="mt-2 text-sm text-flit-draft">
          Se creará el tenant, el registro maestro y la billetera de firmas. El
          slug se genera automáticamente desde la razón social.
        </p>

        <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
          <TextField
            label="NIT"
            name="company-nit"
            required
            autoComplete="off"
            value={nit}
            onChange={(event) => setNit(event.target.value)}
            placeholder="900123456-7"
          />

          <TextField
            label="Razón social"
            name="company-legal-name"
            required
            autoComplete="organization"
            value={legalName}
            onChange={(event) => setLegalName(event.target.value)}
            placeholder="Transportes Ejemplo S.A.S."
          />

          <TextField
            label="Nombre comercial"
            name="company-commercial-name"
            autoComplete="off"
            value={commercialName}
            onChange={(event) => setCommercialName(event.target.value)}
            placeholder="Opcional"
          />

          <TextField
            label="Correo de contacto"
            name="company-contact-email"
            type="email"
            autoComplete="email"
            value={contactEmail}
            onChange={(event) => setContactEmail(event.target.value)}
            placeholder="contacto@empresa.com"
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
              {isPending ? "Creando…" : "Crear compañía"}
            </GradientButton>
          </div>
        </form>
      </div>
    </div>
  );
}
