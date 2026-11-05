import { randomUUID } from 'node:crypto';
import argon2 from 'argon2';
import { Prisma } from '@prisma/client';
import { env } from '../../config/env';
import { prisma } from '../../db';
import { AppError } from '../../middleware/errors';
import { generateRefreshToken, hashToken, signAccessToken } from './tokens';

// Hash falso para gastar el mismo tiempo cuando el email no existe (evita enumerar usuarios por tiempo).
const dummyHash = argon2.hash('dummy-password-for-timing');

export interface PublicUser {
  id: string;
  email: string;
  username: string;
}

export interface AuthResult {
  user: PublicUser;
  accessToken: string;
  refreshToken: string;
}

const toPublic = (u: PublicUser): PublicUser => ({ id: u.id, email: u.email, username: u.username });

async function issueTokens(userId: string, familyId: string) {
  const refreshToken = generateRefreshToken();
  await prisma.refreshToken.create({
    data: {
      userId,
      familyId,
      tokenHash: hashToken(refreshToken),
      expiresAt: new Date(Date.now() + env.REFRESH_TOKEN_TTL_DAYS * 86_400_000),
    },
  });
  return { accessToken: signAccessToken(userId), refreshToken };
}

export async function register(input: {
  email: string;
  username: string;
  password: string;
}): Promise<AuthResult> {
  const passwordHash = await argon2.hash(input.password);
  try {
    const user = await prisma.user.create({
      data: { email: input.email, username: input.username, passwordHash },
    });
    return { user: toPublic(user), ...(await issueTokens(user.id, randomUUID())) };
  } catch (e) {
    if (e instanceof Prisma.PrismaClientKnownRequestError && e.code === 'P2002') {
      throw new AppError(409, 'conflict', 'El email o el nombre de usuario ya están en uso');
    }
    throw e;
  }
}

export async function login(input: { email: string; password: string }): Promise<AuthResult> {
  const user = await prisma.user.findUnique({ where: { email: input.email } });
  const ok = await argon2.verify(user?.passwordHash ?? (await dummyHash), input.password);
  if (!user || !ok) throw new AppError(401, 'invalid_credentials', 'Email o contraseña incorrectos');
  return { user: toPublic(user), ...(await issueTokens(user.id, randomUUID())) };
}

export async function refresh(refreshToken: string) {
  const invalid = new AppError(401, 'invalid_refresh', 'Refresh token inválido o expirado');
  const stored = await prisma.refreshToken.findUnique({ where: { tokenHash: hashToken(refreshToken) } });
  if (!stored) throw invalid;

  // Reutilización de un token ya rotado: posible robo -> se revoca toda la familia (sesión).
  if (stored.revokedAt) {
    await prisma.refreshToken.updateMany({
      where: { familyId: stored.familyId, revokedAt: null },
      data: { revokedAt: new Date() },
    });
    throw invalid;
  }
  if (stored.expiresAt < new Date()) throw invalid;

  const next = await issueTokens(stored.userId, stored.familyId);
  await prisma.refreshToken.update({
    where: { id: stored.id },
    data: { revokedAt: new Date(), replacedBy: hashToken(next.refreshToken) },
  });
  return next;
}

export async function logout(refreshToken: string) {
  const stored = await prisma.refreshToken.findUnique({ where: { tokenHash: hashToken(refreshToken) } });
  if (!stored) return;
  await prisma.refreshToken.updateMany({
    where: { familyId: stored.familyId, revokedAt: null },
    data: { revokedAt: new Date() },
  });
}
