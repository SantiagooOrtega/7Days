import { z } from 'zod';

export const registerSchema = z.object({
  email: z.email().max(254).transform((s) => s.toLowerCase()),
  username: z
    .string()
    .min(3)
    .max(20)
    .regex(/^[A-Za-z0-9_]+$/, 'Solo letras, números y guion bajo'),
  password: z.string().min(8).max(128),
});

export const loginSchema = z.object({
  email: z.email().max(254).transform((s) => s.toLowerCase()),
  password: z.string().min(1).max(128),
});

export const refreshSchema = z.object({
  refreshToken: z.string().min(20).max(200),
});
