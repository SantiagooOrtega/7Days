# Tipos de zombie y jefes (datos)

Cada tipo es un objeto JSON (mismo formato que usa Unity con `JsonUtility`). Valores por defecto: `Procedural/ZombieData.cs` (`ZombieCatalog.Defaults()`); un ejemplo completo y válido está en `docs/zombies.default.json`.
Para editarlos sin tocar código: menú de Unity **7Days > Exportar zombis por defecto (JSON)** crea `Assets/_Project/Resources/Zombies/zombies.json`, que tiene prioridad si es válido (si no, se avisa en la consola y se usan los del código).

| Tipo | PV | Velocidad | Rasgo | Tabla de drops |
|------|----|-----------|-------|----------------|
| common | 100 | 2.4 | equilibrado | common |
| runner | 55 | 5.2 | rápido y frágil, piernas más duras de acertar | runner |
| tank | 400 | 1.5 | 35 % de resistencia, 70 % a aturdimiento, poca penetración de las armas flojas | tank |
| armored | 160 | 1.9 | torso x0.35, la cabeza sigue x2.5 | armored |
| spitter | 80 | 2.0 | mantiene 12 m, proyectil directo o en arco, flanquea | spitter |
| explosive | 70 | 3.0 | tipo creeper: al estar a 3 m se detiene y arde una mecha de 1.35 s (siseo, parpadeo blanco-rojo, se infla); explota (radio 3.6 m, 45 de daño) | explosive |
| boss_brute / boss_launcher / boss_final | 1500 / 2400 / 3000 | - | ver `docs/BOSSES.md` | boss (rara garantizada) |

## Zombi gigante: no entra a edificios
`canEnterBuildings: false` (solo el **tanque** por defecto) le da un tipo de agente de NavMesh propio con `navRadius` ≥ 0.7 m (el tanque usa 0.8 m = 1.6 m de ancho, las puertas libres miden 1.1–1.2 m) y su propio NavMesh horneado. Los demás zombis usan el agente normal y siguen entrando por las puertas.
Tres capas de seguridad: (1) el radio del agente, (2) un volumen "no caminable" sobre cada casa que solo afecta al agente gigante y (3) en código, si acaba dentro de la planta de una casa se le saca por el borde más cercano (`EnforceBuildingGuard`); el respaldo sin NavMesh también barre una esfera contra los colliders y desliza por las paredes en vez de atravesarlas.
Cuando el jugador está dentro de una casa a la que no cabe, el gigante: se acerca al punto alcanzable más cercano, **golpea el muro** (daño reducido, polvo y sacudida) si el jugador está a su alcance, y si pasa unos 4 s sin alcanzarlo **rodea la casa** por un perímetro de 8 puntos. Con histéresis (empieza a golpear a 3.5+ m y para a ~1.35x más lejos), así que no vibra.
- `navRadius`: radio del agente en metros; 0 = automático (0.35 x escala).

## Explosivo tipo creeper
Persigue como un zombi normal. Al entrar el jugador en `fuseTriggerRadius` (3 m) se **detiene** y arranca la mecha de `fuseSeconds` (1.35 s): siseo, parpadeo blanco-rojo cada vez más rápido (4 a 16 Hz) e inflado progresivo (hasta x1.35). Si el jugador se aleja más de `fuseCancelRadius` (5.5 m) antes de que acabe, se **cancela** (pausa de 0.6 s) y vuelve a perseguir; entre ambos radios la mecha sigue (no parpadea). Al terminar explota.
- **Explosión:** daño en `explosionRadius` con caída lineal hasta el 25 % en el borde; si una pared sólida tapa el trayecto solo pasa `explosionBlockedFactor` (25 %) del daño y del empuje; empuje al jugador (`explosionKnockback` m/s) y a otros zombis, sacudida de cámara, humo, bola de fuego, destello de luz, sonido y un fuego de 3 s. Daña en cadena a otros zombis y, con `explosionBreaksCover`, rompe coberturas ligeras.
- **Si lo matan**, también con un headshot en plena mecha, explota igual (una sola vez).
- Diagnóstico del fallo anterior: no existía ninguna mecha ni disparador, así que el explosivo solo parpadeaba y golpeaba cuerpo a cuerpo; la explosión solo se producía si lo matabas. Ahora la lógica está en `Procedural/FuseBrain.cs` (probada en `FuseTests`).

## Lanzador (spitter)
- `projectile`: `direct` (recto, lo frena la primera `CoverObject`: no daña), `arc` (parábola que cae donde estabas, con marca roja en el suelo 1–1.5 s antes, daño de área, ignora la cobertura frontal) o `mixed` (directo con línea libre; en arco si el jugador lleva ≥ 1 s cubierto y algo tapa el tiro).
- `flankAfterSeconds`: si el jugador pasa tanto tiempo en cobertura y no hay línea de tiro, el lanzador busca un punto lateral con línea libre en el NavMesh.
- `projectileKind`: `debris | rod | acid | grenade` (solo cambia el aspecto), más `projectileDamage`, `projectileSpeed`, `projectileCooldown`, `preferredRange`, `areaRadius`.
- Cuando la cobertura frena o absorbe un golpe, el HUD muestra "La cobertura te protegió".

## Esquema zod
```ts
import { z } from 'zod';

const DROP_KINDS = ['common', 'runner', 'armored', 'tank', 'explosive', 'brute', 'spitter', 'boss'] as const;   // 'brute' = genérico pesado (compatibilidad)
const ABILITIES = ['charge', 'slam', 'throw', 'summon', 'shockwave', 'summon_wave'] as const;
const ZOMBIE_IDS = ['common','runner','tank','armored','spitter','explosive','boss_brute','boss_launcher','boss_final'] as const;

const zones = z.object({
  head: z.number().gt(0).max(6), torso: z.number().gt(0).max(3),
  arms: z.number().gt(0), legs: z.number().gt(0), weakPoint: z.number().gt(0).max(10),
});

const ability = z.object({
  id: z.enum(ABILITIES),
  cooldown: z.number().min(1), telegraph: z.number().min(0.5),   // el aviso mínimo es de 0.5 s: el jugador debe poder reaccionar
  minRange: z.number().min(0), maxRange: z.number().min(0),
  minPhase: z.number().int().min(0).max(2), weight: z.number().gt(0),
  a: z.number(), b: z.number(), c: z.number(),
}).refine(a => a.minRange <= a.maxRange, 'minRange > maxRange');

const boss = z.object({
  day: z.number().int().min(1).max(7),
  phase2At: z.number().gt(0).lt(1), phase3At: z.number().gt(0).lt(1),
  furySpeed: z.tuple([z.number(), z.number(), z.number()]),
  furyAttackRate: z.tuple([z.number(), z.number(), z.number()]),
  globalCooldown: z.number().min(0), abilities: z.array(ability),
  hasWeakPoint: z.boolean(), weakPointExposeSeconds: z.number().min(0), weakPointPeriod: z.number().min(0),
  stunnedExtraDamage: z.number().min(1), wallStunSeconds: z.number().min(0),
  summonType: z.enum(ZOMBIE_IDS), summonCount: z.number().int().min(0),
}).refine(b => b.phase2At > b.phase3At, 'phase2At debe ser mayor que phase3At');

export const zombieType = z.object({
  id: z.string().min(1), displayName: z.string(),
  maxHealth: z.number().min(1).max(20000), speed: z.number().min(0.1).max(12), patrolSpeed: z.number().min(0),
  attackDamage: z.number().min(0).max(200), attackRange: z.number().min(0.5).max(8), attackCooldown: z.number().min(0.2).max(10),
  aggroRange: z.number().min(1), resistance: z.number().min(0).max(0.9), stunResistance: z.number().min(0).max(1),
  zones, scale: z.number().min(0.3).max(4),
  canEnterBuildings: z.boolean(), navRadius: z.number().min(0).max(2),   // false => navRadius >= 0.7 (ver abajo) color: z.tuple([z.number(), z.number(), z.number()]),
  dropKind: z.enum(DROP_KINDS),
  explosionRadius: z.number().min(0).max(12), explosionDamage: z.number().min(0),
  fuseTriggerRadius: z.number().min(1).max(8), fuseCancelRadius: z.number(), fuseSeconds: z.number().min(0.5).max(3),
  explosionKnockback: z.number().min(0).max(30), explosionBlockedFactor: z.number().min(0).max(1), explosionBreaksCover: z.boolean(),
  projectile: z.enum(['none', 'direct', 'arc', 'mixed']), projectileKind: z.enum(['debris', 'rod', 'acid', 'grenade']),
  projectileDamage: z.number().min(0), projectileSpeed: z.number().min(2), projectileCooldown: z.number().min(0.5),
  preferredRange: z.number().min(3), areaRadius: z.number().min(0), flankAfterSeconds: z.number().min(0.5),
  isBoss: z.boolean(), boss,
}).superRefine((t, ctx) => {
  if (t.explosionRadius > 0 && t.fuseCancelRadius < t.fuseTriggerRadius + 1) ctx.addIssue({ code: 'custom', message: 'fuseCancelRadius debe superar a fuseTriggerRadius en al menos 1 m' });
  if (!t.canEnterBuildings && t.navRadius < 0.7) ctx.addIssue({ code: 'custom', message: 'un zombi que no entra a edificios necesita navRadius >= 0.7 (puertas de 1.1–1.2 m)' });
  if (t.isBoss) {
    const ratio = t.maxHealth / 100;
    if (ratio < 10 || ratio > 30) ctx.addIssue({ code: 'custom', message: 'un jefe tiene entre 10 y 30 veces la vida de un común' });
    if (t.stunResistance < 0.8) ctx.addIssue({ code: 'custom', message: 'un jefe resiste el aturdimiento (>= 0.8)' });
    if (t.dropKind !== 'boss') ctx.addIssue({ code: 'custom', message: 'un jefe usa la tabla de drops boss' });
    if (t.boss.abilities.length === 0) ctx.addIssue({ code: 'custom', message: 'un jefe necesita habilidades' });
  }
});

export const zombieFile = z.object({ types: z.array(zombieType).min(1) })
  .refine(f => new Set(f.types.map(t => t.id)).size === f.types.length, 'ids repetidos');
```
El validador equivalente en C# es `ZombieValidator` (`Procedural/ZombieData.cs`), probado en `BalanceTests` y `SpitterTests`.
