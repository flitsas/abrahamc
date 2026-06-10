import { useEffect, useId, useState } from "react";
import type { AssignableRole, Collaborator } from "../api/admin.schemas.js";
import { useSyncUserRoles } from "../api/admin.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";

type EditUserRolesDialogProps = {
  open: boolean;
  user: Collaborator | null;
  roles: AssignableRole[];
  onClose: () => void;
  onSuccess: () => void;
};

export function EditUserRolesDialog({
  open,
  user,
  roles,
  onClose,
  onSuccess,
}: EditUserRolesDialogProps) {
  const titleId = useId();
  const syncRoles = useSyncUserRoles();
  const [selectedSlugs, setSelectedSlugs] = useState<string[]>([]);

  useEffect(() => {
    if (open && user) {
      setSelectedSlugs([...user.roleSlugs]);
    }
  }, [open, user]);

  if (!open || !user) {
    return null;
  }

  const selectedUser = user;

  function toggleSlug(slug: string) {
    setSelectedSlugs((current) =>
      current.includes(slug)
        ? current.filter((item) => item !== slug)
        : [...current, slug],
    );
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      await syncRoles.mutateAsync({
        tenantId: selectedUser.tenantId,
        userId: selectedUser.id,
        currentRoleSlugs: selectedUser.roleSlugs,
        nextRoleSlugs: selectedSlugs,
        roles,
      });
      onSuccess();
      onClose();
    } catch {
      // Error shown via syncRoles.isError
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
          Editar roles
        </h2>
        <p className="mt-2 text-sm text-flit-draft">
          {selectedUser.fullName ?? selectedUser.email}
        </p>

        <form className="mt-6 space-y-3" onSubmit={handleSubmit}>
          <fieldset>
            <legend className="sr-only">Roles asignables</legend>
            {roles.map((role) => (
              <label
                key={role.id}
                className="flex cursor-pointer items-center gap-3 rounded-[10px] border border-flit-draft/20 px-4 py-3 hover:bg-flit-bg"
              >
                <input
                  type="checkbox"
                  checked={selectedSlugs.includes(role.slug)}
                  onChange={() => toggleSlug(role.slug)}
                  className="h-4 w-4 rounded border-flit-draft/40 text-flit-blue focus:ring-flit-blue"
                />
                <span>
                  <span className="block text-sm font-semibold text-flit-blueDark">
                    {role.name}
                  </span>
                  <span className="text-xs text-flit-muted">{role.slug}</span>
                </span>
              </label>
            ))}
          </fieldset>

          {syncRoles.isError && (
            <p className="text-sm text-flit-danger" role="alert">
              {syncRoles.error.message}
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
              disabled={syncRoles.isPending}
              className="!h-12 !w-auto !px-8 !text-sm"
            >
              {syncRoles.isPending ? "Guardando…" : "Guardar roles"}
            </GradientButton>
          </div>
        </form>
      </div>
    </div>
  );
}
