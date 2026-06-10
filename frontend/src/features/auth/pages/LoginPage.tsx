import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { AuthShell } from "../../../shared/components/flit/AuthShell.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";
import { useAuthMe, useLogin } from "../api/auth.api.js";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { data: session, isLoading: sessionLoading } = useAuthMe();
  const login = useLogin();
  const [email, setEmail] = useState("superadmin@flit.com.co");
  const [password, setPassword] = useState("FlitDev2026!");

  const redirectTo = (location.state as { from?: string } | null)?.from ?? "/";

  if (sessionLoading) {
    return (
      <AuthShell title="Iniciar sesión" subtitle="Verificando sesión…">
        <p className="text-sm text-flit-draft">Cargando…</p>
      </AuthShell>
    );
  }

  if (session) {
    return <Navigate to={redirectTo} replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await login.mutateAsync({ email, password });
      navigate(redirectTo, { replace: true });
    } catch {
      // Error shown via login.error
    }
  }

  return (
    <AuthShell
      title="Iniciar sesión"
      subtitle="Ingresa con tu correo corporativo para acceder al panel administrativo."
    >
      <form className="space-y-5" onSubmit={handleSubmit} noValidate>
        <TextField
          label="Correo electrónico"
          name="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="usuario@empresa.com"
          icon={<MailIcon />}
        />

        <TextField
          label="Contraseña"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          placeholder="••••••••"
          icon={<LockIcon />}
        />

        {login.isError && (
          <div
            className="rounded-[10px] border border-flit-warning/30 bg-flit-warning/10 px-4 py-3 text-sm text-flit-danger"
            role="alert"
          >
            {login.error.message}
          </div>
        )}

        <GradientButton type="submit" disabled={login.isPending}>
          {login.isPending ? "Ingresando…" : "Ingresar"}
        </GradientButton>
      </form>

      <p className="mt-6 text-center text-xs text-flit-muted">
        Entorno DEV:{" "}
        <span className="font-medium text-flit-draft">
          superadmin@flit.com.co
        </span>
      </p>
    </AuthShell>
  );
}

function MailIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M4 6h16v12H4z" />
      <path d="M4 7l8 6 8-6" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V8a4 4 0 018 0v3" />
    </svg>
  );
}
