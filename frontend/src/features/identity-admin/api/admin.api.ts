import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  assignableRolesSchema,
  collaboratorsPageSchema,
  inviteUserRequestSchema,
  inviteUserResponseSchema,
  type InviteUserRequest,
} from "./admin.schemas.js";

type ListUsersParams = {
  tenantId: string;
  page: number;
  limit: number;
  search?: string;
};

export async function fetchCollaborators(params: ListUsersParams) {
  const { data } = await apiClient.get("/identity/admin/users", {
    params: {
      tenantId: params.tenantId,
      page: params.page,
      limit: params.limit,
      search: params.search || undefined,
    },
  });
  return collaboratorsPageSchema.parse(data);
}

export async function fetchAssignableRoles(tenantId: string) {
  const { data } = await apiClient.get("/identity/admin/roles", {
    params: { tenantId },
  });
  return assignableRolesSchema.parse(data);
}

export async function assignUserRole(
  tenantId: string,
  userId: string,
  roleId: string,
) {
  await apiClient.put(
    `/identity/admin/users/${userId}/roles/${roleId}`,
    {},
    { params: { tenantId } },
  );
}

export async function removeUserRole(
  tenantId: string,
  userId: string,
  roleId: string,
) {
  await apiClient.delete(`/identity/admin/users/${userId}/roles/${roleId}`, {
    params: { tenantId },
  });
}

export async function inviteUser(payload: InviteUserRequest) {
  const body = inviteUserRequestSchema.parse(payload);
  const { data } = await apiClient.post("/users/invite", body);
  return inviteUserResponseSchema.parse(data);
}

export function useCollaborators(
  params: ListUsersParams | null,
  enabled = true,
) {
  return useQuery({
    queryKey: params
      ? queryKeys.identityAdmin.users(params)
      : ["identity-admin", "users", "disabled"],
    queryFn: () => fetchCollaborators(params!),
    enabled: enabled && params !== null,
    staleTime: 15_000,
  });
}

export function useAssignableRoles(
  tenantId: string | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.identityAdmin.roles(tenantId)
      : ["identity-admin", "roles", "disabled"],
    queryFn: () => fetchAssignableRoles(tenantId!),
    enabled: enabled && Boolean(tenantId),
    staleTime: 60_000,
  });
}

export function useInviteUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: inviteUser,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["identity-admin", "users"] });
    },
  });
}

export function useSyncUserRoles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: {
      tenantId: string;
      userId: string;
      currentRoleSlugs: string[];
      nextRoleSlugs: string[];
      roles: { id: string; slug: string }[];
    }) => {
      const slugToId = new Map(input.roles.map((role) => [role.slug, role.id]));
      const current = new Set(input.currentRoleSlugs);
      const next = new Set(input.nextRoleSlugs);

      for (const slug of next) {
        if (!current.has(slug)) {
          const roleId = slugToId.get(slug);
          if (roleId) {
            await assignUserRole(input.tenantId, input.userId, roleId);
          }
        }
      }

      for (const slug of current) {
        if (!next.has(slug)) {
          const roleId = slugToId.get(slug);
          if (roleId) {
            await removeUserRole(input.tenantId, input.userId, roleId);
          }
        }
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["identity-admin", "users"] });
    },
  });
}
