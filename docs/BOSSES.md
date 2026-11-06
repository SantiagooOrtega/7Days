# Jefes finales (días 3, 5 y 7)

Datos en `ZombieCatalog.Defaults()` (`isBoss = true`, bloque `boss`), editables por JSON (`docs/ZOMBIES.md`, esquema zod incluido). Decisiones en `Procedural/BossBrain.cs` (puro, probado en `BossTests`); la ejecución en Unity está en `Combat/BossBehaviour.cs` y la barra grande en `Combat/BossHud.cs`.

| Día | Jefe | PV (x común) | Habilidades | Rasgo especial |
|-----|------|--------------|-------------|----------------|
| 3 | Bruto Titán | 1500 (15x) | carga en línea recta, golpe en área | la carga rompe coberturas ligeras; si choca con un muro o una cobertura pesada queda **aturdido 2.5 s** y recibe **x1.5** de daño |
| 5 | Lanzador Colosal | 2400 (24x) | lanza un objeto grande en arco, invoca corredores, golpe en área | el objeto **destruye las coberturas** del radio de impacto y deja una marca roja en el suelo 1–1.5 s antes |
| 7 | El Devorador | 3000 (30x) | onda expansiva (se esquiva **saltando**), oleadas, golpe en área | **punto débil** (núcleo naranja en el pecho, x4) que solo se expone 4 s cada 14 s y tras la onda expansiva |

Reglas comunes
- **Furia:** al bajar del 50 % de vida y del 25 % cambian de color y de tamaño, rugen, se mueven más rápido (x1.25 y x1.5), atacan más a menudo (x1.3 y x1.6) y acortan un poco los avisos (nunca menos de 0.5 s). La furia no retrocede.
- **Telegraph de cada ataque:** los brazos se alzan, aparece una marca roja en el suelo (franja para la carga, círculo para el golpe/onda/oleada, círculo bajo el proyectil), suena un tono de aviso y la barra grande muestra el nombre del ataque ("¡CARGA!", "¡ONDA EXPANSIVA! ¡SALTA!").
- **Resistencia al aturdimiento** 0.9–0.95: los disparos casi no lo frenan ni empujan. Solo el choque contra un muro lo aturde.
- **Botín:** siempre suelta un objeto raro o mejor (`dropKind = "boss"`, `guaranteed`, `minRarity` raro), **fuera del tope diario** de drops (`LootManager.DropBoss`).
- **Barra:** grande y fija arriba, con el nombre, marcas en 50 % y 25 %, y parpadeo rojo en furia. El jefe no muestra la barra pequeña sobre la cabeza.
- **Coberturas:** todas las `CoverObject` son `Destructible` (fuerza 0 las ligeras, 1 las grandes). La carga rompe las de fuerza 0; los lanzamientos y la onda rompen las que alcanzan según su fuerza; se reponen entre oleadas (`Destructible.RestoreAll()`, Shift+B en pruebas) y el NavMesh se actualiza al romperse una.

Habilidades (`BossAbilityData`): `a` y `b` dependen de la habilidad.
| id | a | b |
|----|---|---|
| charge | velocidad de la carga (m/s) | multiplicador de `attackDamage` |
| slam | radio (m) | daño |
| throw | radio de impacto (m) | daño |
| shockwave | radio (m) | daño |
| summon / summon_wave | - | - (cantidad en `boss.summonCount`, tipo en `boss.summonType`) |
