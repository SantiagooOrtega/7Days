# Armas, munición, botiquines y botín

Todo el sistema está en datos (JSON) para que el backend y Gemini (offline, `npm run content:generate`) puedan definirlo después. Se valida con
`LootValidator` en el juego y con el esquema zod de abajo en el backend.

## Armas (un ScriptableObject por arma)
| id | Arma | Munición | Rareza | Daño | Cadencia | Cargador | Notas |
|----|------|----------|--------|------|----------|----------|-------|
| `pistol` | Pistola | pistola | común | 18 | 330 rpm | 12 | arma inicial |
| `revolver` | Revólver | pistola | poco común | 48 | 130 rpm | 6 | mucho retroceso |
| `smg` | Subfusil | pistola | poco común | 12 | 850 rpm | 30 | automático, dispersión alta |
| `shotgun` | Escopeta | escopeta | poco común | 12 x 8 perdigones | 70 rpm | 6 | alcance corto |
| `assault_rifle` | Fusil de asalto | rifle | rara | 36 | 520 rpm | 30 | automático |
| `sniper` | Rifle de francotirador | rifle | épica | 90 | 55 rpm | 5 | alcance 260 m, casi sin dispersión al apuntar |
| `bar` | Barra metálica | — | común | 38 | 100 rpm | — | cuerpo a cuerpo (alcance 2.1 m) |

- Los ScriptableObjects (`WeaponAsset`) están en `Assets/_Project/Resources/Weapons`. **Se crean solos** la primera vez que Unity compila; también desde el menú **7Days > Crear armas por defecto (ScriptableObjects)**. Se editan en el Inspector: daño, cadencia, cargador, recoil, dispersión (sin apuntar y apuntando), alcance, rareza y tipo de munición. Lo que falte sale de los valores por defecto.
- Colores de rareza: común gris, poco común verde, rara azul, épica morada (nombre del arma en el HUD y brillo del drop).

## Munición por tipo y reservas máximas
Tres reservas separadas, compartidas por las armas del mismo tipo: **pistola** (pistola, revólver, subfusil) máx. 48, **escopeta** máx. 20, **rifle** (fusil, francotirador) máx. 90 (`ammoLimits`).
**Munición llena:** si una reserva está llena, recoger munición de ese tipo muestra "Munición llena" y el objeto **no se consume**. Lo mismo con vendas y botiquines (tope por tipo) y con las armas (máx. 5; si ya tienes el arma, se convierte en media cargador de munición, y si esa reserva está llena tampoco se consume).

## Botiquines (barra de curación cancelable)
| Objeto | Cura | Tiempo | Máx. que llevas |
|--------|------|--------|-----------------|
| Venda | 15 | 1.2 s | 5 |
| Botiquín pequeño | 30 | 2.5 s | 3 |
| Botiquín mediano | 60 | 4 s | 2 |
| Botiquín grande | 100 | 6 s | 1 |

**H** usa el mejor botiquín para la vida que falta y **B** una venda. La vida se recupera al terminar la barra; **recibir daño la cancela y no gasta el botiquín** (volver a pulsar la tecla también). Con la vida completa avisa y no gasta nada.

## Cómo se consigue (solo de dos formas)
1. **Buscando en el mapa** (~18 puntos): muebles de la casa, maleteros de coches, escombros, escondites entre arbustos y cajas de suministros. Se busca **manteniendo E** de 1 a 2 s (el aviso y la barra salen en el centro de la pantalla). Las zonas de más riesgo (cajas lejanas y expuestas, `risk` 2) usan la tabla `high` con mejor botín. Hay unos pocos objetos sueltos en el suelo, de poca cosa.
2. **Al matar zombies** (`DropSystem.OnZombieKilled("tipo", posición)`, lo llamará el sistema de zombies de la Fase 3): común 6%, corredor 12%, bruto 22%, lanzador 25% (tabla de especiales) y el **jefe siempre**, con algo **raro o mejor** (`minRarity`).

## Escasez calibrada
- **Inicio:** pistola con 6 balas en el cargador y 6 de reserva, más la barra.
- **Tope de objetos en el suelo** (`maxActivePickups` = 24: puntos + drops) y **máximo de drops por día** (`maxDropsPerDay` = 8).
- **Sin respawn infinito:** los puntos `once` nunca reaparecen; los demás reaparecen como mucho `maxRespawnsPerPoint` (1) veces, y solo una fracción pequeña de lo vaciado por día (`respawnFractionPerDay` = 25%), que el director puede escalar o apagar (`LootDirectorState.RespawnScale`).
- **Valores esperados** de toda la partida (mapa + drops de 7 días; probado en `LootTests`): pistola ≈ 67 balas, escopeta ≈ 14, rifle ≈ 33, curación ≈ 490 HP, ≈ 5 armas nuevas (≈ 1.5 raras, ≈ 0.7 épicas, casi todas del jefe).
- Los drops y los pickups usan un **pool**, con un **aro y una luz suave** del color de su rareza (solo las 8 luces más cercanas están encendidas) y avisos en el HUD al recoger.

## Formato JSON (`Resources/Loot/loot.json`; menú **7Days > Exportar botín (JSON)**)
```json
{
  "startWeapons": ["pistol", "bar"], "startMagazine": 6, "startReserve": 6,
  "ammoLimits": { "pistol": 48, "shotgun": 20, "rifle": 90 },
  "maxActivePickups": 24, "maxDropsPerDay": 8, "respawnFractionPerDay": 0.25, "maxRespawnsPerPoint": 1,
  "tables": [ { "id": "mid", "entries": [ { "kind": "ammo", "id": "pistol", "amount": 7, "weight": 3 }, { "kind": "weapon", "id": "revolver", "amount": 1, "weight": 1 } ] } ],
  "drops":  [ { "zombie": "boss", "chance": 1, "table": "drop_boss", "guaranteed": true, "minRarity": 2 } ],
  "points": [ { "id": "far_crate_a", "x": -26, "z": -26, "container": "crate", "table": "high", "risk": 2, "searchSeconds": 2, "once": true } ]
}
```
`kind`: `weapon` (id del arma), `ammo` (`pistol|shotgun|rifle`) o `medkit` (`bandage|small|medium|large`). `container`: `ground | crate | car | furniture | rubble | bush`. `zombie`: `common | runner | brute | spitter | boss`. `minRarity` 0 a 3 (común, poco común, rara, épica).

## Esquema zod (backend)
```ts
const rarity = z.number().int().min(0).max(3);
const entry = z.object({ kind: z.enum(['weapon','ammo','medkit']), id: z.string(), amount: z.number().int().min(1).max(60).default(1), weight: z.number().positive() })
  .refine(e => e.kind === 'weapon' ? weaponIds.includes(e.id) : e.kind === 'ammo' ? ['pistol','shotgun','rifle'].includes(e.id) : ['bandage','small','medium','large'].includes(e.id));
export const lootLayout = z.object({
  startWeapons: z.array(z.string()).max(5), startMagazine: z.number().int().min(0), startReserve: z.number().int().min(0),
  ammoLimits: z.object({ pistol: z.number().int().min(1), shotgun: z.number().int().min(1), rifle: z.number().int().min(1) }),
  maxActivePickups: z.number().int().min(1).max(80), maxDropsPerDay: z.number().int().min(0).max(40),
  respawnFractionPerDay: z.number().min(0).max(1), maxRespawnsPerPoint: z.number().int().min(0).max(5),
  tables: z.array(z.object({ id: z.string(), entries: z.array(entry).min(1) })).min(1),
  drops: z.array(z.object({ zombie: z.enum(['common','runner','brute','spitter','boss']), chance: z.number().min(0).max(1), table: z.string(), guaranteed: z.boolean().default(false), minRarity: rarity.default(0) })),
  points: z.array(z.object({ id: z.string(), x: z.number().min(-60).max(60), z: z.number().min(-60).max(60), container: z.enum(['ground','crate','car','furniture','rubble','bush']),
    table: z.string(), risk: z.number().int().min(0).max(2), searchSeconds: z.number().min(0).max(6).default(1.5), once: z.boolean().default(true) })).max(240),
}).superRefine((l, ctx) => { /* cada table/drop/point debe referenciar una tabla existente */ });
```
El arma se define con `WeaponData`: `id, displayName, kind, rarity, ammoType, damage, pellets, roundsPerMinute, magazineSize, reloadTime, recoil, spreadHip, spreadAim, range, automatic, color[3], length, thickness`.

> Daño por disparo recalibrado en la Fase 3 (Bloque 2); caída por distancia, penetración y multiplicador de cabeza de cada arma en `docs/BALANCE.md`.
