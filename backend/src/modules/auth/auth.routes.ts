import { Router } from 'express';
import rateLimit from 'express-rate-limit';
import { asyncHandler } from '../../middleware/errors';
import { loginSchema, refreshSchema, registerSchema } from './auth.schemas';
import * as auth from './auth.service';

export function authRouter(opts: { loginRateLimitMax: number }) {
  const router = Router();

  const loginLimiter = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: opts.loginRateLimitMax,
    standardHeaders: true,
    legacyHeaders: false,
    message: { error: { code: 'too_many_requests', message: 'Demasiados intentos, espera unos minutos' } },
  });

  router.post(
    '/register',
    asyncHandler(async (req, res) => {
      res.status(201).json(await auth.register(registerSchema.parse(req.body)));
    }),
  );

  router.post(
    '/login',
    loginLimiter,
    asyncHandler(async (req, res) => {
      res.json(await auth.login(loginSchema.parse(req.body)));
    }),
  );

  router.post(
    '/refresh',
    asyncHandler(async (req, res) => {
      res.json(await auth.refresh(refreshSchema.parse(req.body).refreshToken));
    }),
  );

  router.post(
    '/logout',
    asyncHandler(async (req, res) => {
      await auth.logout(refreshSchema.parse(req.body).refreshToken);
      res.status(204).end();
    }),
  );

  return router;
}
