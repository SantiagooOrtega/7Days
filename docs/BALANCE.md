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
