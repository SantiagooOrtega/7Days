# Unity: configuración y cómo probar (Fase 2)

Versiones confirmadas en `game/Packages/manifest.json`: Unity 6000.3.28f1, URP 17.3.0,
Input System 1.20.1 (modo "solo Input System nuevo"), AI Navigation 2.0.16 y Cinemachine 3.1.7
(agregado en la Fase 2; trae `splines` como dependencia).

## Pasos en el editor (una sola vez)
1. Abre `game` en Unity Hub. Espera a que termine de importar Cinemachine (barra de progreso abajo a la derecha).
   Revisa la **Console** (Window > General > Console): no debe haber errores rojos. Si los hay, cópiamelos.
2. **Escena Game:** en la ventana Project abre `Assets/Scenes`, clic derecho sobre `SampleScene` > Rename > `Game`. Ábrela con doble clic.
3. En la ventana Hierarchy, clic derecho > Create Empty. Renómbralo `Arena`.
4. Con `Arena` seleccionado, en el Inspector pulsa Add Component, escribe `TestArena` y elígelo.
5. Deja `Main Camera` y `Directional Light` donde están (el script usa la cámara existente). Guarda con Cmd+S.
6. **Escena Login:** File > New Scene > elige **Basic (Built-in)** o **Standard (URP)** > Create. Guárdala como `Assets/Scenes/Login`.
7. En Hierarchy: Create Empty, nómbralo `LoginScreen`, Add Component > `LoginScreen`. Guarda con Cmd+S.
8. File > Build Profiles (o Build Settings) > Scene List: arrastra `Login` primero y `Game` después (Login debe ser el índice 0).

## Cómo probar
Backend (en una terminal):
```bash
cd "/Users/santiagoortegabolanos/Proyectos Final/7Days/backend"
npm run db:push && npm run dev      # http://localhost:3001
```
En Unity:
- **Flujo completo:** abre la escena `Login`, pulsa Play, crea una cuenta (Registrarme) y entrarás a la arena.
- **Atajo sin backend:** abre `Game` y pulsa Play, o usa el botón "[Debug] Jugar sin cuenta".

## Controles
| Acción | Tecla |
|--------|-------|
| Mover / correr / agacharse | WASD / Shift / Ctrl |
| Mirar | Ratón (Esc suelta el cursor; clic izquierdo lo recupera) |
| Apuntar / disparar | Clic derecho / clic izquierdo |
| Recargar / cambiar arma | R / teclas 1, 2 o rueda del ratón |
| Cobertura | C junto a una caja o muro bajo; A/D te deslizas; apunta para asomarte; C sale |
| Depuración | T recibes 20 de daño, Y te curas, F1 oculta la ayuda |

## Qué revisar al probar
1. Se mueve el jugador y la cámara va sobre el hombro derecho.
2. Clic derecho: la cámara se acerca y la mira se cierra.
3. Disparar a los blancos rojos: parpadean, mueren y reaparecen a los 4 s.
4. Munición baja en el HUD; recargar con R; cambiar a pistola con 2.
5. Acércate a una caja o muro bajo y pulsa C: te agachas pegado a él; A/D te desliza y se detiene en los bordes.
6. En cobertura, clic derecho te asoma para disparar por encima.

---

# Fase 2.5: Look & Feel

## Qué cambió (todo por código, no hay que tocar la escena)
Al dar Play en `Game` se aplica automáticamente:
- **Post-procesado URP** (Volume global creado por código): desaturado -38, contraste +28, viñeta, bloom suave (umbral 1.0), grano fino, tonemapping ACES y SMAA.
- **Niebla** exponencial gris-parda, fondo del color de la niebla, ambiente de tres colores y sol bajo de atardecer con sombras suaves.
- **Polvo flotante** que sigue al jugador (500 partículas máximo).
- **Mapa de ruinas industriales** con primitivas: calle de asfalto con líneas, contenedores, barreras, cajas, tuberías, nave semiderruida, escombros, 4 farolas y 2 barriles con fuego (luz parpadeante).
- **Efectos**: destello de boca de cañón, trazadoras e impactos brillantes (con bloom), contador de FPS en el HUD (verde ≥ 55).
- Todos los números del look están en `LookSettings` (`Scripts/Look/LookAndFeel.cs`) para ajustarlos mirando una captura.

Sobre la iluminación **baked**: no se puede hornear porque el mapa se construye en tiempo de ejecución (y en la Fase 3 los mapas vendrán de la base de datos, que también se construyen al jugar). En su lugar se usa 1 sola luz con sombras (el sol), luces puntuales sin sombras y ambiente barato, que cuesta poco en GPU.

## Perfil de calidad "Demo" (una sola vez, en el editor)
1. En la ventana Project abre `Assets/Settings`. Clic derecho sobre `PC_RPAsset` > Duplicate. Renombra la copia a `Demo_RPAsset`.
2. Selecciónala. En el Inspector:
   - Quality > **Anti Aliasing (MSAA)**: Disabled (usamos SMAA en la cámara).
   - Quality > **HDR**: marcado.
   - Quality > **Render Scale**: 1.
   - Lighting > **Additional Lights**: Per Pixel, **Cast Shadows** desmarcado, **Per Object Limit** 4.
   - Shadows > **Max Distance**: 45; **Cascade Count**: 2; **Soft Shadows**: marcado.
3. Edit > Project Settings > Quality. En la lista de niveles, selecciona **PC** y pulsa el botón de duplicar (o Add Quality Level). Nómbralo exactamente `Demo`.
4. En el nivel `Demo`, en **Render Pipeline Asset** arrastra `Demo_RPAsset`. Anti Aliasing: Disabled; VSync: Don't Sync.
5. En la barra de niveles, haz que `Demo` sea el nivel por defecto del Mac (clic en la flecha verde bajo el icono de Mac/Standalone, elige `Demo`).
6. Al dar Play, el juego busca ese nivel por nombre y lo activa; además limita a 60 FPS.

## Texturas procedurales (sin descargar nada)
- Se generan al dar Play: verás "Generando texturas..." menos de un segundo; el resto del tiempo no hay pausas porque se calculan en hilos de fondo. Un mensaje en la Console indica cuánto tardó.
- Materiales: asfalto (calle), muro de ladrillo y revoque (nave y pilares), concreto (suelo, muros del perímetro y barreras) y metal oxidado (contenedores, tuberías, barriles y farolas). Todos con mapa de normales.
- Para cambiar colores, óxido, grietas o suciedad: `Scripts/Look/LookAndFeel.cs`, clase `LookSettings`, campo `Surface` (`SurfaceParams` en `Scripts/Procedural/SurfaceGenerator.cs`). `Size` puede ser 512 (por defecto) o 1024.
- Archivos propios en `Resources/Look` tienen prioridad: ver `docs/ASSETS.md`.

## Pruebas EditMode
1. Window > General > Test Runner.
2. Pestaña **EditMode** > Run All. Deben pasar las 30 pruebas de `SevenDays.Tests.EditMode` (ruido tileable, costuras, paleta oscura, normales unitarias, óxido y grietas).

## Qué probar y la captura que necesito
1. Pulsa Play en `Game`. Mira el contador FPS arriba a la derecha: debe estar verde (≥ 55). Si no, dime el número.
2. Haz **3 capturas** del Game view (Cmd+Shift+4 en Mac, o el botón de captura de Unity) y pásamelas:
   a. Al inicio, mirando hacia el frente por la calle (para ver niebla, farolas y siluetas).
   b. Apuntando a un barril con fuego (para ver bloom y luces).
   c. Disparando (destello y trazadoras).
3. Dime también: ¿se ve demasiado oscuro, demasiado gris o demasiado borroso? Ajusto `LookSettings`.

---

## Ajuste de luz y "Brillo global" (Fase 2.5, 2ª pasada)
El look era demasiado oscuro. Cambios:
- **Brillo global:** `LookSettings.GlobalBrightness` (en `Scripts/Look/LookAndFeel.cs`). 1 = valores base, por defecto **1.25** (medio-alto); rango útil 0.5 a 2. Un solo número sube o baja de golpe el sol, el ambiente, la exposición, la niebla y la paleta de las texturas.
- **Luz:** sol más alto (52°, antes 28°) y de luz fría y difusa (tarde nublada), sombras más suaves (fuerza 0.78), ambiente azul-gris más claro. Exposición base +0.35 EV, que con el brillo por defecto queda en +0.65 EV.
- **Niebla y fondo:** gris azulado medio (antes casi negro) y densidad a la mitad (0.008).
- **Post-procesado:** contraste de 28 a 14, saturación de -38 a -30 (sigue desaturado), viñeta 0.22 (tope 0.25).
- **Texturas:** la paleta base es ~30% más clara y además escala con el brillo global. El asfalto sigue siendo lo más oscuro y la suciedad se mantiene.
- **Jugador legible:** cuerpo y casco claros, franja y mochila naranjas, más una luz de contorno fría (`RimLight`) delante del jugador. Los blancos de práctica tienen cuerpo rojo claro, cabeza pálida y ojos brillantes.
- **Cajas y contenedores:**
  - Caja de madera: marco, tablas con veta, riostra diagonal, tornillos, nudos, marcas estarcidas, bordes gastados y manchas.
  - Caja de cartón: cinta, solapa, etiqueta, flechas, manchas de agua y esquinas aplastadas.
  - Contenedor: chapa corrugada con óxido y chorreras, más marco de acero, barras de puerta y números estarcidos.
  - Cada objeto varía de tono y de posición de la textura, así que no se ven iguales.
- **UV por cara:** las cajas se construyen con `BoxMesh` (UV en metros por cara), por lo que la textura ya no se estira en muros, barreras y contenedores largos.

Si algo sigue oscuro o lavado, sube o baja `GlobalBrightness` (pasos de 0.1) antes de tocar otros valores.

---

## Bloque 1: texturas en esquinas y ángulos
- Las cajas se construyen con `BoxGeometry` (C# puro, con 12 pruebas): UV por cara en metros, sin caras espejadas, la "arriba" de la textura apunta a +Y (veta y líneas verticales en su sitio), tangentes definidas para los mapas de normales y **bisel** en las aristas (muros 5 cm, cajas 2.5 cm) que corta las costuras.
- Las cajas de madera y cartón usan wrap `Clamp` (no se repiten) y todas las texturas generadas piden filtrado anisotrópico 8x (se fuerza con `QualitySettings.anisotropicFiltering`).
- El desplazamiento aleatorio de textura del contenedor es solo horizontal, para que el óxido del borde superior no se mueva.
- Si alguna malla aún muestra estiramiento, dime cuál y paso ese material a mapeo triplanar (necesita un shader propio).

## Bloque 2: ambiente apocalíptico
Todo está en `LookSettings.Atmosphere` (`Scripts/Look/AtmosphereSettings.cs`). Un valor de 0 apaga el efecto y `Density` multiplica todo de golpe.

| Efecto | Parámetros |
|--------|-----------|
| Niebla baja pegada al suelo (nubes planas y verticales que se arrastran) | `GroundFogLayers`, `GroundFogDensity`, `GroundFogAlpha` |
| Bruma lejana entre edificios + niebla exponencial | `HazeBanks`, `LookSettings.FogDensity` |
| Cielo apagado | `SkyDim` |
| Ceniza flotando alrededor del jugador | `Ash` |
| Columnas de humo negro a lo lejos | `SmokeColumns`, `Smoke` |
| Chispas en barriles con fuego | `Sparks` |
| Rayos de luz falsos (partículas alargadas alineadas con el sol) | `GodRays`, `GodRayIntensity` |
| Farolas con parpadeo rojizo y apagones | `RedFlickerLamps` |
| Grietas, maleza, charcos y sangre seca | `Cracks`, `WeedTufts`, `Puddles`, `BloodStains` |
| Edificios en ruinas fuera del mapa | `SkylineBuildings` |

### 60 FPS: recorte automático
- El HUD muestra `FPS ... ambiente N/3`. Si el FPS se mantiene por debajo de **55** durante 2.5 s (tras 6 s de calentamiento), el juego sube un nivel de recorte: 1 = 60% de partículas, 2 = 30% y sin bruma, rayos ni capas extra de niebla, 3 = solo lo esencial. Cada recorte queda en la Console.
- **F3** cambia el nivel a mano (para comparar). Nunca sube solo.
- Si en tu Mac baja de 55 con el ambiente completo, baja `Atmosphere.Density` (por ejemplo 0.7) antes de tocar otra cosa.

### Si haces un Build
Las partículas usan el shader `Universal Render Pipeline/Particles/Unlit`, que se busca por nombre. Para que entre en un Build: Edit > Project Settings > Graphics > Always Included Shaders, aumenta el tamaño de la lista y arrastra ese shader (está en el paquete Universal RP > Shaders > Particles). En el editor (Play) no hace falta.

---

## Bloque 3: vegetación y relleno
- **Árboles muertos** (tronco + ramas, 4 variantes, escala y giro aleatorios), **arbustos secos**, **hierba y maleza**, **neumáticos y bolsas**: mallas generadas por código y dibujadas con **GPU instancing** (una llamada de dibujo por tipo), sin sombras (los árboles pueden tenerlas con `Vegetation.TreeShadows`).
- **Anillo de árboles** fuera de los muros (85% de los árboles) que tapa el borde del mapa; arbustos y hierba en la base de los muros, los bordes de la calle y alrededor de coberturas.
- **Props de abandono**: coches destrozados (cobertura), postes caídos, carteles rotos.
- **Textura de corteza** procedural (`bark`), con la misma regla de prioridad: un `bark_diff.jpg` en `Resources/Look` la reemplaza.
- **Parámetros** en `LookSettings.Vegetation`: `Density`, `MaxTrees`, `MaxBushes`, `MaxGrass`, `MaxProps`, `TreeShadows`, `Seed`. Si bajas de 55 FPS, baja primero `Density`.
- **Por JSON** (posición, tipo, escala): ver `docs/PROP_LAYOUT.md`. Menú **7Days > Exportar layout de props (JSON)**.
- **NavMesh**: árboles y arbustos grandes son obstáculos; la hierba no.

---

## Bloque 4: señales de tráfico desgastadas
- **6 tipos genéricos** (sin logos ni textos protegidos): pare (octágono con bloques abstractos en vez de letras), ceda el paso, velocidad máxima ("40"), prohibido girar, peligro (triángulo con exclamación) y letrero genérico (rectángulo azul con flecha).
- **Textura procedural por señal** (color + mapa de normales, 256 px) en **3 niveles de desgaste** (leve, medio, destrozado): pintura descascarada que deja ver el acero, óxido en bordes y alrededor de los tornillos, abolladuras (relieve en normales), suciedad con chorreras, rayones y agujeros de bala. Solo se generan las combinaciones que usa el mapa, en hilos de fondo.
- **6 estados**: erguida, inclinada, doblada, caída, solo poste y placa colgando de un tornillo.
- **Colocación**: a lo largo de la calle, en las esquinas y en los extremos de la calle; ~35% quedan semitapadas por hierba, bolsas o neumáticos. Posición, tipo, estado, variante y escala van en el mismo JSON de props (ver `docs/PROP_LAYOUT.md`).
- **Rendimiento**: placas y postes se dibujan con **GPU instancing** y sin sombras; los postes no tienen collider (no bloquean el NavMesh) y solo la señal caída lleva un collider pequeño "no caminable".
- **2 señales** llevan una luz roja parpadeante (`LookSettings.Signs.RedLightSigns`).
- **Marcas viales** gastadas sobre el asfalto: línea amarilla discontinua, 2 pasos de cebra y 3 flechas casi borradas (`Signs.RoadMarkings`).
- **Parámetros** en `LookSettings.Signs`: `Density`, `MaxSigns` (tope), `RedLightSigns`, `TextureSize`, `Seed`, `RoadMarkings`.

---

## Bloque 5: salto y vault

### Controles nuevos
| Acción | Cómo |
|--------|------|
| Saltar | **Espacio** (≈ 1.0 m, ágil: sube normal y cae con más fuerza) |
| Pasar un obstáculo bajo (0.5 a 1.3 m) | Correr hacia él (Shift+W) o pulsar Espacio junto a él: lo salta solo |
| Trepar un obstáculo de 1.3 a 1.6 m (techo de coche) | Pulsar Espacio junto a él: trepada lenta (no se activa corriendo) |
| Más de 1.6 m (contenedores, muros) | No se puede pasar |
| Saltar desde la cobertura | Con C en cobertura, Espacio salta el obstáculo hacia el lado contrario |
| Probar la caída | **U** sube 6 m al jugador y lo suelta |

### Cómo funciona
- **Salto ágil:** coyote time (0.1 s: aún puedes saltar justo después de salir de un borde), jump buffer (0.1 s: un salto pulsado justo antes de aterrizar se recuerda) y control aéreo limitado (35%).
- **Vault / mantle automático:** se busca un obstáculo marcado `Vaultable` delante (SphereCast), se mide su altura con un raycast hacia abajo y se comprueba que haya espacio libre arriba y donde se aterriza. Obstáculos delgados se pasan de largo; los gruesos se suben encima. No se puede disparar durante el vault.
- **Quién es Vaultable:** todas las coberturas (`CoverObject` trae un `Vaultable`): cajas, barreras, coches, etc. La altura decide: los contenedores (2.6 m) no se pueden pasar. Para marcar un objeto propio: añade el componente **Vaultable** a un objeto con BoxCollider.
- **Daño por caída:** solo desde más de 4 m (18 de daño por cada metro de más). La cámara recibe un impulso (Cinemachine Impulse) al saltar y al aterrizar, más fuerte si la caída es alta.
- **Zombies:** `Vaultable.AllowZombies` (falso por ahora) deja el flag listo para decidirlo en la Fase 3.

### Valores configurables
Todos están en `MovementSettings` (`Scripts/Procedural/Movement.cs`). Para ajustarlos sin tocar código:
1. En la ventana Project, ve a `Assets/_Project/Resources/Config` (si no existe, créala: clic derecho > Create > Folder).
2. Clic derecho > Create > **7Days > Movimiento del jugador**. Nómbralo exactamente `Movement`.
3. Selecciónalo y edita en el Inspector. Sin este asset se usan los valores por defecto.

| Valor | Por defecto |
|-------|-------------|
| `JumpHeight` | 1.0 m |
| `Gravity` / `FallGravityMultiplier` | 22 / 1.8 |
| `CoyoteTime` / `JumpBufferTime` | 0.1 s / 0.1 s |
| `AirControl` | 0.35 |
| `VaultMinHeight` / `VaultMaxHeight` / `MantleMaxHeight` | 0.5 / 1.3 / 1.6 m |
| `AllowMantle` | sí (apágalo para que 1.3 a 1.6 m tampoco se pueda) |
| `VaultDuration` / `MantleDuration` | 0.55 s / 1.1 s |
| `VaultReach` / `VaultOverMaxDepth` | 0.9 m / 1.6 m |
| `FallDamageThreshold` / `FallDamagePerMeter` | 4 m / 18 |
| `JumpImpulse` / `LandImpulseMin` / `LandImpulseMax` | 0.08 / 0.15 / 0.9 |

### Hooks del Animator (Fase 3)
`PlayerAnimatorHooks` deja listos estos parámetros: **bool `IsGrounded`**, **bool `IsJumping`**, **bool `IsVaulting`**, **float `VerticalSpeed`**. Cuando haya modelo, asigna su `Animator` al campo `Animator` del componente (si el controlador tiene esos parámetros, se actualizan solos).

### Cómo probarlo (pasos numerados)
1. Pulsa Play en `Game` y espera a que desaparezca "Generando texturas...".
2. Pulsa **Espacio** en campo abierto: salto de ~1 m, rápido, con un pequeño golpe de cámara.
3. Camina hasta el borde de la barrera de concreto del centro y corre hacia ella (Shift+W): debe pasarla sola.
4. Párate junto a una caja de madera y pulsa Espacio: la salta.
5. Acércate a un coche destrozado a los lados de la calle y pulsa Espacio junto a su techo: trepada lenta.
6. Acércate a un contenedor y pulsa Espacio: no pasa (2.6 m).
7. Pulsa **C** junto a una barrera para cubrirte y luego **Espacio**: sales al otro lado. Intenta disparar durante el salto: no dispara.
8. Pulsa **U**: caes 6 m, recibes daño y la cámara se sacude. Repite varias veces con **Y** para curarte.

---

## Corrección: objetos transparentes (caras al revés)
**Síntoma:** contenedores, muros, barreras y otras piezas parecían transparentes desde ciertos ángulos.

**Causa (error mío):** todas las mallas generadas por código tenían el orden de vértices (winding) al revés. En Unity el *frente* de un
triángulo es el lado desde el que sus vértices se ven en sentido horario, y su normal es `+Cross(b - a, c - a)`; yo había usado el signo
contrario y mis pruebas usaban la misma convención equivocada, así que pasaban. Con el backface culling, desde fuera se descartaban las
caras de delante y se veía el interior de las del fondo. Dos fallos más con el mismo origen: las UV salían **espejadas** y las tangentes
tenían `w = +1` en vez de `-1`, con lo que los mapas de normales se iluminaban al revés.

**Qué se arregló**
- `Winding` (en `Scripts/Procedural/Winding.cs`) guarda la convención de Unity en un solo sitio, con una prueba basada en el ejemplo del
  manual de Unity (el quad con normal -Z). Cajas, muros, contenedores, barreras, coches, placas de señal, árboles, arbustos, neumáticos y
  bolsas la usan.
- Las caras de las cajas llevan `u` hacia la derecha de quien las mira y `v` hacia arriba (veta de la madera y costillas del contenedor
  en su sitio); la textura de las placas de señal ya no sale espejada.
- Prueba EditMode (`WindingTests`) en todas las mallas cerradas: volumen con signo positivo, cada triángulo mira hacia afuera del centro de
  su malla y coincide con su normal, y las tangentes tienen la lateralidad de Unity. Con el código antiguo 13 de esas pruebas fallan.

### Culling de cada material (`_Cull` de URP Lit)
| Objeto | Malla | Cull | Normales |
|--------|-------|------|----------|
| Cajas, muros, contenedores, barreras, coches, techos, skyline, neumáticos, bolsas | sólido cerrado con grosor | **Back** | hacia afuera |
| Placas de señal | sólido cerrado de 2 cm: frente, dorso y cantos | **Off** (Both) | frente +Z, dorso -Z (volteada) |
| Postes de señal, árboles, arbustos | sólido | **Off** (Both) | hacia afuera |
| Hierba y maleza | hoja de **una** capa | **Off** (Both) | **hacia arriba** en las dos caras |
| Calcomanías (sangre, grietas, charcos, marcas) | quad plano visto desde arriba | Back | hacia arriba |
| Partículas | billboards | Off | (sin iluminación) |

- **URP Lit no voltea la normal de la cara trasera** cuando el Cull es Off. Por eso las láminas de una capa llevan una normal que vale para las
  dos caras (hacia arriba en la hierba), y las placas tienen grosor real con un dorso propio de normal volteada.
- **Sombras:** la propiedad `_Cull` del material se aplica también a ShadowCaster, DepthOnly y DepthNormals, así que la sombra usa las mismas caras
  que el color (y los materiales Cull Off proyectan sombra por las dos caras). Los materiales se crean con `_Cull` explícito en `Prim`.

### Excepción NullReferenceException en `FlickerLight.Update`
**Causa:** `FlickerLight` creaba su `MaterialPropertyBlock` en `Awake` solo si `Bulb` ya estaba asignado, pero `Bulb` se asigna **después** de
`AddComponent` (cuando `Awake` ya corrió). Entonces `_mpb` seguía en `null` y `Update` fallaba en cada frame al usarlo (153 veces en el log; afectaba a las
farolas defectuosas y a las luces rojas de las señales). **Arreglo:** se crea la primera vez que hace falta.

---

# Bloque 1 (ronda 3): piso nuevo

La autopista no se tocó (el asfalto, las líneas y las marcas siguen igual). Cambió el suelo **fuera** de ella.

## Cómo está hecho (sin shader propio)
- **Un mapa de mezcla de 60 x 60 m** generado por código (`GroundGenerator`, `Scripts/Procedural`), que **no se repite**: tierra seca agrietada, grava, ceniza, charcos de barro, parches de hierba seca y restos de concreto roto.
- **Zonas grandes** (≈ 15 m) por ruido de baja frecuencia con los bordes deformados (no es ruido uniforme) y **más escombro, grava y arena junto al borde de la vía**.
- **Relieve** en el mapa de normales: grietas de la tierra, piedrecitas de la grava, losas de concreto a distinta altura, **rodadas** de vehículos a los lados de la vía y **huellas** de pasos.
- **Brillo húmedo del barro:** la suavidad va en el canal alfa del color (`_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` de URP Lit), así solo el barro refleja las luces.
- **Dos escalas de tile:** además del mapa grande, una textura de detalle (grano de tierra y piedrecitas, 256 px) que se repite cada 1.2 m con el modo *Detail x2* de URP Lit (`_DETAIL_MULX2`). Se comprobó en el código fuente de URP 17.3 que esas propiedades y keywords existen.
- **Calcomanías** de manchas oscuras (aceite/hollín) y quemaduras, además de las grietas, charcos y sangre seca que ya había.
- Se genera en hilos de fondo (tiras de filas en paralelo) a la vez que las demás texturas; en la Console sale `Piso listo (... ms)`.

## Parámetros (`LookSettings.Ground`, en `Scripts/Look/GroundSettings.cs`)
| Parámetro | Por defecto | Qué hace |
|-----------|-------------|----------|
| `Enabled` | sí | apagado = vuelve el concreto genérico |
| `MapSize` | 1024 | 1024 (≈ 6 cm por píxel) o 2048 (más nítido, 4x memoria y carga) |
| `ZoneScale` | 1 | tamaño de las zonas (menor = más grandes) |
| `Earth`, `Gravel`, `Ash`, `Mud`, `DryGrass`, `Concrete` | 1 | abundancia de cada capa (0 = nada, 2 = el doble) |
| `RoadDebris` | 1 | escombro y arena junto a la vía |
| `Cracks`, `Pebbles`, `Ruts`, `Footprints` | 1 | cantidad de grietas, piedrecitas, rodadas y huellas |
| `DetailTileMeters`, `DetailStrength` | 1.2 / 1 | escala e intensidad de la textura de detalle |
| `NormalStrength`, `Brightness` | 1.6 / 1.35 | relieve y brillo del piso (el Brillo global también lo afecta) |
| `StainDecals`, `BurnDecals` | 10 / 6 | manchas y quemaduras |

Si baja de 55 FPS, lo primero es `Atmosphere.Density`; el piso casi no cuesta (una textura y un material). Con `MapSize = 2048` la carga tarda más.

---

# Bloque 2 (ronda 3): casa central

Detalle completo, formato JSON y esquema zod en `docs/HOUSE_LAYOUT.md`.

- Se construye por código con las mismas mallas y texturas del mapa: muros de ladrillo con bisel y UV en metros, **techo de 25 cm de grosor** (sin backface culling), marcos, muebles con textura de madera.
- **3 entradas** (puerta principal, puerta trasera y **ventana grande que se salta con Espacio**), 4 ventanas con línea de tiro, 2 habitaciones, techo parcialmente caído y muebles que son cobertura y se saltan.
- Colocación, aberturas, muebles y antorchas **por JSON**; el director bloquea entradas por día (`HouseController.SetDay`).
- NavMesh: puertas = volúmenes transitables, ventana grande = enlace, muros "no caminables", techo ignorado.
- Para hacerle sitio en el lado este de la autopista se quitaron 2 cajas y se movió el contenedor azul 3.7 m al este.

### Cómo probar (pasos numerados)
1. Pulsa Play en `Game`. La casa está al este de la calle, a la altura del centro del mapa.
2. Entra por la puerta principal (mira a la autopista). Mira el techo: está parcialmente caído y entra luz.
3. Cruza el paso entre las dos habitaciones.
4. Sal por la puerta trasera (lado este) y rodea la casa hasta la ventana grande (lado sur): corre hacia ella o pulsa Espacio y la saltas hacia adentro.
5. Párate junto a una ventana pequeña, apunta y dispara hacia afuera: debe haber línea de tiro. Dispara a un muro: la bala no lo atraviesa.
6. Prueba la cobertura (C) y el vault (Espacio) con las cajas, la mesa, el sofá y la cama; el estante y el armario no se saltan.

---

# Bloque 3 (ronda 3): fuego y puntos de interés

## Fuentes de fuego
| Fuente | Dónde | Quema | Notas |
|--------|-------|-------|-------|
| Barril con fuego | 2 barriles del mapa | radio 0.7 m | |
| Fogata entre escombros | 5 por defecto, junto a coberturas y contenedores, lejos de la calle (`campfire` en el JSON de props) | radio 0.9 m | aro de piedras, troncos cruzados y ceniza |
| Coche incendiado | 2 de los coches destrozados (`state: "burning"`) | radio 2.0 m | llamas grandes, mucho humo, pintura ennegrecida |
| Antorcha | 4 en la casa (3 dentro, 1 junto a la puerta principal) | no quema | llama pequeña con luz |

Cada fuego es un `FireEffect` con **llama, humo, chispas, luz puntual parpadeante sin sombras y mancha de quemadura** en el suelo, y sale de un **pool** (`FireManager`): se puede apagar y reutilizar (por ejemplo el director los apaga al cambiar de día con `FireManager.Despawn`).

## Topes y rendimiento
- **`LookSettings.Fire.MaxFires`** (16): tope de fuegos activos.
- **`LookSettings.Fire.MaxFireLights`** (6): solo las luces de los fuegos **más cercanos al jugador** quedan encendidas (a igual distancia gana el coche, luego fogata/barril, luego antorcha). Ninguna proyecta sombras.
- **Gobernador de ambiente** (FPS < 55, F3 lo cambia a mano): nivel 1 = 60% de humo y 4 luces; nivel 2 = sin chispas, 30% de humo y 2 luces; nivel 3 = sin humo, 1 luz y la llama al mínimo (15%).

## Daño por quemadura (configurable)
Si el jugador se queda dentro del radio de un fuego recibe `Fire.BurnDps` de daño por segundo (4 por defecto, en ticks de `BurnTickSeconds` = 0.25 s) y el HUD muestra "¡TE QUEMAS!". `BurnDps = 0` lo desactiva. Saltando por encima (más de 2.2 m) no quema.

## Otros parámetros (`LookSettings.Fire`)
`Enabled`, `SmokeAmount`, `SparkAmount`, `FlameScale`, `LightIntensity`.

## Cómo probarlo (pasos numerados)
1. Pulsa Play en `Game`. Camina de noche hacia un barril con fuego: se ve la llama, el humo y las chispas, y el suelo cerca está iluminado y parpadea.
2. Acércate a una fogata (junto a las barreras y contenedores) y a un coche incendiado (a los lados de la calle).
3. Párate dentro del radio de una fogata 3 segundos: pierdes ~12 de vida y sale "¡TE QUEMAS!". Sal del fuego y se detiene.
4. Entra en la casa: las antorchas iluminan el interior sin quemar.
5. Pulsa **F3** varias veces: el ambiente se recorta (menos humo, chispas y luces); el HUD muestra `ambiente N/3`.

---

# Bloque 4 (ronda 3): vegetación y colisiones

## Más vegetación (sin que cueste FPS)
- Valores nuevos por defecto: 54 arbustos base (tope 100), 280 matas de hierba base (tope 420), 70 matas de maleza fusionadas y **12 arbustos + 50 matas extra agrupados alrededor de la casa** (a 1.6-4.5 m de su borde, dejando libres las entradas).
- La maleza y la sangre también se concentran junto a la casa, las vías y los escombros.
- Todo con GPU instancing o mallas fusionadas, sin sombras y con tope. Parámetros: `LookSettings.Vegetation` (`Density`, `MaxBushes`, `MaxGrass`, `HouseBushes`, `HouseGrass`) y `Atmosphere.WeedTufts`. Si baja de 55 FPS, baja primero `Vegetation.Density`.

## Colisiones: qué es sólido y con qué collider
`SolidCatalog` (`Scripts/Procedural/SolidCatalog.cs`) es la fuente única:

| Objeto | Collider |
|--------|----------|
| Árboles muertos | **Capsule** de tronco |
| Coches, cajas, muros, barreras, contenedores, pilares, vigas, techos, piso y marcos de la casa, muebles | **Box** |
| Barriles, tuberías, postes de farola, poste caído, blancos de práctica | **Capsule** |
| Arbustos **grandes** (matorral con escala ≥ 1.1) | **Sphere** |
| Hierba, arbustos pequeños, neumáticos, bolsas, escombros, postes finos de señal, skyline | **ninguno** (no frenan al jugador) |

Faltaban colliders en las tuberías, las farolas, las vigas y el techo de la nave y los bloques sobre los muros: ya los tienen. Todos son colliders simples (nunca `MeshCollider`).

## Verificación
- **Prueba EditMode** (`SolidCatalogTests`): recorre la lista de props sólidos (todos los tipos, variantes y escalas, las piezas fijas del mapa y las cajas de la casa) y **falla** si algún sólido no tiene un collider simple o si algo no sólido lo tiene.
- **Auditoría al jugar** (`ColliderAudit`): al terminar de construir el mapa recorre la escena y escribe en la Console `Auditoría de colisiones: N/N sólidos con collider`. Si alguno no lo tiene sale un **error en rojo** con su nombre, y también avisa de cualquier `MeshCollider`.

---

# Bloque 5 (ronda 3): armas, botiquines y munición escasos

Detalle completo (tablas de armas, botiquines, escasez, JSON y esquema zod) en `docs/LOOT.md`.

## Controles nuevos
| Acción | Tecla |
|--------|-------|
| Cambiar de arma | **1 a 5** o rueda del ratón |
| Atacar con la barra | clic izquierdo (alcance 2.1 m) |
| Recoger un arma | **E** (la munición y los botiquines se recogen solos al acercarte) |
| Buscar en un contenedor | **mantén E** 1-2 s (se cancela al soltar, alejarte o recibir daño) |
| Usar botiquín / venda | **H** / **B** (barra de curación; recibir daño la cancela) |
| Probar un drop | **L** (cuenta para los topes) |
| Siguiente día (respawn lento + entradas de la casa) | **N** |

## Pasos manuales (numerados)
1. Espera a que Unity compile. Se crean solos 7 ScriptableObjects en `Assets/_Project/Resources/Weapons` (uno por arma). Si no aparecen, menú **7Days > Crear armas por defecto (ScriptableObjects)**.
2. Selecciona `Resources/Weapons/shotgun` en la ventana Project: edita daño, cadencia, cargador, recoil, dispersión, alcance, rareza y munición en el Inspector.
3. Pulsa Play. Empiezas con **la pistola (6 + 6 balas) y la barra**: el HUD muestra las reservas de cada tipo con su máximo.
4. Dispara hasta gastar el cargador: queda poca munición, hay que salir de la cobertura a buscar.
5. Entra a la casa y **mantén E** junto a una caja o el estante: barra de búsqueda y, al terminar, el botín cae al suelo con un aro y una luz del color de su rareza.
6. Camina sobre munición: se recoge sola y sale el aviso. Si tu reserva de ese tipo está llena, sale "Munición llena" y el objeto **se queda en el suelo**.
7. Acércate a un arma en el suelo y pulsa **E** (si tienes las 5 casillas llenas, avisa y no se consume).
8. Pulsa **T** para recibir daño y **H** para curarte: aparece la barra; pulsa **T** otra vez durante la curación y se cancela sin gastar el botiquín.
9. Pulsa **L** varias veces: aparecen drops delante de ti hasta el tope de drops del día; **N** pasa de día y reaparece una fracción pequeña del botín.

---

# Fase 3: combate y zombies

## Arreglo de la auditoría de colisiones
`Vegetation/Campfire/Stone` (y `Log`, `Ash` y las antorchas de la casa) aparecían como "no deberían ser sólidos y tienen collider". Causa: `Prim.Create(..., collider: false)` quitaba el collider con `Destroy`, que Unity ejecuta **al final del frame**, y la auditoría corre en el mismo frame que construye el mapa. Ahora usa `DestroyImmediate`, así que esos objetos decorativos salen sin collider (ya estaban bien en `SolidCatalog`: son no sólidos).

### Bloque 1: zombi base (vida, hitboxes, estados)
Qué hay: `Combat/ZombieRig` (cuerpo de primitivas articuladas), `ZombieController` (NavMeshAgent + patrulla/persigue/ataca), `ZombieManager` (pool, tope de 24, NavMesh generado al iniciar), `Hitbox` (zona de impacto).
Cada zona es un collider simple en la capa **Hitbox** (cabeza esfera; torso, brazos y piernas cápsulas) hijo del pivote de su parte, así que sigue a la animación. El cuerpo de movimiento va en la capa 2 (Ignore Raycast): las balas lo atraviesan y solo cuentan los hitboxes.
Multiplicadores por defecto: cabeza x2.5, torso x1.0, brazos x0.6, piernas x0.7 (configurables por tipo en `ZombieCatalog`).

Pasos manuales en Unity (no los puedo verificar sin el editor):
1. Abre el proyecto y espera a que compile. Mira la Consola: debe salir `[7Days] Capa 'Hitbox' creada en el índice 31` (o usa el menú `7Days > Crear capa Hitbox`). Comprueba en `Edit > Project Settings > Tags and Layers` que la capa 31 se llama Hitbox y guarda `ProjectSettings/TagManager.asset` en git.
2. Abre `Window > General > Test Runner > EditMode > Run All`: deben pasar todas, incluidas `CombatTests` y `ZombieStateMachineTests`.
3. Da Play en la escena de pruebas. Espera a que cargue (el NavMesh se genera al final de la carga; puede tardar 1 s).
4. Pulsa **Z**: aparece un zombi 12 m delante (cada pulsación cicla común, corredor, tanque, blindado, lanzador, explosivo). Debe caminar hacia ti sin atravesar paredes ni cajas, y atacar cuando llega.
5. Dispara a la cabeza y al cuerpo: la cabeza debe quitar mucha más vida. Abre `Window > Analysis > Physics Debugger` o selecciona un zombi en la Jerarquía y mira que sus hijos `Hitbox_Head/Torso/Arms/Legs` tienen la capa Hitbox.
6. Si el zombi no se mueve y solo "resbala" en línea recta, el NavMesh no se generó: busca en la Consola avisos de `NavMeshSurface`.
7. Para cambiar el cuerpo por un modelo de Quaternius: ver "Cambiar el cuerpo del zombi" más abajo (cuando se importe el modelo, solo hay que crear otra clase con las mismas partes que `ZombieRig`).

### Bloque 2: daño por arma
Las armas son `WeaponData` (JSON/ScriptableObject) con `minDamageFraction`, `falloffStart`, `armorPenetration`, `headshotMultiplier` y `knockback`. Si tienes assets de armas creados antes, regénéralos: menú `7Days > ...` de `WeaponAssetCreator` (los viejos tienen el daño anterior). Números y fórmula en `docs/BALANCE.md`.
Pasos manuales:
1. Regenera los assets de armas con el menú del `WeaponAssetCreator` (si usas assets en vez de los valores por defecto) y revisa en el Inspector que pistola = 18 de daño.
2. En Play, pulsa **Z** y dispara a un zombi común con la pistola: 2 tiros a la cabeza, unos 6 al cuerpo. Con el fusil unos 3 al cuerpo; con el francotirador 1 a la cabeza (cambia de arma con su número o la rueda del ratón, según el orden en que las recojas).
3. Escopeta a quemarropa: 1–2 disparos; a 25 m apenas hace daño.
4. Colócate tras una caja o pared con el zombi detrás: la bala no debe atravesar el obstáculo.

### Bloque 3: feedback de combate
Qué hay: barra de vida sobre el zombi (`ZombieHealthBar`), números flotantes y sonidos de impacto (`CombatFeedback`), hitmarker en la mira (normal, headshot, muerte), reacción al golpe (empujón y aturdimiento ligero), sangre con pooling (`BloodFx`), charco y cabeza que vuela al morir de headshot.
Pasos manuales:
1. Play, tecla **Z**, y dispara al zombi: aparece su barra verde sobre la cabeza, baja suavemente y pasa a amarillo y rojo. Si no le disparas, solo se ve a menos de 8 m (a menos de 4 m con la vida llena).
2. Dispara al cuerpo: número **blanco**. A la cabeza: número **amarillo** con "CABEZA" y un sonido más agudo. Con el blindado al torso: número **gris** (daño reducido).
3. En la mira debe aparecer una X blanca al impactar (amarilla en headshot) y una X roja más grande al matar, con sonido grave de muerte.
4. Escopeta a quemarropa: el zombi sale empujado hacia atrás y se tambalea; la pistola solo lo sacude. El tanque casi no se inmuta.
5. Mata uno de un headshot: la cabeza desaparece y sale un chorro de sangre más grande; el cuerpo cae y deja un charco que crece.
6. Abre `Window > Analysis > Profiler` con 10+ zombis (pulsa Z varias veces): deben mantenerse 60 FPS. Si ves caídas, anótalo y dime cuántos zombis había.
