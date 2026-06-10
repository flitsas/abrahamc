export const queryKeys = {
  health: ["health"] as const,
  auth: {
    me: ["auth", "me"] as const,
  },
  procedures: {
    types: (tenantId: string) => ["procedures", "types", tenantId] as const,
  },
};
