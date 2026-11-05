import { Router } from 'express';
import { z } from 'zod';
import { prisma } from '../../db';
import { requireAuth } from '../../middleware/auth';
import { asyncHandler } from '../../middleware/errors';

export const progressRouter = Router();

const MAX_JSON_CHARS = 20_000;
const smallJson = (v: unknown) => JSON.stringify(v).length <= MAX_JSON_CHARS;

const progressSchema = z.object({
  currentDay: z.number().int().min(1).max(7),
  inventory: z.record(z.string(), z.unknown()).refine(smallJson, 'inventory demasiado grande'),
  stats: z
    .object({
      kills: z.number().int().min(0).default(0),
      shotsFired: z.number().int().min(0).default(0),
      shotsHit: z.number().int().min(0).default(0),
      deaths: z.number().int().min(0).default(0),
    })
    .catchall(z.number())
    .refine(smallJson, 'stats demasiado grande'),
});

const parse = (s: string) => {
  try {
    return JSON.parse(s);
  } catch {
    return {};
  }
};

progressRouter.get(
  '/',
  requireAuth,
  asyncHandler(async (req, res) => {
    const p = await prisma.gameProgress.findUnique({ where: { userId: req.userId } });
    res.json({
      progress: p
        ? { currentDay: p.currentDay, inventory: parse(p.inventory), stats: parse(p.stats), updatedAt: p.updatedAt }
        : { currentDay: 1, inventory: {}, stats: {}, updatedAt: null },
    });
  }),
);

progressRouter.put(
  '/',
  requireAuth,
  asyncHandler(async (req, res) => {
    const body = progressSchema.parse(req.body);
    const data = {
      currentDay: body.currentDay,
      inventory: JSON.stringify(body.inventory),
      stats: JSON.stringify(body.stats),
    };
    const p = await prisma.gameProgress.upsert({
      where: { userId: req.userId! },
      create: { userId: req.userId!, ...data },
      update: data,
    });
    res.json({ progress: { currentDay: p.currentDay, inventory: body.inventory, stats: body.stats, updatedAt: p.updatedAt } });
  }),
);
