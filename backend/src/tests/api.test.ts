import request from 'supertest';
import { beforeEach, describe, expect, it } from 'vitest';
import { createApp } from '../app';
import { prisma } from '../db';

const app = createApp({ loginRateLimitMax: 1000 });

const alice = { email: 'alice@example.com', username: 'alice', password: 'password123' };
const bob = { email: 'bob@example.com', username: 'bob', password: 'password456' };

async function signup(u = alice) {
  const res = await request(app).post('/api/auth/register').send(u);
  expect(res.status).toBe(201);
  return res.body as { accessToken: string; refreshToken: string; user: { id: string } };
}
const bearer = (t: string) => ({ Authorization: `Bearer ${t}` });

beforeEach(async () => {
  await prisma.ranking.deleteMany();
  await prisma.gameProgress.deleteMany();
  await prisma.refreshToken.deleteMany();
  await prisma.user.deleteMany();
});

describe('registro y login', () => {
  it('registra, no expone el hash y guarda la contraseña hasheada', async () => {
    const res = await request(app).post('/api/auth/register').send(alice);
    expect(res.status).toBe(201);
    expect(res.body.user).toEqual({ id: expect.any(String), email: alice.email, username: 'alice' });
    expect(JSON.stringify(res.body)).not.toContain('passwordHash');
    const row = await prisma.user.findUniqueOrThrow({ where: { email: alice.email } });
    expect(row.passwordHash).toMatch(/^\$argon2/);
  });

  it('rechaza duplicados con 409', async () => {
    await signup();
    const res = await request(app).post('/api/auth/register').send({ ...alice, username: 'otro' });
    expect(res.status).toBe(409);
  });

  it('valida la entrada con zod', async () => {
    const res = await request(app).post('/api/auth/register').send({ email: 'x', username: 'a', password: '1' });
    expect(res.status).toBe(400);
    expect(res.body.error.code).toBe('validation_error');
  });

  it('login correcto e incorrecto', async () => {
    await signup();
    const ok = await request(app).post('/api/auth/login').send({ email: alice.email, password: alice.password });
    expect(ok.status).toBe(200);
    expect(ok.body.accessToken).toBeTruthy();
    const bad = await request(app).post('/api/auth/login').send({ email: alice.email, password: 'wrong-pass' });
    expect(bad.status).toBe(401);
    const none = await request(app).post('/api/auth/login').send({ email: 'no@example.com', password: 'whatever1' });
    expect(none.status).toBe(401);
    expect(none.body.error.message).toBe(bad.body.error.message);
  });

  it('aplica rate limiting al login', async () => {
    const limited = createApp({ loginRateLimitMax: 3 });
    const statuses: number[] = [];
    for (let i = 0; i < 5; i++) {
      const r = await request(limited).post('/api/auth/login').send({ email: 'a@example.com', password: 'bad-password' });
      statuses.push(r.status);
    }
    expect(statuses.slice(0, 3)).toEqual([401, 401, 401]);
    expect(statuses.slice(3)).toEqual([429, 429]);
  });
});

describe('JWT y perfil', () => {
  it('exige token y devuelve el perfil', async () => {
    expect((await request(app).get('/api/me')).status).toBe(401);
    expect((await request(app).get('/api/me').set(bearer('basura'))).status).toBe(401);
    const { accessToken } = await signup();
    const res = await request(app).get('/api/me').set(bearer(accessToken));
    expect(res.status).toBe(200);
    expect(res.body.user.username).toBe('alice');
    expect(res.body.user.passwordHash).toBeUndefined();
  });
});

describe('refresh token rotativo', () => {
  it('rota el token y guarda solo el hash', async () => {
    const { refreshToken } = await signup();
    const stored = await prisma.refreshToken.findMany();
    expect(stored.every((t) => t.tokenHash !== refreshToken)).toBe(true);

    const res = await request(app).post('/api/auth/refresh').send({ refreshToken });
    expect(res.status).toBe(200);
    expect(res.body.refreshToken).not.toBe(refreshToken);
    expect((await request(app).get('/api/me').set(bearer(res.body.accessToken))).status).toBe(200);
  });

  it('si se reutiliza un token viejo, revoca toda la sesión', async () => {
    const { refreshToken: first } = await signup();
    const second = (await request(app).post('/api/auth/refresh').send({ refreshToken: first })).body.refreshToken;

    const reuse = await request(app).post('/api/auth/refresh').send({ refreshToken: first });
    expect(reuse.status).toBe(401);
    // el token nuevo de la misma familia también queda invalidado
    const after = await request(app).post('/api/auth/refresh').send({ refreshToken: second });
    expect(after.status).toBe(401);
  });

  it('logout invalida el refresh token', async () => {
    const { refreshToken } = await signup();
    expect((await request(app).post('/api/auth/logout').send({ refreshToken })).status).toBe(204);
    expect((await request(app).post('/api/auth/refresh').send({ refreshToken })).status).toBe(401);
  });

  it('rechaza tokens expirados', async () => {
    const { refreshToken } = await signup();
    await prisma.refreshToken.updateMany({ data: { expiresAt: new Date(Date.now() - 1000) } });
    expect((await request(app).post('/api/auth/refresh').send({ refreshToken })).status).toBe(401);
  });
});

describe('progreso', () => {
  const payload = {
    currentDay: 3,
    inventory: { weapons: ['rifle'], ammo: 90 },
    stats: { kills: 25, shotsFired: 100, shotsHit: 60, deaths: 1 },
  };

  it('exige autenticación', async () => {
    expect((await request(app).get('/api/progress')).status).toBe(401);
    expect((await request(app).put('/api/progress').send(payload)).status).toBe(401);
  });

  it('devuelve el día 1 por defecto, guarda y carga', async () => {
    const { accessToken } = await signup();
    const empty = await request(app).get('/api/progress').set(bearer(accessToken));
    expect(empty.body.progress.currentDay).toBe(1);

    const put = await request(app).put('/api/progress').set(bearer(accessToken)).send(payload);
    expect(put.status).toBe(200);
    const got = await request(app).get('/api/progress').set(bearer(accessToken));
    expect(got.body.progress).toMatchObject(payload);
  });

  it('guarda y devuelve las estadísticas de cobertura y fatiga', async () => {
    const { accessToken } = await signup();
    const stats = {
      kills: 40, shotsFired: 200, shotsHit: 120, deaths: 0, headshots: 30, headshotKills: 22, averageTimeToKill: 2.4, damageTaken: 180.5,
      coverSeconds: 310.5, poppedOutSeconds: 42.25, blindFireSeconds: 8, sprintSeconds: 95, maxFatigue: 0.87, daysPlayed: 3,
    };
    const put = await request(app).put('/api/progress').set(bearer(accessToken)).send({ ...payload, stats });
    expect(put.status).toBe(200);
    const got = await request(app).get('/api/progress').set(bearer(accessToken));
    expect(got.body.progress.stats).toEqual(stats);
  });

  it('las estadísticas antiguas (sin los campos nuevos) siguen siendo válidas', async () => {
    const { accessToken } = await signup();
    expect((await request(app).put('/api/progress').set(bearer(accessToken)).send(payload)).status).toBe(200);
  });

  it('rechaza estadísticas imposibles', async () => {
    const { accessToken } = await signup();
    const bad = async (stats: object) => (await request(app).put('/api/progress').set(bearer(accessToken)).send({ ...payload, stats: { ...payload.stats, ...stats } })).status;
    expect(await bad({ maxFatigue: 1.5 })).toBe(400);
    expect(await bad({ maxFatigue: -0.1 })).toBe(400);
    expect(await bad({ coverSeconds: -5 })).toBe(400);
    expect(await bad({ coverSeconds: 10 ** 9 })).toBe(400);
    expect(await bad({ shotsHit: 500 })).toBe(400);
    expect(await bad({ headshotKills: 99 })).toBe(400);
    expect(await bad({ daysPlayed: 9 })).toBe(400);
    expect(await bad({ sprintSeconds: 'mucho' })).toBe(400);
  });

  it('cada jugador tiene su propio progreso', async () => {
    const a = await signup(alice);
    const b = await signup(bob);
    await request(app).put('/api/progress').set(bearer(a.accessToken)).send(payload);
    const got = await request(app).get('/api/progress').set(bearer(b.accessToken));
    expect(got.body.progress.currentDay).toBe(1);
  });

  it('valida el rango del día', async () => {
    const { accessToken } = await signup();
    const res = await request(app).put('/api/progress').set(bearer(accessToken)).send({ ...payload, currentDay: 9 });
    expect(res.status).toBe(400);
  });
});

describe('ranking', () => {
  it('ordena por puntaje y muestra el nombre de usuario', async () => {
    const a = await signup(alice);
    const b = await signup(bob);
    const submit = (t: string, score: number) =>
      request(app).post('/api/rankings').set(bearer(t)).send({ score, daysSurvived: 7, kills: 50, victory: true });
    expect((await submit(a.accessToken, 1000)).status).toBe(201);
    expect((await submit(b.accessToken, 2500)).status).toBe(201);

    const res = await request(app).get('/api/rankings').set(bearer(a.accessToken));
    expect(res.status).toBe(200);
    expect(res.body.rankings.map((r: { username: string }) => r.username)).toEqual(['bob', 'alice']);
    expect(res.body.rankings[0].position).toBe(1);
  });

  it('rechaza puntajes inválidos y requiere auth', async () => {
    const { accessToken } = await signup();
    const bad = await request(app).post('/api/rankings').set(bearer(accessToken)).send({ score: -5, daysSurvived: 7, kills: 1, victory: true });
    expect(bad.status).toBe(400);
    expect((await request(app).get('/api/rankings')).status).toBe(401);
  });
});

describe('seguridad general', () => {
  it('envía cabeceras de helmet y responde 404 JSON', async () => {
    const res = await request(app).get('/nada');
    expect(res.status).toBe(404);
    expect(res.headers['x-content-type-options']).toBe('nosniff');
    expect(res.headers['x-powered-by']).toBeUndefined();
  });
});
