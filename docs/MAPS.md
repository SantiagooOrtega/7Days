# Mapas como datos (JSON)

Los mapas no son escenas: la escena `Game` construye el mapa a partir de un JSON. Así hay una sola escena y un solo Build Profile, y el backend puede servir los mapas (tabla `maps`).

- **Formato**: `Procedural/MapData.cs` (C#, JsonUtility) y `backend/src/modules/maps/maps.schemas.ts` (zod). Las dos validaciones aplican las mismas reglas (`MapKit.Validate` en C#).
- **Respaldo local**: `game/Assets/_Project/Resources/Maps/{id}.json`. Sin backend, el juego usa estos.
- **Exportar el mapa 1** (si cambias el generador): menú **7Days > Mapas > Exportar mapa 1 (Calle en ruinas) a JSON**. La prueba `MapJsonTests` avisa si el JSON y el generador dejan de coincidir.

| Campo | Qué es |
|-------|--------|
| `id`, `name`, `baseDifficulty` (1-5) | identificador, nombre visible y dificultad base |
| `halfSize`, `seed` | el mapa va de -halfSize a +halfSize; semilla de las piezas sueltas y de los tonos |
| `playerStart` | x, y, z y giro del jugador |
| `props` | piezas del kit **en orden** (el orden fija los tonos): `kind` (ground, road, wall, crumbles, barrier, crate, container, pillar, beam, roof, brickwall, pipe, barrel, fire_barrel, rubble_field, lamp), `name`, `pos`, `rot` (grados), `size`, `color`, `surface` (textura procedural), `cover`, `count` (piezas sueltas con semilla), `rank` (farolas: orden en que parpadean) |
| `houses` | casas o refugios (`HouseLayoutData`: medidas, entradas bloqueables, muebles, antorchas); 3 entradas o más |
| `spawns` | 8 o más puntos de aparición en el borde (`n`, `s`, `e`, `o`), en 3 lados o más |
| `vegetation` | árboles, arbustos, hierba, coches, neumáticos, fogatas y señales (`PropLayoutData`) |
| `loot` | botín (`LootLayoutData`: tablas, drops, puntos y topes de escasez) |
| `ambience` | hora, niebla (color y densidad), ambiente, sol, brillo y efectos (niebla baja, bruma, ceniza, humo, chispas, rayos de luz, farolas que parpadean, grietas, maleza, charcos, sangre, horizonte, marcas viales, polvo) |

**Mapa 1 "Calle en ruinas"**: es el mapa de antes, pasado a datos sin cambiar nada. Se comprobó con una "foto" del nivel (`MapSnapshot`, PlayMode a mano): 817 objetos, 44 luces, la niebla, el botín, la vegetación y los 76 sistemas de partículas salen **idénticos** antes y después del cambio. Solo varía el desplazamiento de textura de los edificios lejanos, que ya cambiaba en cada ejecución (usa el azar global de Unity).

## Mapas 2 y 3 (mismo kit, sin escenas nuevas)
Generadores puros en `Procedural/MapLibrary.cs` (probados en `MapLibraryTests`); se exportan con **7Days > Mapas > Exportar los 3 mapas a JSON**. Botín: las mismas tablas, drops y topes que el mapa 1 (14-17 puntos y 3 armas garantizadas).

| Mapa | Tamaño | Qué tiene |
|------|--------|-----------|
| 2 "Barrio abandonado" (`barrio_abandonado`, dificultad 2) | 80 x 80 m | 4 casas pequeñas con entradas que el director tapa en días distintos, 2 calles cruzadas, 6 coches (1 ardiendo), vallas de madera con huecos (flanqueos), barreras y cajas en el cruce, más árboles, arbustos y hierba, niebla más densa y viento |
| 3 "Bosque oscuro" (`bosque_oscuro`, dificultad 3) | 90 x 90 m | noche de luna azul fría, niebla baja y densa (se ve a 35-40 m), 550 pinos y árboles muertos (5 variantes, 3 LOD), 22 troncos caídos y 30 rocas con musgo (cobertura), helechos, maleza, senderos de tierra y barro, 5 claros, una cabaña (refugio con entradas bloqueables), una caravana y un coche oxidados, 3 fogatas y 3 faroles como únicas luces, luciérnagas y sonido de viento, grillos y crujidos |

**Piezas nuevas del kit**: `soil` (suelo de tierra), `boundary` (límite invisible), `fence` (valla, cobertura), `trail` (sendero), `boulder` (roca, cobertura), `trunk` (tronco caído, cobertura), `caravan` y `lantern` (farol con luz). Vegetación nueva: `pine`, `tree_tall` y `fern`.

**Rendimiento del bosque** (`World/ForestRenderer.cs`): GPU instancing por variante y LOD (LOD 0 hasta 16 m, LOD 1 hasta 30 m, después una cruz de 4 caras); no dibuja lo que queda detrás de la cámara ni más allá de la niebla (~47 m); tope de árboles visibles (420; si se supera, la distancia de corte baja sola); sombras solo en los árboles a menos de 22 m; helechos y hierba sin sombras. El gobernador de calidad recorta por niveles: distancia x1 / 0.85 / 0.7 / 0.55, tope 420 / 340 / 260 / 180 y sombras hasta 22 / 15 / 9 / 0 m. Con 30 zombis (Apple M3, 1080p, render sincronizado): bosque 13.6 ms y barrio 14.0 ms; el frame sigue a 60 FPS. En F6 > RENDER, la línea "Bosque" muestra los árboles visibles por LOD, la distancia de corte y los ms de CPU.

**Probar**: Play en la escena `Game` y pulsa **M** para pasar al siguiente mapa (Calle en ruinas -> Barrio abandonado -> Bosque oscuro); la escena se recarga con el contenido nuevo.
