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
| sniper | 90 | sin caída (alcance 400 m) | 100 % | 0.80 | 1.3 | 2.0 |
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

## Manejo de armas: retroceso, dispersión y apuntado (`WeaponCatalog.WithHandling`)
Los valores van en el ScriptableObject de cada arma (`Resources/Weapons/*.asset`). Los assets de una versión anterior (`dataVersion` < 2) se actualizan solos al abrir el editor.

| Arma | Modo(s) | Dispersión cadera / apuntando / máx. (°) | Retroceso vertical / lateral (°) | Recuperación (°/s) | FOV apuntando | Sens. apuntando |
|------|---------|------------------------------------------|----------------------------------|--------------------|---------------|-----------------|
| pistol | semi | 1.4 / 0.25 / 5 | 1.2 / 0.35 | 10 | 46 | x0.65 |
| revolver | semi | 1.8 / 0.2 / 6 | 2.6 / 0.6 | 7 | 42 | x0.6 |
| smg | auto, ráfaga | 3.2 / 1.2 / 7 | 0.5 / 0.45 | 12 | 48 | x0.7 |
| shotgun | bombeo | 6 / 4.2 / 8 | 3.4 / 0.8 | 6 | 50 | x0.7 |
| assault_rifle | auto, ráfaga, semi | 2.2 / 0.4 / 5.5 | 0.8 / 0.3 | 10 | 40 | x0.55 |
| sniper | cerrojo (mira x3 / x5 / x8 / x12) | 3.5 / 0.03 / 8 | 3.8 / 0.4 | 5 | 20 / 12 / 7.5 / 5 | x0.35 (y menor con el zoom) |

- El retroceso acumulado se limita a `recoilMaxPitch` y la desviación lateral a la mitad de ese valor; ×0.6 (o el `recoilAimMultiplier` del arma) apuntando y ×0.8 agachado. Al soltar el gatillo vuelve a la mira original.
- La dispersión crece `spreadPerShot` por disparo (la mitad apuntando), `spreadMove` corriendo, `spreadAir` en el aire, ×0.75 agachado; nunca pasa de `spreadMax` y se recupera a `spreadRecovery` °/s.

### Precisión real (probabilidad de acertar la primera bala; reparto uniforme en el cono, que es lo que hace el juego)
| Arma | Cabeza a 20 m (cadera / apuntando) | Cuerpo a 20 m (cadera / apuntando) | Cuerpo a 20 m tras 10 disparos sin apuntar |
|------|------------------------------------|------------------------------------|--------------------------------------------|
| pistol | 15 % / 100 % | 31 % / 100 % | 2 % |
| revolver | 9 % / 100 % | 18 % / 100 % | 2 % |
| smg | 3 % / 21 % | 6 % / 42 % | 2 % |
| shotgun (por perdigón) | 1 % / 2 % | 2 % / 3 % | 1 % |
| assault_rifle | 6 % / 100 % | 12 % / 100 % | 4 % |
| sniper | 2 % / 100 % | 5 % / 100 % | 1 % |

**TTK tras el cambio de precisión:** el daño por bala no cambió, así que la tabla de disparos para matar de arriba sigue igual (las pruebas `BalanceTests` siguen en verde). Esa tabla supone que las balas aciertan; `AccuracyBalanceTests` comprueba que eso se cumple apuntando a la distancia típica de cada arma (pistola a 15 m a la cabeza, fusil a 25 m al cuerpo, francotirador a 100 m a la cabeza: > 95 %; escopeta a 3 m al cuerpo: > 80 % de los perdigones). Sin apuntar la precisión cae mucho a 20 m: apuntar es la forma de alcanzar esos TTK, y disparar desde la cadera es para distancias cortas.

### Francotirador: más alcance (datos v3)
- Alcance 260 → **400 m** y sin caída de daño en todo el alcance: 90 de daño a cualquier distancia. TTK sin cambios: **1 disparo a la cabeza** (293) y 2 al cuerpo en cualquier punto hasta 400 m (`BalanceTests` lo comprueba a 5, 60, 130, 260 y 400 m).
- Zoom x3 / x5 / x8 / x12 (FOV 20 / 12 / 7.5 / 5) con la rueda mientras apuntas; la sensibilidad baja en proporción al zoom.
- Vaivén de la mira: `scopeSway` (0.35°) x `swayAmount` del arma (1.4) ≈ ±0.5°, más con fatiga. **Shift** contiene la respiración 3 s (vaivén x0.15); al agotarse, jadeo 2.5 s con vaivén x2. Soltar antes conserva el aire que queda.
- Precisión apuntando (0.03°) sin vaivén: a 400 m el cono mide 0.21 m de radio, del tamaño de una cabeza: conteniendo la respiración el disparo a la cabeza es fiable; jadeando, no.
- Los assets de armas de la versión 2 se actualizan solos a la 3 conservando lo editado a mano (solo cambian alcance, caída y niveles de zoom del francotirador).

## Recarga por eventos y golpe cargado (Bloque 3 de ARTE_PROCEDURAL.md)
- **La munición entra en el momento real de la animación**, no al final: cargador (pistola, subfusil, fusil) al 65 % del `reloadTime` (cargador nuevo); revólver al 62 % (meter las balas); francotirador al 62 % (cargador nuevo, con el cerrojo abierto entre el 12 y el 85 %). Cancelar antes (cambiar de arma) no da nada. La duración total no cambia.
- **Escopeta cartucho a cartucho**: 0.25 s de inicio + un cartucho cada (3.2 − 0.55) / 6 ≈ 0.44 s + 0.3 s final con bombeo. Solo carga los que faltan (con 2 que faltan tarda ~1.4 s en vez de 3.2). Si disparas durante la recarga con algún cartucho ya dentro, deja de recargar y dispara; los cartuchos metidos se quedan.
- **Cuerpo a cuerpo**: el daño se aplica en el impacto de la animación (0.14 s tras el clic en el golpe normal), no al pulsar. **Golpe cargado**: mantén el clic 0.45 s y suelta: daño x1.75 (barra: 38 → 66.5, dos golpes al cuerpo de un zombi común en vez de tres), empuje x1.5, impacto a 0.31 s y espera x1.6 antes del siguiente. Golpes seguidos alternan el lado (combo).

## Variantes jugables (`Procedural/CharacterVariant.cs`, ScriptableObject en `Resources/Config/Variants`)
La Calavera es la referencia; las demás cambian poco para no romper el balance de los días (el director no las distingue).
| Variante | Vida | Velocidad | Curación (botiquín/venda) | Balanceo del arma |
|----------|------|-----------|---------------------------|-------------------|
| Calavera | 100 | ×1.00 | ×1.00 | ×1.00 |
| Explorador | 90 | ×1.05 | ×1.00 | ×1.00 |
| Médico | 100 | ×1.00 | ×1.30 | ×1.00 |
| Francotirador | 95 | ×1.00 | ×1.00 | ×0.80 |
Rangos válidos (Validate): vida 60-140, velocidad 0.85-1.15, curación 0.5-1.6, balanceo 0.5-1.5. Cambiar de variante en F6 rellena la vida al nuevo máximo.


## Hitboxes del cuerpo procedural de los zombis
Con el cuerpo procedural (`ZOMBIS_SKINS.md`, bloque C) los colliders de cabeza, torso, brazos y piernas se ajustan a los huesos y a las proporciones de cada tipo (el tanque mide 2.64 m y tiene un brazo derecho
1.55 veces más grueso; el corredor es delgado). **Los multiplicadores por zona, las resistencias y la tabla de este documento no cambian**: `ZoneMultipliers`, `ZombieCatalog.Defaults()` y los tiempos de matar por arma y
tipo salen de `Procedural/CombatModel.cs`, que no se tocó, y `BalanceTests` / `CombatTests` pasan sin modificarse. Lo único que cambia es DÓNDE hay que apuntar: los colliders siguen la animación hueso a hueso y cubren
entre el 88 % (tanque) y el 100 % (corredor) de la malla a menos de 5 cm (18 colliders por zombi: cabeza, cuello, torso, pelvis, hombros, brazos, manos, muslos, pantorrillas y pies, más el vientre del explosivo) (`ZombieBodyTests.Hitboxes_FollowTheBones_AndCoverTheBody`).

### Jefes (bloque D)
Los tres jefes usan ahora el mismo cuerpo procedural (miden 1.82 m x su escala: Bruto 4.0 m, Lanzador Colosal 3.64 m, El Devorador 4.37 m, igual que la cápsula de movimiento). Su vida, resistencias,
multiplicadores por zona, habilidades y tiempos **no cambian** (`ZombieCatalog.Defaults()`, `BossTests`). Los colliders siguen a los huesos y cubren también el equipo que llevan encima (placas y cadenas del Bruto
cuentan como torso, sus hombreras como brazo; el depósito y las bolsas del Lanzador como torso): 89-92 % de la malla a menos de 6-7 cm (la misma proporción que 5 cm en un zombi de 1.8 m). El **punto débil** del
Devorador (x4) es una esfera en el núcleo del pecho del mismo tamaño que antes (0.16 m x 2.4 = 0.38 m) y solo existe mientras está expuesto; un disparo de frente al núcleo le da antes que al torso
(`ZombieBodyTests.Devourer_WeakPoint_OnlyWhileExposed_RibsOpen_AndItIsHitFromTheFront`).
