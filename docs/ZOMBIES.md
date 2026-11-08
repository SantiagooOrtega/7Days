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


## Cuerpo y aspecto de los zombis (procedural, bloque C de ZOMBIS_SKINS.md)

Los 6 tipos de zombi (no los jefes, que siguen con el cuerpo antiguo de primitivas hasta el bloque D) usan **el mismo esqueleto y la misma animación por capas del soldado** (`ProceduralRig`)
con otras proporciones, una sola malla con pesos de hueso (`SkinnedMeshRenderer`, piel + ropa = 2 llamadas de dibujo) y texturas procedurales. Todo es código propio, sin assets externos.

| Tipo | Altura | Rasgos del cuerpo | Triángulos LOD 0 / 1 / 2 | Presupuesto |
|------|--------|-------------------|--------------------------|-------------|
| Común (`common`) | 1.78 m | persona descompuesta, hombros caídos y cabeza ladeada, costillas a la vista | 4 786–5 388 / 2 788–3 274 / 1 458–1 728 | 6 000 |
| Corredor (`runner`) | 1.74 m | delgado y tenso, muy encorvado, brazos largos, columna y costillas marcadas | 4 712–5 814 / 2 536–3 228 / 1 332–1 738 | 6 000 |
| Tanque (`tank`) | 2.64 m | torso enorme, brazo derecho 1.55 veces más grueso, placas de tejido cicatrizado | 5 142–6 208 / 3 178–3 880 / 1 222–1 648 | 12 000 |
| Blindado (`armored`) | 1.82 m | chaleco con placas, puerta atada, neumáticos o chatarra; la cabeza queda expuesta | 5 170–5 910 / 3 078–3 596 / 1 614–1 894 | 6 000 |
| Lanzador (`spitter`) | 1.76 m | bolsas y bultos en espalda y hombros, brazo derecho largo, pústulas verdosas | 4 980–5 908 / 2 934–3 602 / 1 302–1 658 | 6 000 |
| Explosivo (`explosive`) | 1.72 m | abdomen hinchado en un hueso extra (`Belly`) con pústulas y venas naranjas que brillan y pulsan durante la mecha | 4 960–5 672 / 2 854–3 334 / 1 444–1 690 | 6 000 |

(Rangos entre las 3-4 variantes de cada tipo. Código: `Procedural/ZombieLook.cs`, `ZombieModel.cs`, `ZombieTypeFeatures.cs`, `ZombieSkinning.cs`; probado en `ZombieModelTests`.)

**Variantes:** 3 a 4 por tipo (20 en total) con cabeza (`Intact`, `Sunken`, `JawBroken`, `JawExposed`), pelo (`Bald`, `Tufts`, `Matted`), ropa (camisa, uniforme militar, bata, traje, ropa de trabajo, harapos),
calzado (descalzo o botas rotas), armadura (solo el blindado), tonos de piel y ropa, brillo de ojos, suciedad y sangre. **Cada zombi concreto** tira al aparecer (`ZombieIndividual`): variante,
tono de piel (±8 %), suciedad, qué marcas del repertorio lleva (8 posibles: mordidas, tajo, mancha, moratón, costra, salpicadura, chorreras), cojera (45 % de los zombis, en el pie izquierdo o derecho),
desfase de balanceo, cabeza ladeada y hombros desiguales. Cada cuerpo del pool nace con una **altura -6 %, 0 o +6 %** (3 cubos: el rig se construye con las piernas reales, así las zancadas y los pies
siguen siendo exactos). Los tonos y el brillo de los ojos se aplican con `MaterialPropertyBlock` (un bloque por submalla), de modo que todos los zombis de un tipo comparten sus 2 materiales.

**Variantes en JSON (offline, validable):** `Resources/Zombies/zombie_variants.json` (opcional) sustituye a las de fábrica si pasa la validación; el ejemplo con las 20 por defecto está en
`docs/zombie_variants.default.json`. Los enums van como enteros (`JsonUtility`): `head` 0 Intact, 1 Sunken, 2 JawBroken, 3 JawExposed; `hair` 0 Bald, 1 Tufts, 2 Matted; `clothing` 0 Shirt, 1 Military,
2 LabCoat, 3 Suit, 4 Workwear, 5 Rags; `footwear` 0 Bare, 1 BrokenBoots; `armor` 0 None, 1 VestPlates, 2 DoorShield, 3 TireRings, 4 MixedScrap. Esquema zod (para el backend o Gemini offline):

```ts
import { z } from 'zod';
const TYPE_IDS = ['common', 'runner', 'tank', 'armored', 'spitter', 'explosive'] as const;
const tone = (lo: number, hi: number) => z.tuple([z.number().min(lo).max(hi), z.number().min(lo).max(hi), z.number().min(lo).max(hi)]);

export const zombieVariant = z.object({
  id: z.string().min(1), typeId: z.enum(TYPE_IDS), displayName: z.string(),
  head: z.number().int().min(0).max(3), hair: z.number().int().min(0).max(2), clothing: z.number().int().min(0).max(5),
  footwear: z.number().int().min(0).max(1), armor: z.number().int().min(0).max(4), seed: z.number().int(),
  skinTone: tone(0.5, 1.5), clothTone: tone(0.3, 1.6), eyeTint: tone(0, 1),
  eyeGlow: z.number().min(0).max(1), grime: z.number().min(0).max(1), bloodiness: z.number().min(0).max(1),
}).superRefine((v, ctx) => {
  if (v.armor !== 0 && v.typeId !== 'armored') ctx.addIssue({ code: 'custom', message: 'solo el blindado lleva armor' });
  if (v.typeId === 'armored' && v.armor === 0) ctx.addIssue({ code: 'custom', message: 'el blindado necesita armor' });
});

export const zombieVariantFile = z.object({ variants: z.array(zombieVariant).min(1) })
  .refine(f => new Set(f.variants.map(v => v.id)).size === f.variants.length, 'ids repetidos')
  .refine(f => TYPE_IDS.every(t => f.variants.some(v => v.typeId === t)), 'cada tipo necesita al menos una variante');
```
El validador equivalente en C# es `ZombieLookValidator` (`Procedural/ZombieLook.cs`), probado en `ZombieLookTests`.

**Texturas (512 px, hilos de fondo, caché, mipmaps; color y máscara comprimidos BC3, normales y emisión sin pérdida de tipo):** un atlas de piel por tipo (cuerpo y cabeza a 256 px; manos, pies, hueso/dientes, ojo con velo lechoso,
pelo, tejido y los rasgos del tipo a 128 px), un atlas de ropa común (16 telas: camisa, camiseta, uniforme, bata, traje, pantalón, vaquero, mono, harapos, cuero, chapa oxidada, goma, cinta, puerta, venda, saco; el alfa
recorta agujeros y desgarros) y un atlas de heridas (decals con alfa). Memoria medida: 15.7 MB las 8 texturas más 23 MB de mallas con todas las variantes usadas. Código: `ZombieTextures.cs`, `ZombieAtlas.cs`, `Animation/ZombieArt.cs`.

**Capa de daño progresiva (`ZombieDamageLayer`):** al cruzar el 75, 50 y 25 % de vida aparece una herida en la zona del golpe (cabeza, torso, brazos o piernas: las mismas zonas de los hitbox), proyectada con rayos sobre la
malla real (si hay ropa, sale como desgarro). Un headshot mortal hace desaparecer la cabeza (queda el muñón del cuello) y el cuerpo cae.

**Hitboxes:** cabeza (esfera sobre el cráneo), torso (cápsula del pecho y esfera de la pelvis), brazos (brazo y antebrazo+mano por separado, más gruesos en el brazo grande del tanque) y piernas (muslo y
pantorrilla+pie), todos hijos de su hueso: siguen a la animación. Los multiplicadores por zona y la tabla de `BALANCE.md` **no cambian** (las pruebas de balance y de combate pasan sin tocar).

**Rendimiento:** LOD de malla (3 niveles por distancia a la cámara con histéresis, decals solo hasta el LOD 1, sombras solo en el LOD 0), LOD y congelado de animación (cada 1/2/3/6 frames según la distancia; sin animar
a más de 90 m ni fuera de pantalla), pooling por tipo (el pool reutiliza un zombi libre del mismo cuerpo; si está lleno de otros tipos, reconstruye el cuerpo de uno libre), texturas y mallas en caché. Medido
con 30 zombis en la arena real: ver `UNITY_SETUP.md` (sección de zombis procedurales).
