# Lo que el combate expone a la IA director (Fase 4)

Todo en `Procedural/DirectorApi.cs` (puro, probado en `DirectorApiTests`); Unity lo conecta en `Combat/CombatMetricsTracker.cs` y `Combat/ZombieManager.cs`.

## Métricas (lee el director) — `CombatMetricsTracker.Instance.Snapshot`
| Campo | Significado |
|-------|-------------|
| `accuracy`, `shotsFired`, `shotsHit` | aciertos / disparos (la ráfaga de escopeta cuenta como un acierto; cada golpe de barra cuenta como disparo) |
| `headshots`, `headshotRatio`, `headshotKills` | aciertos a la cabeza, su proporción y las muertes por headshot (la cabeza vuela) |
| `kills`, `averageTimeToKill` | muertes y segundos medios desde el primer impacto hasta la muerte |
| `timeInCover`, `coverRatio`, `timePlayed` | tiempo en cobertura (más lanzadores en arco y flanqueo si es alto) |
| `damageTaken`, `damageSources[]`, `damageAmounts[]` | daño recibido por tipo de zombie (`common`, `spitter`, `boss_brute`...) u `other` (caída) |
`CombatMetrics.Reset()` al empezar cada día. F4 muestra el panel en el juego.

## Parámetros (escribe el director) — `ZombieManager.SetTuning(DirectorTuning)`
| Campo | Rango | Efecto |
|-------|-------|--------|
| `healthMultiplier` | 0.25–4 | vida de los zombis normales (los jefes no cambian: siempre 10–30x) |
| `speedMultiplier` | 0.5–2 | velocidad |
| `damageMultiplier` | 0.25–4 | daño de ataques, proyectiles y explosiones |
| `typeIds[]` + `typeWeights[]` | pesos >= 0, suma > 0 | proporción de cada tipo en `SpawnByTuning` |
| `specialFrequency` | 0–4 | multiplica el peso de `spitter` y `explosive` |
| `bossAggressiveness` | 0.5–3 | frecuencia de ataques del jefe y menor pausa entre ataques |
`DirectorTuning.ForDay(1..7)` da la dificultad base creciente de la que partirá el director. Un tuning inválido se ignora con aviso.

```ts
const ids = ['common','runner','tank','armored','spitter','explosive'] as const;
export const directorTuning = z.object({
  healthMultiplier: z.number().min(0.25).max(4), speedMultiplier: z.number().min(0.5).max(2), damageMultiplier: z.number().min(0.25).max(4),
  bossAggressiveness: z.number().min(0.5).max(3), specialFrequency: z.number().min(0).max(4),
  typeIds: z.array(z.enum(ids)).min(1), typeWeights: z.array(z.number().min(0)).min(1),
}).refine(t => t.typeIds.length === t.typeWeights.length && t.typeWeights.some(w => w > 0), 'typeIds/typeWeights incoherentes');
```

## Estadísticas guardadas y reglas por estadísticas (cobertura y fatiga)
Al terminar cada día (`CombatMetricsTracker.EndDay`) las métricas de hoy se suman a las guardadas (`CombatStatsRecord.Merge`) y se envían con `PUT /api/progress` (campo `stats`, ver `docs/API.md`). Al iniciar sesión se cargan con `GET /api/progress`. Sin sesión no se guarda nada.

| Estadística guardada | Quién la produce | Para qué la usa el director |
|----------------------|------------------|-----------------------------|
| `coverSeconds`, `poppedOutSeconds`, `blindFireSeconds` | exposición de la cobertura (pop-out) | **abuso de cobertura** |
| `sprintSeconds`, `maxFatigue` (0–1) | cámara de carrera | **corredor agotado** |
| `kills`, `headshots`, `headshotKills`, `averageTimeToKill`, `shotsFired/Hit`, `damageTaken`, `deaths` | combate | rendimiento (Fase 4) |

Reglas (`DirectorRules.Adjust`, pura, devuelve una copia del tuning y los motivos):
1. **Abuso de cobertura** (`CoverAbuse.IsAbusing` sobre lo guardado + lo de hoy: ≥ 20 s en cobertura y > 85 % escondido): `specialFrequency` x1.5 (tope 4), `coverPressure` = 1 (los lanzadores flanquean en la mitad de tiempo) y más arcos que ignoran la cobertura frontal.
2. **Corredor agotado** (`maxFatigue` ≥ 0.8 con ≥ 30 s corriendo): el peso de los corredores x1.3.
Los motivos salen en la Console (`[7Days] Director: ...`) y en el panel F4.

```ts
// stats (PUT /api/progress): ver progressSchema en backend/src/modules/progress/progress.routes.ts
const stats = z.object({
  kills: z.number().int().min(0), shotsFired: z.number().int().min(0), shotsHit: z.number().int().min(0), deaths: z.number().int().min(0),
  coverSeconds: z.number().min(0).max(604800).optional(), poppedOutSeconds: z.number().min(0).max(604800).optional(), blindFireSeconds: z.number().min(0).max(604800).optional(),
  sprintSeconds: z.number().min(0).max(604800).optional(), maxFatigue: z.number().min(0).max(1).optional(),
}).catchall(z.number()).refine(s => s.shotsHit <= s.shotsFired);
```
`directorTuning` gana `coverPressure: z.number().min(0).max(1)`.
