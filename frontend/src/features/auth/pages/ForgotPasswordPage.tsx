import { useState } from "react";
import { Link } from "react-router-dom";
import { AuthShell } from "../../../shared/components/flit/AuthShell.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";
import { useRequestPasswordReset } from "../api/password-reset.api.js";

export function ForgotPasswordPage() {
  const resetRequest = useRequestPasswordReset();
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await resetRequest.mutateAsync({ email });
      setSubmitted(true);
    } catch {
      // Mostramos el mismo mensaje genérico para no revelar si el correo existe
      setSubmitted(true);
    }
  }

  if (submitted) {
    return (
      <AuthShell
        title="Revisa tu correo"
        subtitle="Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña."
      >
        <div
          className="rounded-[10px] border border-flit-success/30 bg-flit-success/10 px-4 py-4 text-sm text-flit-blueDark"
          role="status"
        >
          Hemos procesado tu solicitud. El enlace de restablecimiento expira en
          30 minutos.
        </div>

        <Link
          to="/login"
          className="mt-6 inline-flex text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
        >
          Volver al inicio de sesión
        </Link>
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title="Olvidé mi contraseña"
      subtitle="Ingresa tu correo corporativo y te enviaremos un enlace para restablecerla."
    >
      <form className="space-y-5" onSubmit={handleSubmit} noValidate>
        <TextField
          label="Correo electrónico"
          name="email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="usuario@empresa.com"
        />

        <GradientButton type="submit" disabled={resetRequest.isPending}>
          {resetRequest.isPending ? "Enviando…" : "Enviar enlace"}
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
