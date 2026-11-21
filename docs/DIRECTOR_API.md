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


## Horda (oleadas)
Durante la oleada cada zombi nace en modo horda: conoce la posición del jugador y lo persigue por el NavMesh desde que sale del suelo (ruta cada 0.5-1 s si está a más de 12 m, cada 0.2 s cerca). En la preparación y para los zombis ya presentes rige el radio de detección normal.

| Parámetro (`DirectorTuning`) | Rango | Defecto | Efecto |
|---|---|---|---|
| `hordeDelayMax` | 0..5 s | 1.5 | retardo aleatorio por zombi antes de lanzarse |
| `hordeSpeedJitter` | 0..0.3 | 0.12 | variación de velocidad (±) por zombi, para que no lleguen todos a la vez |


## Estadísticas por día y partida, puntuación y director adaptativo (Paso 2)
Código: `Procedural/Performance.cs` (puro, `PerformanceTests`) y `World/DayLoop.cs`.

- **Recolector**: al terminar cada día (`DayLoop.RecordDay`) se guarda un `DayStats` en `DayLoop.Match` (`MatchStats`): día, segundos de oleada, zombis de la oleada, bajas (total, a la cabeza y por tipo), disparos y aciertos, daño recibido, tiempo en cobertura, tiempo jugado, si hubo jefe y si murió. La partida sabe su mapa, si terminó y si ganó; `RankingPoints()` da los puntos del ranking. Al ganar o morir se lanza `DayLoop.MatchFinished` (lo usará el Paso 3 para guardar y subir).
- **Puntuación 0-100** (`PerformanceScore.Raw`): precisión (25, llena al 60 %), muertes a la cabeza (15, llena al 40 %), poco daño por zombi (30, 0 a partir de 8 de vida por baja), rapidez (20, llena a 6 s por zombi) y cobertura equilibrada (10, ideal entre el 10 % y el 60 % del tiempo). Morir deja el día en 20 como mucho. **Suavizada**: 60 % lo anterior + 40 % lo de hoy, empezando en 50.
- **Reglas** (`DirectorAdapt.Apply`, en este orden dentro de `DayLoop.TuningFor`): dificultad base del día → reglas por estadísticas guardadas (cobertura, fatiga) → ajuste por la puntuación suavizada → tipos aún bloqueados fuera. Entre 45 y 55 no cambia nada. Fuera de esa franja, `delta` va de -1 a 1 y cada parámetro se multiplica por `1 + k·delta` con tope ±25 %: vida k=0.25, velocidad k=0.10, daño k=0.20, especiales k=0.30 (tope 25 %), retardo de la horda k=-0.30. El resultado siempre pasa `DirectorTuning.Validate`.
- **Explicación en texto**: cada paso añade un motivo (p. ej. «rendimiento 78/100 (alto: más difícil): vida +14 %, velocidad +6 %, daño +11 %, especiales +17 %, retardo de la horda -17 %»). Se ve en **F4** (puntuación suavizada, historial por día y motivos) y en el **panel de resumen** del día (desglose de la puntuación y lo que hará el director mañana).

### Tabla por día (base y rango posible con el ajuste por rendimiento)
| Día | Zombis | Vivos a la vez | Jefe | Nuevos | Vida x | Velocidad x | Daño x | Especiales x | Balas/zombi |
|----:|-------:|---------------:|------|--------|--------|-------------|--------|--------------|------------:|
| 1 | 14 | 8 | — | común | 1.00 (0.75–1.25) | 1.00 (0.90–1.10) | 1.00 (0.80–1.20) | 0.60 (0.45–0.75) | 1.50 |
| 2 | 21 | 10 | — | corredor | 1.10 (0.83–1.38) | 1.03 (0.93–1.14) | 1.07 (0.85–1.28) | 0.80 (0.60–1.00) | 1.45 |
| 3 | 28 | 12 | Bruto | lanzador | 1.20 (0.90–1.50) | 1.07 (0.96–1.17) | 1.13 (0.91–1.36) | 1.00 (0.75–1.25) | 1.40 |
| 4 | 35 | 14 | — | explosivo, blindado | 1.30 (0.98–1.62) | 1.10 (0.99–1.21) | 1.20 (0.96–1.44) | 1.20 (0.90–1.50) | 1.35 |
| 5 | 42 | 16 | Lanzador | tanque | 1.40 (1.05–1.75) | 1.13 (1.02–1.25) | 1.27 (1.01–1.52) | 1.40 (1.05–1.75) | 1.30 |
| 6 | 49 | 18 | — | — | 1.50 (1.12–1.88) | 1.17 (1.05–1.28) | 1.33 (1.07–1.60) | 1.60 (1.20–2.00) | 1.25 |
| 7 | 56 | 20 | Final | — | 1.60 (1.20–2.00) | 1.20 (1.08–1.32) | 1.40 (1.12–1.68) | 1.80 (1.35–2.25) | 1.20 |

Entre paréntesis: con puntuación 0 (más fácil) y 100 (más difícil). Las reglas de cobertura y fatiga se aplican antes y pueden subir los especiales o los corredores por encima de esto (especiales hasta 4).
