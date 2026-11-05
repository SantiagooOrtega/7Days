import cors from 'cors';
import express from 'express';
import helmet from 'helmet';
import { env } from './config/env';
import { errorHandler, notFound } from './middleware/errors';
import { authRouter } from './modules/auth/auth.routes';
import { progressRouter } from './modules/progress/progress.routes';
import { rankingRouter } from './modules/ranking/ranking.routes';
import { usersRouter } from './modules/users/users.routes';

export function createApp(opts: { loginRateLimitMax?: number } = {}) {
  const app = express();
  app.disable('x-powered-by');
  app.use(helmet());
  // Unity no envía cabecera Origin, así que CORS solo afecta a clientes web.
  app.use(cors({ origin: env.corsOrigins }));
  app.use(express.json({ limit: '100kb' }));

  app.get('/health', (_req, res) => res.json({ status: 'ok' }));
  app.use('/api/auth', authRouter({ loginRateLimitMax: opts.loginRateLimitMax ?? 10 }));
  app.use('/api', usersRouter);
  app.use('/api/progress', progressRouter);
  app.use('/api/rankings', rankingRouter);

  app.use(notFound);
  app.use(errorHandler);
  return app;
}
