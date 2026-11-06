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
