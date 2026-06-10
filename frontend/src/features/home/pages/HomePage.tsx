import { Link } from "react-router-dom";
import { useAuthMe } from "../../auth/api/auth.api.js";
import { useHealth } from "../api/health.api.js";
import { FlitCard } from "../../../shared/components/flit/FlitCard.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { KpiCard } from "../../../shared/components/flit/KpiCard.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { StatusChip } from "../../../shared/components/flit/StatusChip.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

export function HomePage() {
  const health = useHealth();
  const { data: session } = useAuthMe();

  const apiStatus = health.isLoading
    ? null
    : health.isError
      ? { label: "Sin conexión", variant: "danger" as const }
      : health.data
        ? { label: health.data.status, variant: "success" as const }
        : { label: "Sin datos", variant: "draft" as const };

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Inicio"
        subtitle="Panel operativo con visión en tiempo real de tu sesión, la API y los módulos activos del tenant."
        actions={
          <Link to="/tramites" className="inline-block w-full sm:w-auto">
            <GradientButton className="!h-12 !w-auto !px-8 !text-sm">
              Ir a trámites
            </GradientButton>
          </Link>
        }
      />

      <section
        aria-label="Indicadores principales"
        className="grid gap-4 md:grid-cols-2 xl:grid-cols-4"
      >
        <KpiCard
          futuristic
          label="Sesión activa"
          value={session ? session.user.email.split("@")[0] : "—"}
          hint={session?.user.email}
          status={
            session
              ? { label: "Conectado", variant: "success" }
              : { label: "Cargando", variant: "draft" }
          }
          icon={<UserIcon />}
        />

        <KpiCard
          futuristic
          label="Permisos asignados"
          value={session?.permissionSlugs.length ?? "—"}
          hint="Capacidades habilitadas en tu rol"
          status={{ label: "Acceso", variant: "active" }}
          icon={<ShieldIcon />}
        />

        <KpiCard
          futuristic
          label="Estado API"
          value={health.data?.service ?? (health.isLoading ? "…" : "—")}
          hint={
            health.data
              ? `v${health.data.version}`
              : health.isError
                ? "Revisa el backend"
                : "Consultando salud del servicio"
          }
          status={apiStatus ?? { label: "Consultando", variant: "draft" }}
          icon={<PulseIcon />}
        />

        <KpiCard
          futuristic
          label="Módulos activos"
          value="02"
          hint="Inicio y trámites disponibles"
          status={{ label: "Operativo", variant: "success" }}
          icon={<GridIcon />}
        />
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.4fr_1fr]">
        <FlitCard accent className="flit-panel-futuristic">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h2 className="text-lg font-bold text-flit-blueDark">
                Actividad del sistema
              </h2>
              <p className="mt-1 text-sm text-flit-draft">
                Línea de tiempo operativa del entorno DEV.
              </p>
            </div>
            <StatusChip label="En vivo" variant="active" />
          </div>

          <ol className="mt-6 space-y-0">
            <TimelineItem
              title="Sesión validada"
              detail={
                session
                  ? `Usuario ${session.user.email} autenticado correctamente.`
                  : "Verificando credenciales de sesión…"
              }
              time="Ahora"
              state="success"
            />
            <TimelineItem
              title="API core"
              detail={
                health.data
                  ? `Servicio ${health.data.service} respondiendo en estado ${health.data.status}.`
                  : health.isError
                    ? "No fue posible contactar el endpoint de salud."
                    : "Esperando respuesta del backend…"
              }
              time="Hoy"
              state={health.isError ? "danger" : health.data ? "success" : "active"}
            />
            <TimelineItem
              title="Tenant vinculado"
              detail={
                session
                  ? `Contexto multi-tenant activo (${session.user.tenantId.slice(0, 8)}…).`
                  : "Resolviendo tenant de la sesión…"
              }
              time="Hoy"
              state={session ? "active" : "draft"}
              isLast
            />
          </ol>
        </FlitCard>

        <div className="space-y-6">
          <FlitCard>
            <h2 className="text-lg font-bold text-flit-blueDark">
              Resumen de sesión
            </h2>
            {!session ? (
              <div className="mt-4">
                <LoadingSkeleton rows={3} />
              </div>
            ) : (
              <dl className="mt-4 space-y-4 text-sm">
                <SummaryRow label="Correo" value={session.user.email} />
                <SummaryRow
                  label="Rol"
                  value={
                    session.user.isSuperAdmin ? "Superadministrador" : "Operador"
                  }
                />
                <SummaryRow
                  label="Tenant"
                  value={session.user.tenantId}
                  mono
                />
                <SummaryRow
                  label="Permisos"
                  value={`${session.permissionSlugs.length} asignados`}
                />
              </dl>
            )}
          </FlitCard>

          <FlitCard accent>
            <h2 className="text-lg font-bold text-flit-blueDark">
              Acciones rápidas
            </h2>
            <p className="mt-1 text-sm text-flit-draft">
              Atajos a los flujos operativos principales.
            </p>
            <div className="mt-5 space-y-3">
              <QuickAction
                to="/tramites"
                title="Catálogo de trámites"
                description="Consulta tipos activos del tenant."
              />
              <QuickAction
                to="/tramites"
                title="Nuevo traspaso"
                description="Próximamente — wizard del prototipo FLIT."
                disabled
              />
            </div>
          </FlitCard>

          {health.isError && (
            <ErrorState
              error={health.error}
              onRetry={() => health.refetch()}
              title="API no disponible"
            />
          )}
        </div>
      </section>
    </div>
  );
}

type TimelineState = "success" | "active" | "danger" | "draft";

type TimelineItemProps = {
  title: string;
  detail: string;
  time: string;
  state: TimelineState;
  isLast?: boolean;
};

function TimelineItem({
  title,
  detail,
  time,
  state,
  isLast = false,
}: TimelineItemProps) {
  const dotClass: Record<TimelineState, string> = {
    success: "bg-flit-green shadow-[0_0_0_4px_rgba(112,207,58,0.18)]",
    active: "bg-flit-blue shadow-[0_0_0_4px_rgba(79,116,201,0.18)]",
    danger: "bg-flit-danger shadow-[0_0_0_4px_rgba(228,61,48,0.18)]",
    draft: "bg-flit-draft shadow-[0_0_0_4px_rgba(89,103,125,0.18)]",
  };

  return (
    <li className="relative flex gap-4 pb-6">
      {!isLast && (
        <span
          className="absolute left-[7px] top-4 h-[calc(100%-8px)] w-px bg-gradient-to-b from-flit-cyan/70 via-flit-blue/50 to-transparent"
          aria-hidden="true"
        />
      )}
      <span
        className={`relative z-10 mt-1 h-3.5 w-3.5 shrink-0 rounded-full ${dotClass[state]}`}
        aria-hidden="true"
      />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="font-semibold text-flit-blueDark">{title}</h3>
          <time className="text-xs font-medium uppercase tracking-wide text-flit-muted">
            {time}
          </time>
        </div>
        <p className="mt-1 text-sm leading-relaxed text-flit-draft">{detail}</p>
      </div>
    </li>
  );
}

function SummaryRow({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-wide text-flit-muted">
        {label}
      </dt>
      <dd
        className={`mt-1 font-medium text-flit-blueDark ${mono ? "font-mono text-xs break-all" : ""}`}
      >
        {value}
      </dd>
    </div>
  );
}

function QuickAction({
  to,
  title,
  description,
  disabled = false,
}: {
  to: string;
  title: string;
  description: string;
  disabled?: boolean;
}) {
  const className = `block rounded-flit-card border border-flit-border px-4 py-3 transition ${
    disabled
      ? "cursor-not-allowed opacity-60"
      : "hover:border-flit-blue/35 hover:bg-flit-bg hover:shadow-flit-card"
  }`;

  if (disabled) {
    return (
      <div className={className} aria-disabled="true">
        <p className="font-semibold text-flit-blueDark">{title}</p>
        <p className="mt-1 text-sm text-flit-draft">{description}</p>
      </div>
    );
  }

  return (
    <Link to={to} className={className}>
      <p className="font-semibold text-flit-blueDark">{title}</p>
      <p className="mt-1 text-sm text-flit-draft">{description}</p>
    </Link>
  );
}

function UserIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 20c1.5-4 6-6 8-6s6.5 2 8 6" />
    </svg>
  );
}

function ShieldIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
      <path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z" />
    </svg>
  );
}

function PulseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
      <path d="M4 12h3l2-7 4 14 3-7h4" />
    </svg>
  );
}

function GridIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
      <rect x="4" y="4" width="7" height="7" rx="1.5" />
      <rect x="13" y="4" width="7" height="7" rx="1.5" />
      <rect x="4" y="13" width="7" height="7" rx="1.5" />
      <rect x="13" y="13" width="7" height="7" rx="1.5" />
    </svg>
  );
}
