import type { NextFunction, Request, Response } from 'express';
import { AppError } from './errors';
import { verifyAccessToken } from '../modules/auth/tokens';

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Express {
    interface Request {
      userId?: string;
    }
  }
}

export function requireAuth(req: Request, _res: Response, next: NextFunction) {
  const header = req.headers.authorization;
  if (!header?.startsWith('Bearer ')) {
    return next(new AppError(401, 'unauthorized', 'Falta el token de acceso'));
  }
  try {
    req.userId = verifyAccessToken(header.slice(7));
    next();
  } catch {
    next(new AppError(401, 'unauthorized', 'Token inválido o expirado'));
  }
}
