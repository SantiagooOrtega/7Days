import { z } from 'zod';

/**
 * Esquema de un mapa (el mismo JSON que lee Unity con JsonUtility: game/Assets/_Project/Resources/Maps/*.json).
 * Debe coincidir con MapKit.Validate (C#): tipos de pieza conocidos, piezas dentro del mapa, al menos una casa con 3 entradas,
 * 8 spawns o más en el borde y en 3 lados o más, puntos de botín y ambientación con niebla en rango.
 */
export const MAP_PROP_KINDS = [
  'ground', 'road', 'wall', 'crumbles', 'barrier', 'crate', 'container', 'pillar', 'beam', 'roof', 'brickwall',
  'pipe', 'barrel', 'fire_barrel', 'rubble_field', 'lamp',
  'soil', 'boundary', 'fence', 'trail', 'boulder', 'trunk', 'caravan', 'lantern',
] as const;
export const MIN_SPAWNS = 8;

const vec3 = z.array(z.number()).length(3);
const rgb = z.array(z.number().min(0).max(4)).length(3);

export const mapPropSchema = z.object({
  kind: z.enum(MAP_PROP_KINDS),
  name: z.string().min(1).max(80),
  pos: vec3,
  rot: vec3,
  size: vec3,
  color: rgb,
  surface: z.string().max(40),
  cover: z.boolean(),
  count: z.number().int().min(0).max(1000),
  rank: z.number().int().min(-1).max(16),
});

export const mapSpawnSchema = z.object({
  id: z.string().min(1).max(20),
  x: z.number(),
  z: z.number(),
  side: z.enum(['n', 's', 'e', 'o']),
});

const houseSchema = z
  .object({
    x: z.number(),
    z: z.number(),
    yaw: z.number(),
    width: z.number().positive(),
    depth: z.number().positive(),
    openings: z.array(z.object({}).passthrough()).min(3, 'cada casa necesita 3 entradas o más'),
  })
  .passthrough();

export const mapAmbienceSchema = z
  .object({
    hour: z.number().min(0).max(24),
    fogColor: rgb,
    fogDensity: z.number().min(0).max(0.2),
    sunPitch: z.number().min(-90).max(90),
    sunIntensity: z.number().min(0).max(5),
    brightness: z.number().min(0.2).max(3),
    fireflies: z.number().min(0).max(3).optional(),
    soundscape: z.enum(['none', 'wind', 'forest']).optional(),
  })
  .passthrough();

export const mapSchema = z
  .object({
    version: z.literal(1),
    id: z.string().regex(/^[a-z0-9_]{3,40}$/),
    name: z.string().min(1).max(60),
    baseDifficulty: z.number().int().min(1).max(5),
    halfSize: z.number().min(15).max(120),
    seed: z.number().int(),
    playerStart: z.array(z.number()).length(4),
    props: z.array(mapPropSchema).max(2000),
    houses: z.array(houseSchema).min(1, 'necesita al menos una casa o refugio'),
    spawns: z.array(mapSpawnSchema).min(MIN_SPAWNS),
    vegetation: z.object({ items: z.array(z.object({ type: z.string(), x: z.number(), z: z.number() }).passthrough()).max(2500) }),
    loot: z.object({ points: z.array(z.object({ id: z.string(), x: z.number(), z: z.number() }).passthrough()).min(1) }).passthrough(),
    ambience: mapAmbienceSchema,
  })
  .superRefine((m, ctx) => {
    const lim = m.halfSize + 5;
    m.props.forEach((p, i) => {
      if (Math.abs(p.pos[0]) > lim || Math.abs(p.pos[2]) > lim) ctx.addIssue({ code: 'custom', path: ['props', i], message: `${p.name}: fuera del mapa` });
    });
    if (Math.abs(m.playerStart[0]) > m.halfSize || Math.abs(m.playerStart[2]) > m.halfSize)
      ctx.addIssue({ code: 'custom', path: ['playerStart'], message: 'el jugador empieza fuera del mapa' });
    const ids = new Set<string>();
    const sides = new Set<string>();
    m.spawns.forEach((s, i) => {
      if (ids.has(s.id)) ctx.addIssue({ code: 'custom', path: ['spawns', i], message: `spawn repetido ${s.id}` });
      ids.add(s.id);
      sides.add(s.side);
      const edge = m.halfSize - Math.max(Math.abs(s.x), Math.abs(s.z));
      if (edge > 6 || edge < 0.5) ctx.addIssue({ code: 'custom', path: ['spawns', i], message: `spawn ${s.id} no está en el borde` });
    });
    if (sides.size < 3) ctx.addIssue({ code: 'custom', path: ['spawns'], message: 'spawns en menos de 3 lados' });
  });

export type MapJson = z.infer<typeof mapSchema>;
