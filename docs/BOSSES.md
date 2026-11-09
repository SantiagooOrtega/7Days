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

## Aspecto y animación (bloque D de ZOMBIS_SKINS.md)
Los jefes ya no son de primitivas: usan el cuerpo procedural de los zombis (`Combat/ZombieBody.cs`) con anatomía, piezas y texturas propias (`Procedural/ZombieLook.cs`, `ZombieBossFeatures.cs`, `ZombieTextures.cs`).

| Jefe | Altura | Triángulos (LOD 0 / 1 / 2) | Historia visual |
|------|--------|----------------------------|-----------------|
| Bruto Titán | 4.0 m | 9 712 / 5 286 / 2 496 | hombros enormes; armadura soldada (placas de chapa con cordones de soldadura, hombreras con varillas de obra); cadenas en bandolera y a la cintura; casco de obra amarillo incrustado y agrietado; antebrazos y puños como mazas con placa y pernos; brazos al aire con quemaduras; ojos rojos |
| Lanzador Colosal | 3.64 m | 9 316 / 4 884 / 2 592 | torso hipertrofiado; brazo derecho gigante con vendajes y dos neumáticos; espalda con bolsas y un depósito de residuos atado con correas (franja de aviso); pústulas verdosas que brillan; piel verdosa con granos |
| El Devorador | 4.37 m | 7 910 / 3 970 / 2 350 | alto y deformado (brazos y piernas largos); mandíbula doble que se abre hacia los lados con colmillos; columna expuesta con púas; bultos mutados; garras; músculo a la vista; **núcleo naranja en el pecho tras unas costillas que se abren como puertas** cuando el punto débil se expone |

- **Texturas de 1024 px** (la piel; ropa y heridas son las comunes de 512), generadas en hilos de fondo con color, normales, rugosidad, oclusión y emisión (ojos, venas, pústulas, núcleo).
- **Telegraph con animación propia** (`ZombieMotion.Boss`): preparar la carga (cabeza baja, se encoge, escarba) y embestir; golpe de suelo (puños arriba, tiembla, cae); lanzar (el brazo grande atrás y arriba, suelta delante); rugir para invocar (se arquea con los brazos abiertos y la boca abierta); onda expansiva (se agacha cruzando los brazos y estalla con un pisotón). La pose avanza con el telegraph del cerebro y **el golpe cae exactamente cuando se dispara la habilidad** (t = 0.5): las marcas del suelo, el texto de la barra y el sonido de aviso siguen igual y sincronizados (`BossBodyTests`: desfase medido 0.000).
- **Furia** (50 % y 25 %): rugido con los brazos abiertos, ojos y venas que brillan (x2.8 y x4.6), pose más encorvada y vapor que sale de la espalda. El cuerpo antiguo de respaldo conserva el cambio de color y tamaño.
- **Reacción al daño contenida**: los impactos mueven al jefe un 25-30 % de lo que mueven a un zombi (su resistencia al aturdimiento); el choque contra un muro sigue dejándolo aturdido con su pose de aturdido.
- **Voz**: rugido, gruñidos, pisadas pesadas con temblor de cámara, dolor y muerte con la voz sintetizada de cada jefe (más grave que el tanque).
- **Muerte**: cae tumbado ajustado al suelo con la malla completa; el Lanzador nunca cae de espaldas (llevaría el depósito debajo).
- Prueba en Play: **B** genera los jefes en orden (día 3, 5 y 7) a 20 m. Capturas: `ZombieShowcase.Bosses_FrontSide_Fury_AndTelegraphs` (PlayMode, a mano).
