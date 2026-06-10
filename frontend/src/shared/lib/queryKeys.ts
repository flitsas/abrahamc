export const queryKeys = {
  health: ["health"] as const,
  auth: {
    me: ["auth", "me"] as const,
    invitationPreview: (
      params: {
        invitationId: string;
        token: string;
        signature: string;
      } | null,
    ) => ["auth", "invitation-preview", params] as const,
  },
  procedures: {
    types: (tenantId: string) => ["procedures", "types", tenantId] as const,
  },
  identityAdmin: {
    users: (params: {
      tenantId: string;
      page: number;
      limit: number;
      search?: string;
    }) => ["identity-admin", "users", params] as const,
    roles: (tenantId: string) => ["identity-admin", "roles", tenantId] as const,
  },
};
