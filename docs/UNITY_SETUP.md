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

### Bloque 4: tipos de zombie
Pasos manuales:
1. Play, pulsa **Z** varias veces (cicla: común, corredor, tanque, blindado, lanzador, explosivo). El corredor debe ser claramente más rápido y morir con pocos tiros; el tanque, lento y esponjoso; el blindado, casi inmune al torso (apunta a la cabeza).
2. Lanzador: se queda a unos 12 m y, tras 0.9 s de aviso (brazos arriba), lanza una bola verde en línea recta. Ponte tras una caja pulsando **C** en cobertura: la bola debe chocar con la caja y salir "La cobertura te protegió" sin daño.
3. Quédate 1 s o más en cobertura sin línea de tiro: empieza a lanzar en arco; aparece un círculo rojo en el suelo 1–1.5 s antes y debes salir de él para evitar el daño de área. Tras 4 s en cobertura, intenta rodear para tener línea de tiro.
4. Explosivo: parpadea en naranja cerca de ti; dispárale a distancia: explota y daña a los zombis cercanos. Si te mata de cerca, comprueba el daño que recibes (hasta 42 en el centro).
5. Mata varios y comprueba que cada tipo suelta botín según su tabla (tecla **N** cambia de día; los jefes se prueban en el Bloque 5).
6. Opcional: menú **7Days > Exportar zombis por defecto (JSON)**, cambia `maxHealth` de un tipo en `Resources/Zombies/zombies.json` y vuelve a dar Play: el zombi debe cambiar. Un JSON inválido se ignora con un aviso en la consola.

### Bloque 5: jefes
Teclas de prueba: **B** = jefe delante (cicla día 3, 5, 7), **Shift+B** = reponer coberturas rotas, **T** = recibir 20 de daño, **Y** = curarte.
Pasos manuales (nada de esto lo puedo verificar sin el editor):
1. Play y pulsa **B**: un zombi enorme aparece a 20 m; arriba debe salir la **barra grande** con su nombre ("BRUTO TITÁN") y dos marcas blancas al 50 % y 25 %. No debe tener barra pequeña sobre la cabeza.
2. **Bruto (día 3):** cuando aparezca una franja roja en el suelo hacia ti y suene el tono de aviso, apártate de lado. Debe cargar en línea recta, romper cajas y muebles ligeros al pasar, y si choca contra un muro o un contenedor grande debe quedar aturdido unos 2.5 s (texto "¡ATURDIDO! ¡DISPARA!"): tus disparos hacen entonces más daño. Compara cuántos disparos necesitas con y sin aturdimiento.
3. Dispárale hasta bajar del 50 %: ruge, se vuelve más rojo y se mueve más rápido ("¡FURIA!"). Otra vez al 25 %.
4. Pulsa **B** de nuevo para el **Lanzador Colosal (día 5)**: aparece un círculo rojo en el suelo donde caerá un objeto grande 1–1.5 s después; sal del círculo. La cobertura que alcance debe desaparecer y debe invocar corredores. Pulsa Shift+B para reponer coberturas.
5. **B** otra vez: **El Devorador (día 7)**. Avisa una onda expansiva (círculo rojo grande): **salta** justo antes de que termine y debe salir "¡Esquivaste la onda!". Cada ~14 s (y tras la onda) se enciende un núcleo naranja en su pecho: un disparo ahí debe hacer mucho más daño que en el torso.
6. Mátalo: debe soltar un objeto raro o mejor cerca de su cuerpo aunque ya hayas agotado los drops del día (mensaje "¡El jefe soltó un botín raro!").
7. Con 3 o 4 jefes y 20 zombis en pantalla vigila los FPS (F3 muestra el gobernador de calidad); si bajan de 55 dime qué hacía el jefe en ese momento.

### Bloque 6: métricas y parámetros para el director
Teclas: **F4** panel de métricas, **N** siguiente día (aplica `DirectorTuning.ForDay`), **V** mini oleada de 8 zombis con las proporciones del día.
Pasos manuales:
1. Play, pulsa **F4**: aparece el panel con precisión, headshots, TTK medio, tiempo en cobertura, daño recibido por tipo y los parámetros activos.
2. Dispara a varios zombis (Z) con headshots y sin ellos: la precisión y la proporción de headshots deben moverse con lo que haces; el TTK sale de la primera bala a la muerte.
3. Pégate a una caja con **C** durante unos segundos: "en cobertura" debe subir. Recibe daño de un lanzador y de un común: deben aparecer por separado en "daño recibido".
4. Pulsa **N** varias veces y luego **V**: al día 1 salen casi todos comunes; al día 7 hay más corredores, tanques, blindados, lanzadores y explosivos, y los zombis tienen más vida (compruébalo con los números de daño). El panel F4 muestra los multiplicadores.
5. Con una mini oleada (V) pulsada dos o tres veces (16–24 zombis), mira los FPS del HUD: deben seguir en ~60.

### Corrección: MissingReferenceException al reiniciar
Causa: `PlayerInteract.Registry` (lista estática) conservaba los pickups destruidos al recargar la escena (`[RuntimeInitializeOnLoadMethod]` solo corre al dar Play, no al recargar), y `it == null` sobre una interfaz no detecta objetos destruidos de Unity. Arreglo: `SceneStatics.ResetAll()` (al descargar la escena y al crear el `LootManager`), `PlayerInteract.IsAlive`, `OnDestroy` en los pickups y descarte de los pools estáticos de proyectiles y marcas.
Pasos manuales:
1. Play, mata o deja que te maten y pulsa **R** para reiniciar. La Console no debe mostrar ningún `MissingReferenceException`.
2. Tras cada (re)inicio la Console escribe `[7Days] Loot reconstruido: N puntos (...), M contenedores buscables, K pickups activos...`. Deben salir los mismos números en la primera partida y en cada reinicio.
3. Busca un contenedor (mantén E) y recoge un objeto tras reiniciar: todo funciona como en la primera partida.

### Corrección: el zombi gigante no entra a la casa
Pasos manuales:
1. Play y espera la carga. Pulsa **Z** hasta que salga "Tanque" (tercer tipo) y entra tú a la casa (centro del mapa, entrada por el oeste).
2. El tanque debe acercarse al muro más cercano a ti, golpearlo (polvo, sacudida, y daño si estás pegado al muro) y, tras unos 4 s sin alcanzarte, rodear la casa por fuera. No debe cruzar ninguna puerta ni vibrar en el sitio.
3. Un zombi común (primer tipo de **Z**) sí debe entrar por la puerta.
4. Con el jugador fuera de la casa el tanque te persigue con normalidad. Abre **Window > AI > Navigation** y en `ZombieManager` comprueba que hay dos superficies: una del agente por defecto y otra (GiantNavMesh) con un tipo de agente nuevo; en la segunda, el interior de la casa debe verse sin malla azul.
5. Si ves el aviso "No se pudo limitar el volumen de la casa..." en la Console, dímelo: significa que esa versión del paquete cambió el campo privado (siguen funcionando el radio y la barrera de seguridad).

### Corrección: zombi explosivo tipo creeper
Pasos manuales:
1. Play, **Z** hasta "Explosivo" (sexto tipo). Debe perseguirte y, a ~3 m, detenerse, inflarse y parpadear blanco-rojo con un siseo.
2. Mecha completa (~1.4 s) a menos de 3.6 m: explosión con bola de fuego, humo, destello de luz y sonido; recibes daño con caída por distancia (hasta 45 en el centro), te empuja y sacude la cámara. Un fuego queda ~3 s.
3. Aléjate rápido más de ~5.5 m durante la mecha: debe cancelarse (se desinfla, deja de silbar) y volver a perseguirte.
4. Colócate tras la pared de la casa cuando explote al otro lado: el daño debe ser mucho menor (25 %).
5. Mátalo de un headshot en plena mecha: explota igual. Dispárale desde lejos con otros zombis a su lado: les hace daño y los empuja.
6. Una caja ligera cerca del estallido desaparece (como con la carga del bruto).

### Corrección: armas por búsqueda
Pasos manuales:
1. Play: en la Console debe salir `[7Days] Loot reconstruido: ... puntos omitidos ...`; si "puntos omitidos" es mayor que 0 dime cuáles (aparecen como aviso "no hay mueble/coche cerca").
2. Busca (mantén E) en el estante del fondo de la casa, en la caja lejana del noreste (26, 25) y en la caja de (-12, 13): cada una debe dar un arma que no tengas ("¡Encontraste un arma!").
3. Busca en 10–15 contenedores normales: deberías encontrar 1 o 2 armas, y nunca una repetida. Tras ~7 búsquedas sin arma la probabilidad sube; a las 15 es seguro.
4. Pulsa **N** para pasar de día: si ya buscaste los puntos garantizados y te faltan armas, vuelven a armarse (mensaje "Se oye ruido...").
5. La munición y los botiquines deben seguir igual de escasos que antes.

### Corrección: armas garantizadas visibles
Causa: las 3 armas garantizadas eran un resultado de búsqueda (solo aparecían al mantener E sobre el mueble/caja), así que no había ninguna arma en el suelo y la consola decía "0 armas". Ahora `LootManager.PlaceGuaranteedWeapons()` pone un pickup visible con brillo sobre cada punto garantizado al construir el mapa (y las repone al cambiar de día si faltan).
Pasos: Play y mira la Console: debe decir `3 pickups activos + ... (3+ armas, 3 garantizadas)`. Ve al estante de la casa, a la caja lejana (26, 25) y a la de (-12, 13): cada una tiene un arma brillando encima (E para recogerla).

### Drops de zombis
1. Play, mata unos 20 zombis comunes (**Z**): deberías ver ~2 drops brillando suavemente; el tanque suelta con más frecuencia (~30 %).
2. En la Console cada drop escribe `[7Days] Drop #N de 'tipo': ...` con el contador del día (hoy X/8). Al reiniciar la línea `Loot reconstruido` muestra "Drops de zombis: 0 en 0 muertes".
3. Tras 8 drops en un día aparece "descartado por el tope"; **N** pasa de día y reinicia el contador.

## Cámara y combate estilo Gears of War / Call of Duty

### Bloque 1: cámara sobre el hombro
Ajustes en `Resources/Config/CameraSettings.asset` (se crea solo al abrir Unity; menú **7Days > Crear ajustes de cámara**): sensibilidad, límites de pitch, suavizado, tiempo de cambio de hombro y sacudidas. Por arma (en `Resources/Weapons/*.asset`): `aimFov`, `aimDistance`, `aimSensitivity`, `adsTime`, `fireShake`.
Pasos manuales:
1. Abre Unity y espera a que compile. La Console debe decir "Creados 2 ajustes de cámara" y, si tenías armas creadas antes, "Actualizados N ScriptableObjects de armas de una versión anterior".
2. Play. Pulsa **Q**: la cámara pasa al hombro izquierdo en ~0.25 s, sin saltos; **Q** otra vez vuelve.
3. Acércate de espaldas a una pared: la cámara se acerca y no la atraviesa.
4. Mantén clic derecho: el FOV y la distancia cambian con suavidad (entra y sale con ease) y el ratón gira más despacio.
5. Dispara: pequeña sacudida (más fuerte con escopeta y revólver). Pulsa **T** (recibir daño): sacudida de golpe. Salta y aterriza desde algo alto (**U**): sacudida de aterrizaje. Corre: un golpe muy leve en cada paso.
6. Esc: aparece "Ajustes" con **Movimiento de cámara** (0–100 %) y sensibilidad. Con 0 % no debe haber balanceo, jadeo ni sacudidas.

### Bloque 1B: cámara al correr
Valores en `Resources/Config/CameraRunSettings.asset`. Límites duros en código: roll ≤ 1.5°, desplazamiento ≤ 8 cm.
Pasos manuales:
1. Corre con **Shift** por la calle: la cámara baja un poco, se acerca y abre el FOV (~+6°). Se balancea de lado a lado con cada paso (±4.5 cm), con un leve roll y rebote.
2. Corre más de 4 s seguidos: el balanceo crece poco a poco (hasta +40 %), los pasos se aceleran un poco y aparece un jadeo (cabeceo suave). Para: en 2–3 s vuelve todo a la calma.
3. Apunta (clic derecho) mientras corres: el efecto desaparece en 0.15 s y la mira queda estable.
4. Agáchate (Ctrl), entra en cobertura (C), salta o haz un vault: el efecto se apaga con suavidad. Correr en diagonal o de lado lo reduce a la mitad.
5. **F4** (métricas del director) muestra "corriendo N s, fatiga X %". **F5** (panel de ajuste, Bloque 6) muestra balanceo, roll y fatiga en vivo.

### Bloque 2: apuntado por la mira (ADS)
Por arma: `aimFov`, `aimDistance`, `aimSensitivity`, `adsTime`, `aimFirstPerson`, `scopeFovs` (zoom variable). Cada arma tiene un **SightPoint** (hijo de `Player/WeaponHolder/WeaponPose`): al importar un modelo real, colócalo justo detrás de la mira del arma.
Pasos manuales:
1. Con la pistola, clic derecho: el arma se centra, el FOV baja (60 → 46), la retícula se cierra casi a un punto y los disparos se agrupan mucho más.
2. Con el francotirador (búscalo o usa la caja lejana garantizada): al apuntar, la cámara pasa a la mira en ~0.28 s, aparece el overlay negro de la lente con la cruz y "x3.0". La rueda del ratón cambia el zoom (x3 → x5 → x8.6) y la sensibilidad baja con el zoom. Tu cuerpo y el arma dejan de verse (solo proyectan sombra).
3. Con la barra metálica no se puede apuntar. Durante un vault tampoco. En cobertura sí (ver el lean en el Bloque 4).
4. Mientras corres y aprietas clic derecho, dejas de correr y apuntas (la cámara de carrera se anula en 0.15 s).

### Bloque 3: retroceso y manejo del arma
Por arma: `recoil` (vertical), `recoilHorizontal`, `recoilRecovery`, `recoilMaxPitch`, `recoilAimMultiplier`, `recoilCrouchMultiplier`, `spreadPerShot`, `spreadMax`, `spreadRecovery`, `spreadMove`, `spreadAir`, `spreadCrouchMultiplier`, `swayAmount`, `bobAmount`, `kickback`, `kickbackPitch`, `fireModes`, `burstCount`. Valores en `docs/BALANCE.md`.
Pasos manuales:
1. Fusil de asalto: mantén el gatillo contra una pared: la mira sube y serpentea un poco a los lados; suelta: vuelve sola a donde apuntabas. Apuntando y agachado sube menos.
2. Dispara en ráfaga sin apuntar: la retícula se abre; al parar se cierra. Corriendo o saltando también se abre.
3. **X** cambia el modo del fusil: AUTO → RÁFAGA (3 balas por clic) → SEMI. El HUD muestra el modo junto a la munición.
4. Gira la cámara rápido: el arma se retrasa un poco (sway). Camina y corre: se mece con los pasos (bob). Al disparar retrocede y se levanta (kickback). Caen casquillos dorados a la derecha (la escopeta, más grandes).

### Bloque 5: feedback
1. Cada arma suena distinto (pistola seca, escopeta grave, francotirador largo); tras la escopeta suena el bombeo y tras el francotirador el cerrojo. Al quedarte sin balas suena un "clic".
2. Destello, trazadoras, hitmarker normal/headshot/muerte y números flotantes siguen como antes.
3. Pasos: suenan con cada pisada (más fuertes corriendo, más suaves agachado). Corre más de 4 s: la respiración se oye cada vez más y más rápida; al parar se calma.

### Bloque 4: locomoción, lean y Animator
Pasos manuales:
1. Ponte tras un muro alto con **C** y apunta: el cuerpo se inclina hacia el hombro activo y la cámara se desplaza a ese lado (~40 cm). **Q** cambia el lado del lean. Tras una caja baja, al apuntar te asomas por encima y la inclinación es pequeña.
2. Apunta y muévete en 8 direcciones (strafe): el cuerpo sigue mirando a la cámara y el arma sigue el pitch de la mira (arriba/abajo).
3. Menú **7Days > Crear Animator del jugador**: aparece `Assets/_Project/Animation/PlayerAnimator.controller`. Ábrelo: 16 parámetros, blend tree `Locomotion` con 10 ranuras vacías, estados Crouch/Cover/Airborne/Vault y capa UpperBody (Fire/Reload/Melee). Cómo conectar los modelos: `docs/ANIMATION_HOOKS.md`.

### Bloque 6: panel de ajuste en vivo (F5)
1. Play y pulsa **F5**: se suelta el ratón y aparece a la izquierda el panel con los valores en vivo (retroceso, dispersión, bloom, FOV, apuntado, sensibilidad, hombro, lean, cámara de carrera, fatiga, pasos por segundo).
2. Mueve los deslizadores del arma actual (retroceso, dispersión, FOV de apuntado, sway...), de la cámara y de la cámara de carrera: el cambio se nota al momento (los límites duros siguen aplicándose). Los cambios duran hasta salir de Play: copia los números que te gusten al ScriptableObject del arma o a `CameraRunSettings`.
3. **F5** otra vez cierra el panel y vuelve a capturar el ratón. Hacer clic sobre el panel no captura el ratón.

## Cobertura estilo Gears of War: asomarse y disparar (pop-out)
Especificación: `COBERTURA_POPOUT.md`. Ajustes (en `Resources/Config/CameraSettings.asset`): `popSeconds` (0.15–0.25 s), `lowCoverMax` (1.45 m: más baja = se asoma por encima), `popSideMeters`, `blindDamageMultiplier` (x0.6), `blindSpreadMultiplier`, `blindMinSpread`/`blindMaxSpread`, `blindExposure`, `blindHold`, `coveredDamageFloor` (fracción del daño cuerpo a cuerpo que pasa estando cubierto). También se pueden mover en vivo con **F5**.

**Teclas en cobertura**
| Tecla | Efecto |
|-------|--------|
| **C** | entrar / salir de la cobertura (más cercana) |
| **A / D** | deslizarse a lo largo de la cobertura |
| **Clic derecho (mantener)** | asomarse: por **encima** si es baja (hasta 1.45 m), por el **lado** del hombro activo si es alta y estás en su borde. Al soltar vuelves a cubrirte (0.2 s) |
| **Clic izquierdo estando asomado** | disparo normal (apuntado, con tu dispersión y daño completos) |
| **Clic izquierdo sin apuntar** | disparo **a ciegas**: dispersión muy alta, daño x0.6, exposición parcial unos 0.45 s |
| **Q** | cambia de hombro y, con él, el lado por el que te asomas (si solo hay sitio por el otro lado, la cámara cambia sola) |
| **R** | recargar: solo cubierto (asomado sale "Cúbrete para recargar") |
| **Espacio / C** | saltar el obstáculo o salir, como antes |
| barra metálica | el cuerpo a cuerpo no se asoma ni dispara a ciegas |

Indicador en pantalla: **CUBIERTO** (azul), **ASOMADO** (rojo) y **A CIEGAS** (naranja). Si al apuntar no hay sitio por ningún lado (cobertura alta, lejos de un borde) sale el aviso "solo fuego a ciegas".

**Pasos manuales**
1. Pégate a una caja baja (C) y mantén clic derecho: te levantas y la cámara sube por encima (0.2 s); la barra de estado pasa a ASOMADO. Dispara: la bala debe salir por encima de la caja y herir al zombi del otro lado. Suelta el clic: vuelves a CUBIERTO.
2. Pégate a un muro alto cerca de su borde y mantén clic derecho: te inclinas hacia el borde del hombro activo y la cámara se desplaza ~50 cm. **Q** cambia al otro lado (si el otro borde está lejos, la cámara se mantiene en el hombro con borde).
3. Pégate al medio de un muro alto y apunta: no te asomas (aviso). Clic izquierdo sin apuntar: disparo a ciegas sobre el borde (o "Sin línea de tiro" si no hay sitio).
4. Con un zombi en la otra cara de una caja baja, dispara a ciegas: las balas se dispersan mucho (cono de ~8–20°) y hacen 60 % del daño (los números flotantes lo muestran). Comprueba que en el primer metro la propia caja no las frena, y que otra caja o muro delante sí.
5. Con un lanzador: cubierto del todo, su proyectil directo se estrella en la caja ("La cobertura te protegió"); asomado, te golpea. Un zombi cuerpo a cuerpo hace ~35 % del daño si estás cubierto y 100 % asomado.
6. A 15 m de un zombi, agáchate tras la caja sin asomarte: tarda en detectarte (solo te ve de cerca, ~6 m); asomado te ve desde su rango normal.
7. **F4**: tiempo cubierto, asomado y a ciegas; "[ABUSO]" aparece si pasas más del 85 % de ≥ 20 s escondido. **F5** muestra la exposición y por dónde te asomas.

### Cambio: disparar cubierto = asomarse solo
- **Clic izquierdo en cobertura**: el personaje se asoma lo justo (por encima si es baja, por el lado si es alta y hay borde), dispara desde ahí hacia el punto de la retícula y vuelve a cubrirse 0.4 s después de soltar (`popHoldAfterFire`, 0.3–0.5). En automático/ráfaga sigue asomado mientras disparas.
- **Ctrl + clic izquierdo**: fuego a ciegas (también si no hay hueco para asomarse).
- **Clic derecho**: asomarse y apuntar hasta soltar.
- **R** tras disparar: vuelve a cubrirse y recarga en cuanto está cubierto.
- Depuración: **F5** > "Logs de cada disparo en cobertura": la Console escribe estado, lado, origen, collider golpeado y por qué no se asomó.
Pasos: 1) pégate a un muro bajo (C) y haz clic izquierdo sin apuntar: te levantas, el tiro pasa por encima y el indicador muestra ASOMADO; 2) suelta: a los ~0.4 s vuelves a CUBIERTO; 3) Ctrl + clic: A CIEGAS; 4) junto al borde de un muro alto, clic: te asomas por el lado.

### Apuntado y francotirador
1. Con cada arma de fuego (pistola, revólver, subfusil, escopeta, fusil, francotirador), clic derecho: la retícula se cierra, el FOV baja, el arma se centra y los disparos se agrupan. En cobertura también (te asomas). Con la barra no se apunta.
2. Esc > **Apuntar: mantener / alternar**: en "alternar" un clic derecho apunta y otro deja de apuntar (se guarda). Si haces un vault o cambias a la barra, deja de apuntar solo.
3. Francotirador apuntando: la lente es más clara (borde negro fino, viñeta suave). La rueda cambia x3 → x5 → x8 → x12 (se ve en la etiqueta) y la sensibilidad baja con cada nivel.
4. La mira se mece despacio. Mantén **Shift**: el vaivén casi desaparece ("CONTENIENDO 100%" baja hasta 0 en ~3 s). Al agotarse: jadeo audible, pequeño cabeceo y la mira se mueve más ("JADEANDO") durante ~2.5 s.
5. Dispara a un zombi a más de 260 m (si el mapa lo permite) o a 100–200 m: el daño es el mismo que de cerca (un tiro a la cabeza).

### Corrección: "The referenced script on this Behaviour is missing" (CameraRunSettings)
Causa: `CameraSettings` y `CameraRunSettings` estaban en un mismo archivo (`CameraConfigs.cs`). Unity solo enlaza un ScriptableObject a un archivo que se llame como su clase, así que `CameraRunSettings.asset` se creó con `m_Script: {fileID: 0}` y la cámara de carrera usaba los valores por defecto. Ahora cada clase tiene su archivo (`CameraSettings.cs` conserva el GUID de antes) y `MissingScriptRepair` vuelve a enlazar el asset sin tocar sus valores.
1. Abre Unity y espera a que compile: la Console debe decir `Reparado Assets/_Project/Resources/Config/CameraRunSettings.asset ... (valores conservados)` (o usa **7Days > Reparar scripts de los ajustes**).
2. Selecciona `Resources/Config/CameraRunSettings.asset`: el Inspector muestra el script y sus valores (fovBoost 6, swayMeters 0.045...).
3. Play: el aviso de script desvinculado ya no aparece. Corre 5 s: balanceo y fatiga como antes; cambia `swayMeters` en el asset y comprueba que se nota (antes no se leía).

### Estadísticas de cobertura y fatiga guardadas (backend + director)
Requiere el backend en marcha (`cd backend && npm run dev`, puerto 3001) y haber entrado desde la escena Login (sin sesión no se guarda nada).
1. Entra con tu cuenta y da Play. Juega un rato: escóndete tras una caja unos 40 s sin asomarte y corre más de 5 s con **Shift**.
2. **F4**: "cobertura: cubierto N s asomado N s a ciegas N s" y "corriendo N s fatiga X %".
3. Pulsa **N** (fin de día): en la Console no debe haber avisos de guardado. Comprueba el guardado: `curl -H "Authorization: Bearer <token>" http://localhost:3001/api/progress` debe incluir `coverSeconds`, `poppedOutSeconds`, `blindFireSeconds`, `sprintSeconds` y `maxFatigue` en `stats`.
4. Con más del 85 % del tiempo escondido (≥ 20 s), al pulsar **N** la Console dice `[7Days] Director: abuso de cobertura ...` (también en F4) y salen más lanzadores. Con fatiga ≥ 80 % y ≥ 30 s corriendo: "corres hasta agotarte": más corredores.
5. Cierra Unity, vuelve a entrar con la misma cuenta: la Console dice `Estadísticas guardadas cargadas ...` y la regla de abuso sigue usando lo acumulado.
Nota: por ahora el guardado envía el inventario vacío (el guardado del inventario llega en la Fase 6), así que sobrescribe el inventario guardado.

## Arte y animación procedural (ARTE_PROCEDURAL.md)

### Bloques 1 y 2: esqueleto, rig y animación del jugador
El jugador ya no es una cápsula: es un soldado corpulento (casco, hombreras, chaleco, guantes, botas) sobre un esqueleto de 22 huesos animado por código. Las mallas detalladas y texturas llegan en el Bloque 4.
Pasos manuales (lo importante es la FLUIDEZ):
1. Play. Quieto: respira (el pecho sube y baja), cambia el peso de un pie a otro muy despacio. Gira la cámara y el cuerpo: si los pies quedan mal colocados, da un pasito corrector (uno cada vez).
2. Camina (WASD) y corre (Shift): los pies se apoyan en el suelo SIN patinar (fíjate en un pie apoyado: no se desliza); la zancada se alarga al correr, el torso se inclina hacia delante, los hombros contrarrestan la cadera, la pelvis rebota en cada paso. Corriendo sin apuntar el brazo izquierdo suelta el arma y se balancea.
3. Apunta (clic derecho) y muévete en 8 direcciones: el cuerpo mira a la cámara y las piernas dan pasos laterales y hacia atrás; el pie que cruza pasa por delante. Las dos manos van al arma.
4. Gira rápido: el torso y la cabeza empiezan a girar antes que las piernas (anticipación); el arma se retrasa un poco (sway).
5. Agáchate (Ctrl) quieto y caminando: la pelvis baja, las rodillas se doblan y los pies se separan un poco.
6. Salta: piernas recogidas en el aire; desde una altura (U): al aterrizar la pelvis baja más cuanto mayor es la caída y vuelve con suavidad.
7. Vault (Espacio junto a una caja): las manos se apoyan en el obstáculo y las piernas se recogen al pasar.
8. Cobertura (C): agachado y pegado; A/D se desliza dando pasos laterales; clic derecho o clic izquierdo se asoma (por encima se levanta, por el lado se inclina); Ctrl + clic: el brazo se extiende sobre el borde (a ciegas).
9. T (recibir daño) desde distintos ángulos: el torso retrocede en la dirección del golpe. Muere: se desploma y cae de espaldas; el arma cae al suelo.
10. Corre con Shift: el balanceo de la cámara y el sonido de los pasos van con cada pie que toca el suelo (antes eran un ciclo aparte).
11. FPS en el HUD: el rig del jugador cuesta muy poco (la cifra exacta en ms la mostrará el panel F6 del Bloque 7).
Si algo se ve roto (rodillas hacia atrás, pies que flotan, temblores), dime en qué acción y con qué arma.

### Correcciones del Bloque 2: agachado, rendimiento y panel F6
- **Agachado**: pelvis más baja y 12 cm atrás, pies algo abiertos y bajo las rodillas, rodillas hacia delante y algo hacia fuera (no se cruzan), torso inclinado 18°, arma a la altura del pecho (antes quedaba en la tripa). De pie las rodillas ya no se bloquean (estaban estiradas del todo y los pies podían flotar 1–2 cm). Las mismas medidas quieto y caminando: sin saltos al pasar de uno a otro.
- **Rendimiento**: quieto, el rig no lanza rayos al suelo; caminando mide el suelo como mucho 10 veces por segundo por pie (antes en cada frame); a más de 18 / 35 / 60 m se actualiza cada 2 / 3 / 6 frames y desde 35 m sin rayos. Las piezas pequeñas del soldado no proyectan sombra.
- **F6**: panel de la animación: coste en ms (último y media), coste de todos los rigs, rayos por actualización, LOD, fase y longitud de la zancada, apoyo, pesos de mezcla, suavizados editables, casilla para dibujar huesos (cian), pies (verde apoyado, rojo en el aire) y manos (amarillo), y una **prueba de carga con 20 soldados** caminando alrededor.
Pasos:
1. Agáchate quieto, caminando, en cobertura baja y con cada arma: rodillas hacia delante, pies plantados, arma frente al pecho.
2. **F6**: anota "Coste del jugador" y "Todos los rigs" con 1 jugador. Marca "20 soldados" y vuelve a anotar el total y los FPS. Mándame los números.

### Bloque 3: agarre, poses y recarga animada
Pasos manuales:
1. Cambia de arma (1-5): el arma baja a la cadera y sube en ~0.35 s; no dispara hasta estar lista.
2. Pistola y revólver: a la cadera con una mano; al apuntar, las dos. Subfusil: al pecho con dos manos. Fusil, escopeta y francotirador: al hombro con dos manos.
3. Corre (Shift): el arma baja e inclina (rifles en diagonal, pistola abajo); en cobertura sin asomarte, el arma pegada al pecho.
4. Dispara: la corredera de la pistola y la palanca del fusil retroceden; la bomba de la escopeta y el cerrojo del francotirador ciclan tras el disparo (y entonces sale el casquillo). El torso y los brazos acusan el retroceso.
5. Recarga (R) cada arma: pistola/subfusil/fusil: el cargador baja y desaparece, la mano izquierda va a la cintura, vuelve con el nuevo (sube y encaja: ahí sube la munición), golpe. Escopeta: cartucho a cartucho (mano a la cintura y a la ventana), y si disparas a mitad conserva los metidos. Revólver: el tambor se abre a un lado. Francotirador: cerrojo abierto, cargador, cerrojo cerrado. Cambia de arma a mitad: se cancela.
6. Barra: clic = golpe con anticipación, curva y seguimiento, con estela; golpes seguidos alternan el lado. Mantén el clic: "CARGANDO..." y el arma se echa atrás; a los 0.45 s "¡GOLPE CARGADO!", suelta: golpe lento y fuerte.

### Correcciones de pose: suelo, agachado, cobertura y carrera
**Diagnóstico de las piernas bajo el asfalto en cobertura:** al entrar en cobertura el jugador se teletransporta junto al muro (hasta ~1.6 m) y los pies seguían clavados en el sitio anterior hasta dar pasos correctivos de uno en uno: la IK estiraba las piernas en diagonal hacia el suelo de atrás. Además no había regla dura de suelo: el tobillo podía pedirse bajo el suelo, la rodilla podía doblarse sin límite y bajar del suelo con la pelvis muy baja, y nada impedía que un pie cayera dentro del collider de la cobertura. El orden de actualización era correcto (movimiento -> cámara -> arma -> driver -> rig).
**Arreglos:** pies recolocados al instante en un salto corto de posición; `LegSolver` con reglas duras (tobillo >= suelo + tobillo + suela, rodilla 0-150° hacia delante y algo fuera, rodilla nunca bajo el suelo: si hace falta se adelanta el pie, nunca se dobla hacia atrás); pelvis limitada a la altura mínima que permiten las piernas; pies fuera del collider de la cobertura.
**Posturas:** cobertura baja, cubierto y quieto: una rodilla al suelo con la pantorrilla plana (la del lado del hombro activo; **Q** la cambia) y la otra a ~90°, torso erguido; Ctrl (sin cobertura): sentadilla con las dos rodillas, talones arriba solo si la rodilla pasa de 120°; en cobertura el cuerpo mira a lo largo del muro con el hombro cercano pegado (45°); al asomarse por encima se estira desde las rodillas, por el lado se inclina el torso con la pelvis quieta; quieto, la pelvis se centra sobre los pies.
**Carrera:** brazos libres en oposición a las piernas con el codo a ~90°; rifles en "low ready" con dos manos (cañón abajo y adelante); pistola y barra al costado con un balanceo amortiguado y el otro brazo libre; el arma sigue el rebote y el giro del pecho con un retraso elástico (se quitó el balanceo propio del arma, que duplicaba el del pecho). La cámara de carrera ya seguía los pasos reales: no hay duplicado.

Pasos manuales:
1. **F6** > marca "Mostrar altura de suelo y mínimo de pies": bajo cada pie se ve una cruz blanca (suelo) y una línea naranja (mínimo del tobillo); el punto del tobillo debe ser siempre verde. Junto a cada rodilla, su ángulo (verde < 120°, amarillo < 145°, rojo cerca del límite de 150°).
2. Agáchate (Ctrl) quieto y caminando sobre asfalto, en bordillos y rampas: ningún pie bajo el suelo, rodillas hacia delante y algo abiertas, sin cruzarse.
3. Entra en cobertura (C) junto a una caja baja: los pies quedan bajo el cuerpo al instante (no se estiran hacia atrás) y, quieto, se apoya una rodilla en el suelo. Pulsa **Q**: cambia la rodilla de apoyo. Desliza con A/D: se levanta a cuclillas y da pasos cortos sin cruzar las piernas. Ningún pie entra en la caja.
4. Con clic derecho o clic izquierdo te asomas: por encima te estiras desde las rodillas; junto al borde de un muro alto, el torso se inclina y la pelvis no se mueve.
5. Corre con la barra o la pistola: el brazo libre va contrario a su pierna con el codo doblado; el arma al costado se balancea poco. Corre con el fusil: arma abajo y adelante con las dos manos. Pulsa clic derecho corriendo: pasa a apuntar sin saltos.
6. Mira el panel F6 con "PELVIS LIMITADA": si aparece a menudo en una postura normal, dime cuál.

### Motricidad y agarre del jugador
**Por qué las piernas iban casi rectas:** la rodilla solo se doblaba lo que la IK necesitaba para llegar al pie; no había un modelo de cuánto debe doblarse en cada fase, la pelvis iba alta y el pie en el aire solo subía unos centímetros.
**Ahora:** `GaitPose` da por fase de la zancada la flexión de rodilla (contacto 15°, apoyo medio 5°, despegue 32°, vuelo 62° andando y 112° corriendo con el talón hacia el glúteo), el ángulo del muslo (30° / 55° adelante) y el rodar del pie (talón, planta, punta). La pelvis va a la altura que da esa flexión en las piernas apoyadas (baja al contactar, sube en el apoyo medio); el pie en el aire sigue la pierna doblada y converge a su aterrizaje (sin patinar). Valores en `Resources/Config/LocomotionPoseSettings.asset` (se crea solo) y editables en F6.
**Media rodilla** en cobertura baja: rodilla delantera a ~90° con la espinilla vertical y el pie plano (la pierna contraria al hombro activo), rodilla trasera a ~5 cm del suelo con la espinilla casi horizontal y la punta apoyada, torso 30° adelante. Al deslizarte con A/D baja a media altura con pasos cortos. **Entrar en cobertura ya no teletransporta**: caminas hasta el muro en ~0.35 s (pasos de verdad). El arma en cobertura va al pecho apuntando adelante y algo abajo, con dos manos (también la pistola).
**Agarre:** manos con pulgar y 4 dedos de 2 falanges; poses de empuñadura (índice estirado fuera del guardamonte hasta que disparas), agarre en "C" bajo el cañón, sobre el cargador (subfusil), en la bomba (escopeta), puño en la barra. Puntos de agarre por arma en su ScriptableObject (`grip`: empuñadura, guardamano, culata; los assets antiguos se actualizan solos a la versión 4 conservando lo editado). Codos hacia abajo y un poco hacia dentro. Al apuntar con rifle, escopeta o francotirador: culata en el hueco del hombro, postura de perfil (torso girado 25°: así el brazo izquierdo llega al guardamano) y mejilla pegada al arma. Pistola: dos manos al apuntar, una a la cadera.
**Geometría corregida (lo detectó la prueba de agarre):** la bomba de la escopeta estaba al 62 % del largo (0.74 m desde la culata: ningún brazo llega); ahora al 42 %. Lo mismo con el guardamano del fusil (60 % → 40 %) y del francotirador (55 % → 22 %).

Pasos manuales:
1. **F6** > "Mostrar ángulos y objetivos de agarre": junto a rodillas y codos sale su ángulo; en el arma, puntos verde (empuñadura), azul (guardamano) y magenta (culata); las manos (amarillo) deben coincidir con ellos. La gráfica muestra la flexión de rodilla en vivo.
2. Camina y corre en línea recta y míralo de lado: al contactar la rodilla ~15°, en el vuelo el talón sube (60° andando, 110° corriendo), el pie rueda talón-planta-punta y la pelvis baja un poco en cada contacto. Ajusta los valores en F6 si hace falta.
3. Pulsa C junto a una caja baja: el personaje camina hasta ella y baja a media rodilla (la rodilla trasera casi en el suelo, la delantera a ~90°). Q cambia la pierna delantera. A/D: se desliza a media altura con pasos cortos.
4. Con el fusil apuntando: culata en el hombro, torso de perfil, mejilla en el arma, mano izquierda en el guardamano. Con la pistola: a la cadera con una mano, apuntando con dos. Escopeta: la mano izquierda en la bomba (y sigue la bomba al ciclar). Barra: puño cerrado.
5. Dispara: el índice pasa al gatillo y vuelve a estirarse fuera del guardamonte un momento después.
6. Recarga: la mano izquierda se abre al coger el cargador y vuelve al guardamano sin saltos.

### Piernas v2: lo que se ve (no solo la matemática)
**Causa encontrada:** los huesos de piernas y brazos se giraban con `Quaternion.LookRotation(dirección de doblado, eje del hueso)`. Unity conserva exacto el
PRIMER vector y proyecta el segundo, así que el eje de cada hueso acababa sobre la línea cadera-tobillo. Resultado: la rodilla visible siempre a 0° (aunque el IK
dijera 112°), y el pie salía más allá de su objetivo, hasta 47 cm bajo el suelo corriendo. Las pruebas pasaban porque medían el IK, no los huesos ni las mallas.
Además: la bota era un cubo centrado que asomaba bajo la suela al inclinarse; de pie la rodilla estaba a 41° (no se notaba con la pierna recta) y la pelvis
bajaba 27 cm al correr porque el apoyo era más largo de lo que alcanza la pierna.

1. Deja que Unity compile (el panel **Test Runner** muestra un ensamblado nuevo: `SevenDays.Tests.Visual`).
2. Window > General > Test Runner > **EditMode** > Run All. Deben pasar todas, incluidas las de `LegVisualTests`: crean el soldado con sus mallas, lo ponen en
   cada fase (caminar, trotar, correr, correr con arma, media rodilla, agachado) y miden las mallas (vértices y `Renderer.bounds`).
   `Report_FinalAngles` escribe en la consola los ángulos medidos.
3. Play en la arena. Pulsa **F7**: aparece un soldado a tu derecha que camina, corre, corre con arma, se pone en media rodilla y se agacha (5 s cada uno),
   visto de lado. Arriba a la izquierda verás, en vivo, la rodilla según el IK, los huesos y la malla (deben coincidir), la distancia de la bota al suelo
   (siempre ≥ 0), la inclinación del torso y la pelvis respecto a de pie; y una tabla por tramo. F7 otra vez lo quita.
4. Con F7 activo, pulsa **F6** y marca **"Esqueleto vs malla"**: huesos en cian, cadena del IK en magenta, eje de cada malla en blanco y, junto a cada
   rodilla, "IK / hueso / malla" en verde si coinciden (rojo si no). Bajo cada pie, "pie +x cm". Mientras está marcado, la consola escribe una línea por frame
   (`[Piernas] ...`) con tobillo (hueso, IK) y vértice más bajo de la malla frente al suelo.
5. En F6, "Pose de locomoción": **Inclinación al correr** (y al trotar, con arma, máxima ≤ 15° y la parte de la pelvis), rodilla de pie, rodilla en el apoyo
   corriendo y caída de la pelvis. Se aplican en vivo. Los valores por defecto están en `Resources/Config/LocomotionPoseSettings` (un asset viejo se pone al
   día solo al cargarlo: `poseVersion`).
6. Graba un clip (Cmd+Shift+5) con F7 y "Esqueleto vs malla": de lado caminando, corriendo, corriendo con arma y en media rodilla.

Medido en las pruebas (mallas): correr rodilla 15° al contactar y 112° en el vuelo, cadera a -0.6 cm de su altura de pie con 4.3 cm de rebote, torso 10°
(4.6° con arma); trotar 17°/83°, torso 6.5°; caminar 60° en el vuelo; media rodilla: delantera 94° con espinilla a 3° de la vertical, rodilla trasera a
5.9 cm del suelo, torso 29.7°; sentadilla 112°/114°. Ningún vértice de la pierna bajo el suelo (mínimo +1.5 cm, la suela).

### Agarre del arma por arma y soldado esbelto
**Diagnóstico (medido en las mallas reales antes de cambiar nada):** la muñeca iba al punto de agarre, así que la palma quedaba a 6.6 cm de la empuñadura en
todas las armas; el arma colgaba de un soporte fijo del jugador (no del pecho): sin apuntar la mano de apoyo no llegaba (14–25 cm; corriendo 27–62 cm, codo
bloqueado), la culata se hundía 11–13 cm en el chaleco y en cobertura el arma atravesaba el pecho 14–15 cm; los dedos atravesaban el arma hasta 4.7 cm.

**Qué cambió:** el arma va anclada al cuerpo (culata en el hueco del hombro; pistola y cuerpo a cuerpo respecto al pecho) con inercia según su peso; cada mano
se coloca por el EJE del mango que agarra (palma a un radio, dedos que lo envuelven sin atravesarlo); uso de manos por arma (pistola a una mano a la cadera y
agarre cruzado apuntando, revólver con el pulgar al martillo al disparar, subfusil con la mano en el cargador, fusil en "C" bajo el guardamano, escopeta en
la bomba que se mueve al bombear, francotirador con la mano bajo la culata y la derecha al cerrojo al ciclar, cuerpo a cuerpo con agarre alto y bajo que suelta
la de apoyo en el golpe, salvo la pala); recarga que nunca cruza el arma (la mano se aparta por su lado, agarra el cargador y lo acompaña); manos con velocidad
y giro limitados (sin saltos); hombro que se adelanta para llegar y baja un poco con las armas pesadas; codos abajo y algo hacia dentro.
Soldado sin mochila, hombros y chaleco ligeros (ProportionSettings). Misma altura: cámara, mira y pies no cambian. El jugador no tiene hitboxes por zona (son de
los zombis); su CharacterController (radio 0.35) no se toca porque de él dependen cobertura, vault y puertas.
Bate, hacha y pala tienen su agarre listo pero aún no están en el botín (sus modelos llegan en el Bloque 4).

1. Deja que Unity compile. Si tus assets de armas son de una versión anterior, se ponen al día solos (datos v5: los puntos de agarre pasan a ser el eje del mango).
   Aparece `Resources/Config/ProportionSettings.asset` (complexión).
2. Window > General > Test Runner > EditMode > Run All. En `GripVisualTests`: manos a menos de 2 cm de su agarre en todas las armas (sin apuntar, apuntando,
   disparando, corriendo y en cobertura), dedos y arma sin atravesar, codos sin hiperextensión, recarga sin cruzar el arma, transiciones sin saltos, cuerpo a
   cuerpo a dos manos que suelta en el golpe, sin mochila y torso esbelto. `Report_GripPerWeaponAndStep` escribe la tabla de medidas en la consola.
3. Play. Pulsa **F7**: tras las piernas vienen las armas, cada una con sacar, sin apuntar, apuntando, disparando (o golpe y golpe cargado), recargando y
   bajándola al correr, con la cámara cerca de las manos y, arriba, la distancia de cada mano a su agarre, dedos dentro, culata y codos. Al final, el soldado de
   frente y de lado. **[** y **]** saltan de tramo; F7 sale.
4. En **F6** > "Complexión (ProportionSettings)": hombros, cadera, grosor general, torso, brazos y piernas, cabeza y largo de brazos y piernas; pulsa
   **"Aplicar complexión"** y el soldado se reconstruye en vivo (los valores por defecto se cambian en el asset).
5. Graba un clip (Cmd+Shift+5) con F7: el agarre de cada arma de cerca, sin apuntar, apuntando y recargando, y el personaje de frente y de lado.

## Rendimiento a pantalla grande (cuello de botella de GPU por resolución)
**Diagnóstico:** el URP Asset del PC tenía 4 cascadas de sombra con suavizado Alto, sombras de luces adicionales, textura opaca y de profundidad (copias de
pantalla completa que nada usaba) y SSAO a resolución completa; filtrado anisotrópico forzado en todas las texturas; y la niebla/humo con partículas de 6 a
48 m que podían cubrir media pantalla (overdraw que crece con la resolución). En una Mac Retina la ventana Game grande renderiza a píxeles nativos.
Banco de pruebas (Unity real en batch, Apple M3, render + espera de GPU, mediana; con el editor abierto hay ±3 ms de ruido): antes 720p 8.7–9.8 ms, 1080p
15.0–16.8 ms, 1440p 16.7–18.3 ms; lo que más cuesta por píxel son las sombras del sol, el SSAO y las partículas grandes.

**Qué cambió:** resolución dinámica por tiempo de GPU (render scale 0.6–1.0 con FSR; la interfaz se dibuja a resolución nativa), gobernador de calidad que
baja y vuelve a subir (QualityDirector), URP con 2 cascadas, sombra suave media, sin sombras de luces adicionales, sin textura opaca ni de profundidad y SSAO a
mitad; bloom a mitad (o a un cuarto); texturas de color comprimidas (BC/DXT) con anisotrópico 4x; niebla y humo con tamaño máximo del 30 % de la pantalla y
fundido junto a la cámara; ayuda del HUD que se oculta a los 10 s. Después (mismo banco, calidad Alta a escala 1.0): 720p 8.3–8.6 ms, 1080p 12.4–13.3 ms,
1440p ~17 ms; a 1440p la resolución dinámica baja a 0.75 (15 ms) o 0.6 (10.8 ms).

### Niveles de calidad (QualityProfile)
| Nivel | Sombras | Suavizado | SSAO | Bloom | Grano | Atmósfera | Resolución dinámica |
|---|---|---|---|---|---|---|---|
| Alta | 50 m, 2 cascadas | medio | a mitad | a mitad | sí | completa | 0.75–1.0 |
| Media | 42 m, 2 cascadas | bajo | a mitad | a un cuarto | sí | 60 % | 0.67–1.0 |
| Baja | 35 m, 1 cascada | duras | no | a un cuarto | no | 30 % | 0.6–0.9 |
| Mínima | 30 m, 1 cascada | duras | no | no | no | lo esencial | 0.6–0.8 |
En **Auto**: primero baja la resolución (en ~0.35 s si la GPU pasa de 15 ms; sube 0.05 tras 3 s por debajo de 11.5 ms; entre medias no cambia: sin parpadeo);
si con la resolución al mínimo el FPS sigue por debajo de 55 durante 2.5 s, baja un nivel; si pasa de 58 durante 4 s con la resolución al máximo, sube uno.
F3 pasa de nivel a mano.

1. **Menú Esc > Calidad:** Auto (recomendado), Alta, Media o Baja; casilla **Resolución dinámica**. Se guardan para la próxima vez.
2. **F6 > panel "Render"** (a la izquierda): ventana e interna, render scale, ms de CPU y **GPU**, draw calls, SetPass, triángulos, shadow casters, luces y
   nivel de calidad. Con la ventana grande, si el FPS no llega a 60 mira primero la GPU: si pasa de 15 ms, la resolución dinámica debería estar bajando.
3. **Perfil de calidad "Demo"** (opcional; el juego lo usa si existe): Edit > Project Settings > Quality > Add Quality Level, nómbralo `Demo`, Render Pipeline
   Asset = `PC_RPAsset`, Anisotropic Textures = Per Texture, Anti Aliasing = Disabled, VSync Count = Don't Sync, y márcalo como predeterminado de la
   plataforma (flecha verde en la fila "Default"). En el URP Asset (Assets/Settings/PC_RPAsset) comprueba: Opaque Texture y Depth Texture desactivados,
   HDR activado, Anti Aliasing (MSAA) Disabled, Shadows: Max Distance 50, Cascade Count 2, Soft Shadows Medium, Additional Lights > Cast Shadows desactivado.
4. **Medir en el editor:** Game view en 1280x720, 1920x1080 y 2560x1440 (desplegable de resolución), Play, F6, apunta FPS y GPU ms con calidad Alta y
   resolución dinámica APAGADA (para comparar) y luego en Auto. Window > Analysis > Profiler (GPU) para ver qué pasa cuesta más.
5. **Medir en una build** (el editor rinde peor): File > Build Profiles > macOS > marca "Development Build" (para ver draw calls en F6) > Build And Run;
   en la build, mismas resoluciones (ventana o pantalla completa), F6.
6. **Banco de pruebas en batch:** Window > General > Test Runner > PlayMode > `RenderBenchmark` (es [Explicit]: se lanza a mano). Escribe en la consola una
   tabla de ms por resolución y nivel.
