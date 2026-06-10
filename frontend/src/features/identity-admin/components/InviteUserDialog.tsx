import { useEffect, useId, useState } from "react";
import type { AssignableRole } from "../api/admin.schemas.js";
import { useInviteUser } from "../api/admin.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { TextField } from "../../../shared/components/flit/TextField.js";

type InviteUserDialogProps = {
  open: boolean;
  tenantId: string;
  roles: AssignableRole[];
  isSuperAdmin: boolean;
  onClose: () => void;
  onSuccess: () => void;
};

export function InviteUserDialog({
  open,
  tenantId,
  roles,
  isSuperAdmin,
  onClose,
  onSuccess,
}: InviteUserDialogProps) {
  const titleId = useId();
  const invite = useInviteUser();
  const [email, setEmail] = useState("");
  const [roleId, setRoleId] = useState("");

  useEffect(() => {
    if (!open) {
      return;
    }
    setEmail("");
    setRoleId(roles[0]?.id ?? "");
  }, [open, roles]);

  if (!open) {
    return null;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!roleId) {
      return;
    }

    try {
      await invite.mutateAsync({
        email: email.trim(),
        invitedRoleId: roleId,
        tenantId: isSuperAdmin ? tenantId : null,
      });
      onSuccess();
      onClose();
    } catch {
      // Error shown via invite.isError
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
          Invitar usuario
        </h2>
        <p className="mt-2 text-sm text-flit-draft">
          Se enviará un correo con enlace de activación (válido 72 horas).
        </p>

        <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
          <TextField
            label="Correo electrónico"
            name="invite-email"
            type="email"
            required
            autoComplete="off"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="nuevo.usuario@empresa.com"
          />

          <div>
            <label
              htmlFor="invite-role"
              className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
            >
              Rol inicial
            </label>
            <select
              id="invite-role"
              name="invite-role"
              required
              value={roleId}
              onChange={(event) => setRoleId(event.target.value)}
              className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 text-sm text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
            >
              {roles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name} ({role.slug})
                </option>
              ))}
            </select>
          </div>

          {invite.isError && (
            <p className="text-sm text-flit-danger" role="alert">
              {invite.error.message}
            </p>
          )}

          {invite.isSuccess && (
            <p className="text-sm text-flit-green" role="status">
              Invitación enviada correctamente.
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
              disabled={invite.isPending || !roles.length}
              className="!h-12 !w-auto !px-8 !text-sm"
            >
              {invite.isPending ? "Enviando…" : "Enviar invitación"}
            </GradientButton>
          </div>
        </form>
      </div>
    </div>
  );
}
