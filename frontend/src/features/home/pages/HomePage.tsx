import { useAuthMe } from "../../auth/api/auth.api.js";
import { useHealth } from "../api/health.api.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

export function HomePage() {
  const health = useHealth();
  const { data: session } = useAuthMe();

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-2xl font-semibold text-slate-900">Inicio</h1>
        <p className="mt-2 text-sm text-slate-600">
          Bienvenido al panel FLIT Trámites 2.0.
        </p>
      </header>

      <section className="grid gap-4 md:grid-cols-2">
        <article className="rounded-xl border border-slate-200 bg-white p-6">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
            Sesión activa
          </h2>
          {session ? (
            <dl className="mt-4 space-y-2 text-sm">
              <div>
                <dt className="text-slate-500">Usuario</dt>
                <dd className="font-medium text-slate-900">
                  {session.user.email}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Tenant</dt>
                <dd className="font-mono text-xs text-slate-700">
                  {session.user.tenantId}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Permisos</dt>
                <dd className="text-slate-700">
                  {session.permissionSlugs.length} asignados
                </dd>
              </div>
            </dl>
          ) : (
            <LoadingSkeleton rows={3} />
          )}
        </article>

        <article className="rounded-xl border border-slate-200 bg-white p-6">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
            Estado de la API
          </h2>
          {health.isLoading && (
            <div className="mt-4">
              <LoadingSkeleton rows={2} />
            </div>
          )}
          {health.isError && (
            <div className="mt-4">
              <ErrorState
                error={health.error}
                onRetry={() => health.refetch()}
                title="API no disponible"
              />
            </div>
          )}
          {health.data && (
            <dl className="mt-4 space-y-2 text-sm">
              <div>
                <dt className="text-slate-500">Estado</dt>
                <dd className="font-medium text-emerald-700">
                  {health.data.status}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Servicio</dt>
                <dd className="text-slate-900">{health.data.service}</dd>
              </div>
              <div>
                <dt className="text-slate-500">Versión</dt>
                <dd className="font-mono text-xs text-slate-700">
                  {health.data.version}
                </dd>
              </div>
            </dl>
          )}
        </article>
      </section>
    </div>
  );
}
