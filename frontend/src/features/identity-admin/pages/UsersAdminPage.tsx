import { useState } from "react";
import { useAuthMe } from "../../auth/api/auth.api.js";
import { useAssignableRoles, useCollaborators } from "../api/admin.api.js";
import type { Collaborator } from "../api/admin.schemas.js";
import { EditUserRolesDialog } from "../components/EditUserRolesDialog.js";
import { InviteUserDialog } from "../components/InviteUserDialog.js";
import {
  accountStateLabel,
  accountStateVariant,
} from "../lib/accountStateLabel.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { StatusChip } from "../../../shared/components/flit/StatusChip.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

const PAGE_SIZE = 10;

function shortTenantId(tenantId: string): string {
  return `${tenantId.slice(0, 8)}…`;
}

export function UsersAdminPage() {
  const { data: session } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [inviteOpen, setInviteOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<Collaborator | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const listParams = tenantId
    ? { tenantId, page, limit: PAGE_SIZE, search: search || undefined }
    : null;

  const usersQuery = useCollaborators(listParams);
  const rolesQuery = useAssignableRoles(tenantId);

  const totalPages = usersQuery.data
    ? Math.max(1, Math.ceil(usersQuery.data.total / PAGE_SIZE))
    : 1;

  function handleSearchSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Usuarios y roles"
        subtitle="Administre colaboradores del tenant, asigne roles y envíe invitaciones por correo."
        actions={
          <GradientButton
            type="button"
            className="!h-12 !w-auto !px-8 !text-sm"
            onClick={() => setInviteOpen(true)}
            disabled={!rolesQuery.data?.length}
          >
            Invitar usuario
          </GradientButton>
        }
      />

      {statusMessage && (
        <div
          className="rounded-[10px] border border-flit-green/30 bg-flit-green/10 px-4 py-3 text-sm text-flit-green"
          role="status"
        >
          {statusMessage}
        </div>
      )}

      <form
        className="flex flex-col gap-3 sm:flex-row sm:items-end"
        onSubmit={handleSearchSubmit}
      >
        <div className="flex-1">
          <label
            htmlFor="user-search"
            className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
          >
            Buscar
          </label>
          <input
            id="user-search"
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Nombre o correo…"
            className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 text-sm text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          />
        </div>
        <GradientButton
          type="submit"
          className="!h-12 !w-auto !px-8 !text-sm sm:mb-0"
        >
          Buscar
        </GradientButton>
      </form>

      {usersQuery.isLoading && <LoadingSkeleton rows={6} />}
      {usersQuery.isError && (
        <ErrorState
          error={usersQuery.error}
          onRetry={() => usersQuery.refetch()}
        />
      )}

      {!usersQuery.isLoading &&
        !usersQuery.isError &&
        usersQuery.data &&
        usersQuery.data.items.length === 0 && (
          <EmptyState
            title="Sin usuarios"
            description="No hay colaboradores que coincidan con la búsqueda en este tenant."
          />
        )}

      {!usersQuery.isLoading &&
        !usersQuery.isError &&
        usersQuery.data &&
        usersQuery.data.items.length > 0 && (
          <div className="overflow-hidden rounded-flit-card border border-flit-draft/20 bg-flit-card shadow-flit-card">
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-flit-bg">
                  <tr>
                    <th
                      scope="col"
                      className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                    >
                      Nombre
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                    >
                      Correo
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                    >
                      Estado
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                    >
                      Roles
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-left font-semibold text-flit-blueDark"
                    >
                      Tenant
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-right font-semibold text-flit-blueDark"
                    >
                      Acciones
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-flit-draft/10">
                  {usersQuery.data.items.map((user) => (
                    <tr key={user.id}>
                      <td className="px-4 py-3 text-flit-blueDark">
                        {user.fullName ?? "—"}
                      </td>
                      <td className="px-4 py-3 text-flit-blueDark">
                        {user.email}
                      </td>
                      <td className="px-4 py-3">
                        <StatusChip
                          label={accountStateLabel(user.accountState)}
                          variant={accountStateVariant(user.accountState)}
                        />
                      </td>
                      <td className="px-4 py-3 text-flit-draft">
                        {user.roleSlugs.length
                          ? user.roleSlugs.join(", ")
                          : "—"}
                      </td>
                      <td
                        className="px-4 py-3 font-mono text-xs text-flit-muted"
                        title={user.tenantId}
                      >
                        {shortTenantId(user.tenantId)}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          onClick={() => setEditingUser(user)}
                          className="text-sm font-semibold text-flit-blueText hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
                        >
                          Editar roles
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between border-t border-flit-draft/10 px-4 py-3">
              <p className="text-xs text-flit-muted">
                {usersQuery.data.total} usuario
                {usersQuery.data.total === 1 ? "" : "s"} · Página {page} de{" "}
                {totalPages}
              </p>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  className="rounded-flit-pill border border-flit-draft/30 px-4 py-2 text-xs font-semibold text-flit-blueDark disabled:opacity-40"
                >
                  Anterior
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() =>
                    setPage((current) => Math.min(totalPages, current + 1))
                  }
                  className="rounded-flit-pill border border-flit-draft/30 px-4 py-2 text-xs font-semibold text-flit-blueDark disabled:opacity-40"
                >
                  Siguiente
                </button>
              </div>
            </div>
          </div>
        )}

      {tenantId && rolesQuery.data && (
        <InviteUserDialog
          open={inviteOpen}
          tenantId={tenantId}
          roles={rolesQuery.data}
          isSuperAdmin={session?.user.isSuperAdmin ?? false}
          onClose={() => setInviteOpen(false)}
          onSuccess={() =>
            setStatusMessage(
              "Invitación enviada. El usuario recibirá un correo de activación.",
            )
          }
        />
      )}

      <EditUserRolesDialog
        open={editingUser !== null}
        user={editingUser}
        roles={rolesQuery.data ?? []}
        onClose={() => setEditingUser(null)}
        onSuccess={() => setStatusMessage("Roles actualizados correctamente.")}
      />
    </div>
  );
}
