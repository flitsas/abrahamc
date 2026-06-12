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
  companiesAdmin: {
    list: (params: { page: number; pageSize: number; search?: string }) =>
      ["companies-admin", "list", params] as const,
    moduleConfig: (tenantId: string, moduleKey: string) =>
      ["companies-admin", "module-config", tenantId, moduleKey] as const,
    vehicleExceptions: (tenantId: string) =>
      ["companies-admin", "vehicle-exceptions", tenantId] as const,
    authorizedAgencies: (tenantId: string) =>
      ["companies-admin", "authorized-agencies", tenantId] as const,
    otCatalog: ["companies-admin", "ot-catalog"] as const,
  },
  parametrizadorAdmin: {
    families: ["parametrizador-admin", "families"] as const,
    catalogEdges: ["parametrizador-admin", "catalog-edges"] as const,
    types: (tenantId: string) =>
      ["parametrizador-admin", "types", tenantId] as const,
    matrix: (tenantId: string, typeCode: string) =>
      ["parametrizador-admin", "matrix", tenantId, typeCode] as const,
    edgeForm: (tenantId: string, typeCode: string, edgeCode: string) =>
      [
        "parametrizador-admin",
        "edge-form",
        tenantId,
        typeCode,
        edgeCode,
      ] as const,
    rules: (tenantId: string, procedureTypeId: string) =>
      ["parametrizador-admin", "rules", tenantId, procedureTypeId] as const,
  },
};
