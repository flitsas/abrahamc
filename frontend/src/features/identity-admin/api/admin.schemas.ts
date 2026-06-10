import { z } from "zod";

export const collaboratorSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  email: z.string().email(),
  accountState: z.string(),
  fullName: z.string().nullish(),
  phone: z.string().nullish(),
  roleSlugs: z.array(z.string()),
  rowVersion: z.number(),
});

export const collaboratorsPageSchema = z.object({
  items: z.array(collaboratorSchema),
  total: z.number(),
  page: z.number(),
  limit: z.number(),
});

export const assignableRoleSchema = z.object({
  id: z.string().uuid(),
  slug: z.string(),
  name: z.string(),
  scope: z.string(),
});

export const assignableRolesSchema = z.array(assignableRoleSchema);

export const inviteUserRequestSchema = z.object({
  email: z.string().email(),
  invitedRoleId: z.string().uuid(),
  tenantId: z.string().uuid().nullable().optional(),
});

export const inviteUserResponseSchema = z.object({
  invitationId: z.string().uuid(),
  email: z.string().email(),
  expiresAt: z.string(),
  activationUrl: z.string().url(),
  emailQueued: z.boolean(),
});

export type Collaborator = z.infer<typeof collaboratorSchema>;
export type CollaboratorsPage = z.infer<typeof collaboratorsPageSchema>;
export type AssignableRole = z.infer<typeof assignableRoleSchema>;
export type InviteUserRequest = z.infer<typeof inviteUserRequestSchema>;
