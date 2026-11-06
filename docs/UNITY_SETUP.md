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
