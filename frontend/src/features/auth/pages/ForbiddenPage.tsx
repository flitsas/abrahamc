import { Link } from "react-router-dom";
import { FlitCard } from "../../../shared/components/flit/FlitCard.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";

export function ForbiddenPage() {
  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Acceso denegado"
        subtitle="No tiene permisos para ver este módulo. Si cree que es un error, contacte al administrador de su compañía."
      />

      <FlitCard className="max-w-xl">
        <p className="text-sm text-flit-draft" role="alert">
          Código HTTP 403 — permiso insuficiente para esta sección.
        </p>
        <div className="mt-6">
          <Link to="/" className="inline-block">
            <GradientButton className="!h-12 !w-auto !px-8 !text-sm">
              Volver al inicio
            </GradientButton>
          </Link>
        </div>
      </FlitCard>
    </div>
  );
}
