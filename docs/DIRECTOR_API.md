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
