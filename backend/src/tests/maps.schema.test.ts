import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapSchema } from '../modules/maps/maps.schemas';

// los mismos JSON que usa Unity como respaldo local (Resources/Maps)
const MAPS_DIR = join(__dirname, '..', '..', '..', 'game', 'Assets', '_Project', 'Resources', 'Maps');
const files = readdirSync(MAPS_DIR).filter((f) => f.endsWith('.json'));

describe('esquema de mapas', () => {
  it('hay al menos el mapa 1', () => {
    expect(files).toContain('calle_en_ruinas.json');
  });

  it.each(files)('%s cumple el esquema', (f) => {
    const json = JSON.parse(readFileSync(join(MAPS_DIR, f), 'utf8'));
    const r = mapSchema.safeParse(json);
    if (!r.success) throw new Error(r.error.issues.map((i) => `${i.path.join('.')}: ${i.message}`).join('\n'));
    expect(r.data.spawns.length).toBeGreaterThanOrEqual(8);
  });

  it('rechaza mapas mal formados', () => {
    const base = JSON.parse(readFileSync(join(MAPS_DIR, 'calle_en_ruinas.json'), 'utf8'));
    const bad = (mut: (m: any) => void) => { const m = structuredClone(base); mut(m); return mapSchema.safeParse(m).success; };
    expect(bad((m) => { m.props[0].kind = 'castillo'; })).toBe(false);
    expect(bad((m) => { m.spawns = m.spawns.slice(0, 5); })).toBe(false);
    expect(bad((m) => { m.spawns[0].x = 0; m.spawns[0].z = 0; })).toBe(false);
    expect(bad((m) => { m.houses = []; })).toBe(false);
    expect(bad((m) => { m.ambience.fogDensity = 1; })).toBe(false);
    expect(bad((m) => { m.props[5].pos[0] = 500; })).toBe(false);
  });
});
