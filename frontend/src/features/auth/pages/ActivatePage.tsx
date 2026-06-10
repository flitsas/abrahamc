import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { AuthShell } from "../../../shared/components/flit/AuthShell.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import {
  useActivateAccount,
  useInvitationPreview,
} from "../api/onboarding.api.js";
import {
  describePasswordPolicy,
  validatePassword,
} from "../lib/passwordPolicy.js";

export function ActivatePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const activate = useActivateAccount();
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);
  const [completed, setCompleted] = useState(false);

  const invitationParams = useMemo(() => {
    const invitationId = searchParams.get("invitationId");
    const token = searchParams.get("token");
    const signature = searchParams.get("signature");

    if (!invitationId || !token || !signature) {
      return null;
    }

    return { invitationId, token, signature };
  }, [searchParams]);

  const preview = useInvitationPreview(invitationParams);

  if (!invitationParams) {
    return (
      <AuthShell
        title="Enlace inválido"
        subtitle="No pudimos validar tu invitación."
      >
        <InvitationError message="El enlace de activación está incompleto o no es válido." />
      </AuthShell>
    );
  }

  if (preview.isLoading) {
    return (
      <AuthShell title="Activar cuenta" subtitle="Validando tu invitación…">
        <LoadingSkeleton rows={4} />
      </AuthShell>
    );
  }

  if (preview.isError) {
    return (
      <AuthShell
        title="No se puede activar"
        subtitle="Tu invitación expiró o ya fue utilizada."
      >
        <InvitationError message={preview.error.message} />
      </AuthShell>
    );
  }

  const { email, passwordPolicy } = preview.data!;

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLocalError(null);

    if (password !== confirmPassword) {
      setLocalError("Las contraseñas no coinciden.");
      return;
    }

    const policyError = validatePassword(password, passwordPolicy);
    if (policyError) {
      setLocalError(policyError);
      return;
    }

    try {
      await activate.mutateAsync({
        invitationId: invitationParams!.invitationId,
        token: invitationParams!.token,
        signature: invitationParams!.signature,
        password,
      });
      setCompleted(true);
      window.setTimeout(
        () => navigate("/login?reason=account-activated", { replace: true }),
        2500,
      );
    } catch {
      // Error shown via activate.error
    }
  }

  if (completed) {
    return (
      <AuthShell
        title="Cuenta activada"
        subtitle={`La cuenta ${email} ya está lista para usar.`}
      >
        <div
          className="rounded-[10px] border border-flit-success/30 bg-flit-success/10 px-4 py-4 text-sm text-flit-blueDark"
          role="status"
        >
          Redirigiendo al inicio de sesión…
        </div>
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title="Activar cuenta"
      subtitle="Define tu contraseña para completar el registro."
    >
      <form className="space-y-5" onSubmit={handleSubmit} noValidate>
        <TextField
          label="Correo electrónico"
          name="email"
          type="email"
          value={email}
          readOnly
          aria-readonly="true"
        />

        <TextField
          label="Contraseña"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          placeholder="••••••••"
        />

        <TextField
          label="Confirmar contraseña"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          required
          value={confirmPassword}
          onChange={(event) => setConfirmPassword(event.target.value)}
          placeholder="••••••••"
        />

        <ul
          className="space-y-1 text-xs text-flit-muted"
          aria-label="Requisitos de contraseña"
        >
          {describePasswordPolicy(passwordPolicy).map((rule) => (
            <li key={rule} className="flex items-center gap-2">
              <span
                className="h-1.5 w-1.5 rounded-full bg-flit-blueText"
                aria-hidden="true"
              />
              {rule}
            </li>
          ))}
        </ul>

        {(localError || activate.isError) && (
          <div
            className="rounded-[10px] border border-flit-warning/30 bg-flit-warning/10 px-4 py-3 text-sm text-flit-danger"
            role="alert"
          >
            {localError ?? activate.error?.message}
          </div>
        )}

        <GradientButton
          type="submit"
          disabled={activate.isPending}
          variant="success"
        >
          {activate.isPending ? "Activando…" : "Activar cuenta"}
        </GradientButton>
      </form>
    </AuthShell>
  );
}

function InvitationError({ message }: { message: string }) {
  return (
    <>
      <ErrorState title="Invitación no disponible" error={message} />
      <p className="mt-4 text-sm text-flit-draft">
        Solicita una nueva invitación al administrador de tu organización.
      </p>
      <Link
        to="/login"
        className="mt-4 inline-flex text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
      >
        Ir al inicio de sesión
      </Link>
    </>
  );
}
