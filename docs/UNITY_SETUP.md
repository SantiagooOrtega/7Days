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
