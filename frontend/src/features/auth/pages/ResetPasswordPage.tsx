import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { AuthShell } from "../../../shared/components/flit/AuthShell.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { useConfirmPasswordReset } from "../api/password-reset.api.js";
import {
  describePasswordPolicy,
  validatePassword,
} from "../lib/passwordPolicy.js";
import type { PasswordPolicy } from "../api/onboarding.schemas.js";

const defaultPolicy: PasswordPolicy = {
  minLength: 8,
  requireUppercase: true,
  requireLowercase: true,
  requireDigit: true,
  requireSymbol: true,
};

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const confirmReset = useConfirmPasswordReset();
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);
  const [completed, setCompleted] = useState(false);

  if (!token) {
    return (
      <AuthShell
        title="Enlace inválido"
        subtitle="El enlace de restablecimiento no es válido o ya expiró."
      >
        <ErrorState
          title="No se puede restablecer la contraseña"
          error="Solicita un nuevo enlace desde la pantalla de olvido de contraseña."
        />
        <Link
          to="/forgot-password"
          className="mt-6 inline-flex text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
        >
          Solicitar nuevo enlace
        </Link>
      </AuthShell>
    );
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLocalError(null);

    if (password !== confirmPassword) {
      setLocalError("Las contraseñas no coinciden.");
      return;
    }

    const policyError = validatePassword(password, defaultPolicy);
    if (policyError) {
      setLocalError(policyError);
      return;
    }

    try {
      await confirmReset.mutateAsync({
        resetToken: token,
        newPassword: password,
      });
      setCompleted(true);
      window.setTimeout(
        () => navigate("/login?reason=password-reset", { replace: true }),
        2500,
      );
    } catch {
      // Error shown via confirmReset.error
    }
  }

  if (completed) {
    return (
      <AuthShell
        title="Contraseña actualizada"
        subtitle="Tu contraseña fue restablecida correctamente."
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
      title="Nueva contraseña"
      subtitle="Define una contraseña segura para tu cuenta."
    >
      <form className="space-y-5" onSubmit={handleSubmit} noValidate>
        <TextField
          label="Nueva contraseña"
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

        <PasswordPolicyHints policy={defaultPolicy} />

        {(localError || confirmReset.isError) && (
          <div
            className="rounded-[10px] border border-flit-warning/30 bg-flit-warning/10 px-4 py-3 text-sm text-flit-danger"
            role="alert"
          >
            {localError ?? confirmReset.error?.message}
          </div>
        )}

        <GradientButton type="submit" disabled={confirmReset.isPending}>
          {confirmReset.isPending ? "Guardando…" : "Restablecer contraseña"}
        </GradientButton>
      </form>

      <p className="mt-6 text-center text-sm text-flit-draft">
        <Link
          to="/login"
          className="font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
        >
          Volver al inicio de sesión
        </Link>
      </p>
    </AuthShell>
  );
}

function PasswordPolicyHints({ policy }: { policy: PasswordPolicy }) {
  const rules = describePasswordPolicy(policy);

  return (
    <ul
      className="space-y-1 text-xs text-flit-muted"
      aria-label="Requisitos de contraseña"
    >
      {rules.map((rule) => (
        <li key={rule} className="flex items-center gap-2">
          <span
            className="h-1.5 w-1.5 rounded-full bg-flit-blueText"
            aria-hidden="true"
          />
          {rule}
        </li>
      ))}
    </ul>
  );
}
