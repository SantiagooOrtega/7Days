import { Router } from 'express';
import { z } from 'zod';
import { prisma } from '../../db';
import { requireAuth } from '../../middleware/auth';
import { asyncHandler } from '../../middleware/errors';

export const rankingRouter = Router();

const submitSchema = z.object({
  score: z.number().int().min(0).max(10_000_000),
  daysSurvived: z.number().int().min(0).max(7),
  kills: z.number().int().min(0).max(100_000),
  victory: z.boolean(),
});

rankingRouter.post(
  '/',
  requireAuth,
  asyncHandler(async (req, res) => {
    const body = submitSchema.parse(req.body);
    const ranking = await prisma.ranking.create({ data: { userId: req.userId!, ...body } });
    res.status(201).json({ ranking });
  }),
);

rankingRouter.get(
  '/',
  requireAuth,
  asyncHandler(async (req, res) => {
    const limit = z.coerce.number().int().min(1).max(100).catch(20).parse(req.query.limit);
    const rows = await prisma.ranking.findMany({
      orderBy: [{ score: 'desc' }, { createdAt: 'asc' }],
      take: limit,
      include: { user: { select: { username: true } } },
    });
    res.json({
      rankings: rows.map((r, i) => ({
        position: i + 1,
        username: r.user.username,
        score: r.score,
        daysSurvived: r.daysSurvived,
        kills: r.kills,
        victory: r.victory,
        createdAt: r.createdAt,
      })),
    });
  }),
);
