# Balance de combate

## Fórmula
```
daño final = daño base × falloff(distancia) × multiplicador de zona × (1 − resistencia efectiva)
resistencia efectiva = clamp(resistencia del zombi − penetración de armadura del arma, 0, 0.95)
```
- **Falloff:** 1 hasta `falloffStart × alcance`; luego baja linealmente hasta `minDamageFraction` en el alcance máximo.
- **Zona** (por defecto, configurable por tipo de zombi): cabeza ×2.5, torso ×1.0, brazos ×0.6, piernas ×0.7, punto débil de jefe ×4. En la **cabeza** se multiplica además por el `headshotMultiplier` del arma.
- **Redondeo estable:** mitades hacia arriba; nunca negativo; si el daño es positivo el mínimo es 1.
- **Escopeta:** el daño por perdigón se calcula por separado y se suma (8 perdigones). Las tablas suponen que todos aciertan; a distancia media acertarán menos.
- Código: `Procedural/CombatModel.cs` (`DamageModel`), probado en `CombatTests` y `BalanceTests`.

## Armas (`WeaponCatalog.Defaults()`)
| Arma | Daño | Falloff desde | Mín. a alcance máx. | Penetración | ×Cabeza (arma) | Empuje |
|------|------|---------------|---------------------|-------------|----------------|--------|
| pistol | 18 | 40 % | 50 % | 0.10 | 1.2 | 0.8 |
| revolver | 48 | 45 % | 60 % | 0.35 | 1.3 | 1.4 |
| smg | 12 | 30 % | 35 % | 0.00 | 1.0 | 0.5 |
| shotgun | 12 × 8 | 15 % | 12 % | 0.05 | 1.0 | 2.4 |
| assault_rifle | 36 | 35 % | 55 % | 0.30 | 1.0 | 1.0 |
| sniper | 90 | 50 % | 100 % | 0.80 | 1.3 | 2.0 |
| bar (melee) | 38 | sin caída | - | 0.20 | 1.0 | 1.8 |

## Disparos para matar a un zombi común (100 PV) a 10 m
| Arma | Daño cabeza | Daño cuerpo | Tiros a la cabeza | Tiros al cuerpo | Objetivo |
|------|-------------|-------------|-------------------|-----------------|----------|
| pistol | 54 | 18 | **2** | **6** | 2 cabeza / ~6 cuerpo |
| revolver | 156 | 48 | 1 | 3 | - |
| smg | 30 | 12 | 4 | 9 | - |
| shotgun (cerca, 3 m) | 240 | 96 | 1 | **2** (≤ 2) | 1–2 a quemarropa |
| assault_rifle | 90 | 36 | 2 | **3** | 2–3 al cuerpo |
| sniper | 293 | 90 | **1** | 2 | 1 a la cabeza |
| bar | 95 | 38 | 2 | 3 | - |

A alcance máximo la pistola necesita 4 a la cabeza y 12 al cuerpo, y la escopeta pasa de 2 a 13 tiros al cuerpo (a 28 m): por eso solo sirve de cerca.
`BalanceTests` falla si alguno de los objetivos de arriba sale de su rango.

## Otros zombis a 10 m (tiros cabeza / cuerpo)
| Tipo | PV | pistol | revolver | smg | shotgun | assault | sniper | bar |
|------|----|--------|----------|-----|---------|---------|--------|-----|
| common | 100 | 2/6 | 1/3 | 4/9 | 1/2 | 2/3 | 1/2 | 2/3 |
| runner | 55 | 2/4 | 1/2 | 2/5 | 1/1 | 1/2 | 1/1 | 1/2 |
| tank | 400 | 13/37 | 4/10 | 25/58 | 4/9 | 6/14 | 2/6 | 7/15 |
| armored | 160 | 3/27 | 2/10 | 6/40 | 1/7 | 2/13 | 1/5 | 2/13 |
| spitter | 80 | 2/5 | 1/2 | 3/7 | 1/2 | 1/3 | 1/1 | 1/3 |
| explosive | 70 | 2/4 | 1/2 | 3/6 | 1/1 | 1/2 | 1/1 | 1/2 |

Lectura: el **tanque** casi ignora la pistola y el subfusil (poca penetración, 35 % de resistencia) y pide revólver, fusil o francotirador; el **blindado** obliga a apuntar a la cabeza (el torso recibe ×0.35).

## Armas al buscar (`Procedural/WeaponSearch.cs`, probado en `WeaponSearchTests`)
Antes las armas solo estaban en las tablas `mid`/`high` con peso bajo, así que casi nunca salían. Ahora el arma es una tirada aparte; la munición y los botiquines se tiran en la misma tabla de siempre, sin armas, así que su escasez no cambia.

**Probabilidad por contenedor** (antes del pity; `risk` ≥ 2 suma +3 % por nivel sobre 1; tope 25 %):
| Contenedor | Base | Zona de riesgo 2 |
|------------|------|------------------|
| escombros (`rubble`) | 8 % | 11 % |
| arbusto (`bush`) | 9 % | 12 % |
| coche (`car`) | 11 % | 14 % |
| caja (`crate`) | 12 % | 15 % |
| mueble de la casa (`furniture`) | 15 % | 18 % |
| suelo (`ground`) | no se busca | - |

**Pity timer:** tras 7 búsquedas seguidas sin arma, la probabilidad sube +6 % por cada búsqueda extra, y a las 15 sin arma la siguiente la da seguro. Simulación de 50 000 búsquedas con el mapa por defecto: tasa real **15.9 %**, una arma cada **6.3 búsquedas** de media, racha máxima sin arma **16**.

**Rareza ponderada** (entre las armas que el jugador NO tiene; si no queda de una rareza se reparte entre las que sí): común 50 / poco común 35 / rara 12 / épica 2, y en zonas de riesgo 2: 20 / 35 / 33 / 12. Con pistola y barra iniciales la pistola y la barra (únicas comunes) ya las tienes, así que sale: poco común (revólver, subfusil, escopeta) ≈ 64 %, rara (fusil) ≈ 28 %, épica (francotirador) ≈ 7.5 %.

**Sin duplicados:** nunca se da un arma que ya tienes. Si ya las tienes todas se da munición de rifle (`duplicateAmmo`). (Y si aun así recoges una repetida del suelo, se convierte en munición de su tipo, como antes.)

**Armas garantizadas:** 3 puntos tienen `guaranteeWeapon` y dan siempre un arma que no tienes: `house_shelf` (dentro de la casa), `far_crate_b` (lejos y expuesta) y `mid_crate_b` (una caja intermedia). Al empezar cada día, si hay menos de 3 armas disponibles en el mapa (puntos garantizados sin buscar + armas sueltas en el suelo que no tienes), se vuelven a armar puntos garantizados hasta llegar a 3 (o a las que te falten). `WeaponGuarantee.ToRearm` lo calcula y se prueba.

La prueba `Simulates1000Searches_WeaponRateIsInRange` falla si la tasa de 1000 búsquedas sale de 10–18 %, y `ChancesPerContainer_AreLowButReal...` si algún contenedor sale de 8–16 %.

```ts
const weaponSearch = z.object({
  rubble: z.number().min(0).max(0.5), bush: z.number().min(0).max(0.5), car: z.number().min(0).max(0.5), crate: z.number().min(0).max(0.5), furniture: z.number().min(0).max(0.5),
  riskBonus: z.number().min(0).max(0.2), maxChance: z.number().min(0).max(0.5),
  pityStart: z.number().int().min(1), pityStep: z.number().min(0).max(0.5), pityGuarantee: z.number().int(),
  rarityWeights: z.tuple([z.number().min(0), z.number().min(0), z.number().min(0), z.number().min(0)]),
  riskRarityWeights: z.tuple([z.number().min(0), z.number().min(0), z.number().min(0), z.number().min(0)]),
  duplicateAmmo: z.number().int().min(0), guaranteedMin: z.number().int().min(0).max(6),
}).refine(w => w.pityGuarantee > w.pityStart && w.rarityWeights.some(x => x > 0));
// en cada punto del layout: guaranteeWeapon: z.boolean() (no puede ser container 'ground'); y el layout necesita >= guaranteedMin puntos con guaranteeWeapon
```

## Drops de zombis (un solo drop por muerte)
Cada muerte hace UNA tirada (`LootRoller.RollDrop`): probabilidad del tipo y, si toca, un objeto de su tabla ponderada. Los drops respetan el tope diario (`maxDropsPerDay` = 8) y el de pickups en el suelo (`maxActivePickups` = 24); un drop sin hueco se descarta con aviso en la Console. Nunca sueltan un arma que el jugador ya tiene (se convierte en munición de su tipo). Brillan con una luz suave (los drops tienen prioridad sobre los demás en el reparto de luces).

| Tipo | Probabilidad | Tabla |
|------|--------------|-------|
| common | 12 % | `drop_common` |
| runner | 12 % | `drop_special` |
| explosive | 8 % | `drop_common` |
| armored | 20 % | `drop_heavy` |
| tank | 30 % | `drop_heavy` |
| spitter | 25 % | `drop_special` |
| boss | 100 % (rara o mejor, fuera del tope diario) | `drop_boss` |
| brute (genérico, sin uso por defecto) | 25 % | `drop_heavy` |

Pesos de las tablas (probabilidad relativa dentro del drop; armas solo poco comunes o raras, ≈ 4–8 % de los drops):
- `drop_common`: vendaje 2, munición de pistola 3, de escopeta 0.6, de rifle 0.6, revólver 0.12, subfusil 0.12.
- `drop_special`: pistola 4 (x4 balas), escopeta 2, rifle 2, botiquín pequeño 1.3, vendaje 1.5, revólver/subfusil/escopeta 0.2 cada una.
- `drop_heavy`: rifle 3, pistola 2, escopeta 2, pequeño 1.5, mediano 0.8, escopeta 0.3, revólver 0.3, fusil de asalto 0.2.
La curación esperada de toda la partida sigue por debajo del límite que vigila `LootTests` (< 520 PV); por eso los pesos de botiquines de los drops bajaron un poco al subir las probabilidades. `ZombieDropTests` simula 20 000 muertes por tipo y falla si alguna tasa se sale ±1.5 puntos de la tabla de arriba o si las armas pasan del 12 % de los drops.
