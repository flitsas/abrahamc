import { z } from "zod";

export const loginRequestSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
});

export const authUserSchema = z.object({
  userId: z.string().uuid(),
  email: z.string().email(),
  tenantId: z.string().uuid(),
  accountState: z.string(),
  isSuperAdmin: z.boolean(),
  permissionSlugs: z.array(z.string()),
  expiresInSeconds: z.number(),
});

export const loginResponseSchema = z.object({
  user: authUserSchema,
  permissionSlugs: z.array(z.string()),
  expiresIn: z.number(),
});

export const authMeResponseSchema = z.object({
  user: z.object({
    id: z.string().uuid(),
    email: z.string().email(),
    tenantId: z.string().uuid(),
    isSuperAdmin: z.boolean(),
  }),
  permissionSlugs: z.array(z.string()),
});

export type LoginRequest = z.infer<typeof loginRequestSchema>;
export type AuthUser = z.infer<typeof authUserSchema>;
export type LoginResponse = z.infer<typeof loginResponseSchema>;
export type AuthMeResponse = z.infer<typeof authMeResponseSchema>;
