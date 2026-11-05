import { Router } from 'express';
import { prisma } from '../../db';
import { requireAuth } from '../../middleware/auth';
import { AppError, asyncHandler } from '../../middleware/errors';

export const usersRouter = Router();

usersRouter.get(
  '/me',
  requireAuth,
  asyncHandler(async (req, res) => {
    const user = await prisma.user.findUnique({
      where: { id: req.userId },
      select: { id: true, email: true, username: true, createdAt: true },
    });
    if (!user) throw new AppError(404, 'not_found', 'Usuario no encontrado');
    res.json({ user });
  }),
);
