import { useEffect, useState } from "react";
import {
  Link,
  Navigate,
  useLocation,
  useNavigate,
  useSearchParams,
} from "react-router-dom";
import { AppLayout } from "../../../app/AppLayout.js";
import { AuthShell } from "../../../shared/components/flit/AuthShell.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { SplitCurtain } from "../../../shared/components/flit/SplitCurtain.js";
import { TextField } from "../../../shared/components/flit/TextField.js";
import { useAuthMe, useLogin } from "../api/auth.api.js";
import { LoginWelcome } from "../components/LoginWelcome.js";

const loginReasonMessages: Record<string, string> = {
  "permissions-stale":
    "Sus permisos cambiaron. Inicie sesión nuevamente para continuar.",
  "password-reset": "Contraseña actualizada. Ya puede iniciar sesión.",
  "account-activated": "Cuenta activada correctamente. Inicie sesión.",
};

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { data: session, isLoading: sessionLoading } = useAuthMe();
  const login = useLogin();
  const [showWelcome, setShowWelcome] = useState(true);
  const [isExiting, setIsExiting] = useState(false);
  const [exitCurtainOpen, setExitCurtainOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [statusMessage] = useState(() => {
    const reason = new URLSearchParams(window.location.search).get("reason");
    return reason ? (loginReasonMessages[reason] ?? null) : null;
  });

  const redirectTo = (location.state as { from?: string } | null)?.from ?? "/";

  useEffect(() => {
    if (!searchParams.has("reason")) {
      return;
    }

    const nextParams = new URLSearchParams(searchParams);
    nextParams.delete("reason");
    const nextSearch = nextParams.toString();
    navigate(
      { pathname: "/login", search: nextSearch ? `?${nextSearch}` : "" },
      { replace: true, state: location.state },
    );
  }, [searchParams, navigate, location.state]);

  useEffect(() => {
    if (!isExiting) {
      return;
    }

    const frame = window.requestAnimationFrame(() => {
      setExitCurtainOpen(true);
    });

    return () => window.cancelAnimationFrame(frame);
  }, [isExiting]);

  if (sessionLoading) {
    return (
      <AuthShell title="Iniciar sesión" subtitle="Verificando sesión…">
        <p className="text-sm text-flit-draft">Cargando…</p>
      </AuthShell>
    );
  }

  if (session && !isExiting) {
    return <Navigate to={redirectTo} replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await login.mutateAsync({ email, password });
      setIsExiting(true);
    } catch {
      // Error shown via login.error
    }
  }

  const loginShell = (
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

        <div className="flex justify-end">
          <Link
            to="/forgot-password"
            className="text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          >
            ¿Olvidó su contraseña?
          </Link>
        </div>

        {statusMessage && (
          <div
            className="rounded-[10px] border border-flit-blue/20 bg-flit-blue/5 px-4 py-3 text-sm text-flit-blueDark"
            role="status"
          >
            {statusMessage}
          </div>
        )}

        {login.isError && (
          <div
            className="rounded-[10px] border border-flit-warning/30 bg-flit-warning/10 px-4 py-3 text-sm text-flit-danger"
            role="alert"
          >
            {login.error.message}
          </div>
        )}

        <GradientButton type="submit" disabled={login.isPending || isExiting}>
          {login.isPending ? "Ingresando…" : "Ingresar"}
        </GradientButton>
      </form>
    </AuthShell>
  );

  if (isExiting) {
    return (
      <>
        <AppLayout previewPath={redirectTo} />
        <SplitCurtain
          open={exitCurtainOpen}
          variant="content"
          onComplete={() => navigate(redirectTo, { replace: true })}
        >
          {loginShell}
        </SplitCurtain>
      </>
    );
  }

  return (
    <>
      {loginShell}
      {showWelcome && <LoginWelcome onComplete={() => setShowWelcome(false)} />}
    </>
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
