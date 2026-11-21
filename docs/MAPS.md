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

**Mapas jugables**: Calle en ruinas y Bosque oscuro (`MapIO.PlayableIds`). El Barrio abandonado sigue en el repo (JSON y generador) pero no se ofrece para jugar.

**Probar**: Play en la escena `Game` y pulsa **M** para alternar entre los 2 mapas jugables; la escena se recarga con el contenido nuevo.

## Niebla (ajuste tras probar)
- **Calle en ruinas**: densidad 0.008 → 0.0108 (+35 %), color un 25 % hacia el del cielo, 3 capas de niebla baja (x1.35, alfa 0.095), 14 bancos de bruma, cielo más apagado y pared de bruma en el horizonte (`horizonFog` 1).
- **Bosque oscuro**: la densidad ya estaba cerca del límite que deja ver un zombi a 25 m, así que sube solo a 0.047 (el máximo, `FogModel.MaxDensity()`) y el +35 % va a la bruma baja (4 capas, x2.45) y a la pared del horizonte (`horizonFog` 1.2).
- **Pared del horizonte** (`Atmosphere.HorizonWall`): 3 anillos de nubes justo fuera del perímetro con degradado del color de la niebla (abajo) al del cielo (arriba); no se ve el borde del mapa. Son partículas: el gobernador de calidad apaga primero los anillos altos y luego el bajo. La densidad de la niebla de Unity no cuesta FPS.
- **Regla**: `MapKit.Validate` rechaza un mapa cuya niebla deje menos del 25 % de visibilidad a 25 m (`FogTests`).

## Luces del fondo
- **Calle en ruinas** (`World/SkylineLights.cs`, lógica pura en `Procedural/WindowGrid.cs`): cada edificio del skyline lleva ventanas en las 3 fachadas que dan al mapa. Una sola textura de 128x128 (64x64 ventanas, ~12 % encendidas en amarillo cálido, naranja o azul pálido) en UN material emisivo compartido; cada fachada toma su trozo y su brillo por MaterialPropertyBlock. Cada 1.3 s se apaga o enciende un par de ventanas. 1 de cada 4 edificios tiene una baliza roja intermitente en la azotea. Sin luces reales, sin sombras y sin colliders: solo emisión + bloom.
- **Bosque oscuro** (`World/FogBeacons.cs`, `distantLights` en el JSON): halos lejanos fuera del perímetro (dos ventanas de una cabaña, una torre vigía con luz cálida y baliza roja, y una luz pálida). La niebla del Bosque taparía cualquier objeto a 60 m, así que son halos con un shader sin niebla y brillo bajo.

## Botín siempre dentro del mapa (bug corregido)
- **Causa**: dos puntos de botín de la Calle (`bush_1`, `bush_2`) salían de arbustos de la vegetación que está detrás del muro (`LootDefaults` no miraba el perímetro). Las tandas de munición y la reposición entre días reutilizaban esos puntos, y `LootManager.Spawn` no validaba nada (drops de zombis, del jefe, pity y resultados de búsqueda caían donde fuera).
- **Regla dura** (`Procedural/LootPlacement.cs` + `LootManager.TryPlace`): todo pickup, antes de crearse, va dentro del perímetro con 1 m de margen, sobre el NavMesh y con camino completo desde el inicio del jugador. Si no, se acerca al centro, se pega al NavMesh o va al punto válido más cercano; si no hay ninguno, se descarta con aviso en la Console. Cada 2 s, lo que acabe fuera o bajo el suelo se recoloca.
- Marcador flotante, flechas del borde, ping y "suministro cercano" de VIGÍA solo cuentan pickups alcanzables.
- **Auditoría**: al construir el mapa y en cada reposición la Console dice `Loot: N válidos, 0 fuera de rango (...)`.
- **Validación de los JSON**: `MapKit.Validate` (C#) y el esquema zod del backend rechazan puntos de botín fuera del perímetro.
- **Pruebas**: `LootPlacementTests` (EditMode) y `MapsTests.Loot_AfterSevenDaysAnd30DropsOutside_...` (PlayMode, los 2 mapas: 7 días de reposición + 30 drops lanzados fuera o bajo el suelo; todos dentro, sobre el NavMesh y alcanzables).

## Casa con segundo piso (Calle en ruinas)
- Datos: `upperFloor`, `upperHeight`, `upperOpenings` y `upperFurniture` en la casa del JSON; `HouseLayoutData.WithUpperFloor` da los valores del mapa 1. Lo construye `HousePlanner.AddUpperFloor` (puro, `HouseUpperFloorTests`).
- **Escalera interior** (habitación este, junto al muro norte): 13 escalones macizos de 0.255 m (el jugador sube 0.3), con colliders. El forjado (suelo de arriba) tiene el hueco de la escalera, con barandilla de 1 m (cobertura baja) en sus bordes.
- **Piso 2**: muros de 2.6 m con 4 ventanas de antepecho a 1 m (línea de tiro de pie, cobertura agachado), muebles de cobertura (2 cajas, mesa, cama), techo de arriba parcialmente caído con losas caídas sobre el suelo del piso 2, y **balcón** al sur (2.2 x 4.2 m, barandilla) para disparar desde arriba y flanquear.
- **NavMesh**: la escalera (1.4 m de ancho) se hornea dentro del NavMesh (escalón 0.255 m < agentClimb 0.75) y los zombis la suben andando, al 60 % de velocidad en pendiente. Escalera, piso 2 y balcón son el área "Upstairs" (coste x4): prefieren el suelo. Prioridad de esquive por tipo (jefe 10, tanque 20, blindado 35, común 50-60, corredor 65) para que no se apilen. El tanque usa otro tipo de agente y un volumen que le cierra la casa: no entra ni sube.
- **Arreglo de paso**: con agentes de 0.5 m de radio, el horneado cerraba la puerta interior y la trasera (1.1 m), y los zombis nunca llegaban a la habitación este. Ahora esas dos puertas miden 1.5 m en la casa del mapa 1, y todas las puertas de casas y refugios llevan un enlace que el director apaga al tapiarlas (como la ventana grande).
- **Botín**: una caja más arriba (`house_upper_crate`, tabla `low`, sin arma garantizada: la escasez se mantiene).
- El refugio del Bosque no lleva piso 2. Es una cabaña pequeña y una escalera no cabe sin rehacerla; se queda como estaba, pero con los enlaces de las puertas.

## Perímetro: trinchera con alambre (Calle) y cerca de troncos (Bosque)
- `ambience.perimeter` en el JSON: `trench` o `logs` (vacío = muro liso). Lógica pura en `Procedural/PerimeterLayout.cs` (`PerimeterTests`), dibujo en `World/PerimeterBarrier.cs`.
- Por tramos de 3 m con variación: sacos de arena (dos filas de fondo) con tablón, concreto roto o cerca metálica rota sobre sacos; en el Bosque, tres troncos apilados. Todos llevan un poste inclinado hacia fuera y una espiral de alambre de espino (cintas cruzadas finas, opacas y sin sombras) hasta ~2 m. Además hay caballos de frisa fuera, banderines rotos, focos (6 con luz tenue sin sombras) y algún barril. Las esquinas están cerradas y nada invade la zona jugable.
- Los muros de siempre siguen como collider invisible (4 m en la Calle, 6 m en el Bosque): bloquean jugador, zombis y balas, y no se saltan.
- Rendimiento: GPU instancing (`Graphics.RenderMeshInstanced`) con una malla y un material por tipo y las matrices preparadas una vez. LOD: alambre, cercas, banderines y caballos de frisa solo a menos de ~57 m de su tramo. F6 muestra "Perímetro: piezas, instancias, lotes, ms CPU" (en pruebas: Calle 3193 instancias, 41 lotes, 0.03 ms; Bosque 719, 23 lotes, 0.01 ms).
- **Segunda ronda (+25 %)**: Calle 0.0108 → 0.0135 (a 25 m se sigue viendo el 89 %), bruma baja x1.7, 17 bancos y horizonte 1.25. Bosque: la densidad sigue en 0.047 (máximo de 25 m) y el +25 % va a la bruma baja (x3.05) y al horizonte (1.5).

## Luna y estrellas
- `ambience.moon` (brillo, 0 = sin luna), `moonSize`, `moonClouds` (nubes/humo delante), `stars` y `milkyWay` en el JSON. Lógica pura en `Procedural/NightSky.cs` (`NightSkyTests`), dibujo en `World/NightSkyRenderer.cs`.
- La luna (textura de cráteres de 128 px y halo) está donde la luz principal, así que sus sombras cuadran. Añade una luz direccional fría muy tenue (0.08) sin sombras, que no cambia la visibilidad. Hay 700 estrellas x densidad en 4 mallas con parpadeo sutil por grupo, una fugaz cada ~25 s y una vía láctea tenue en el Bosque.
- Todo va a 105 m siguiendo a la cámara, con un shader sin niebla. La atenuación por niebla, horizonte y nubes se calcula aparte, así que el coste de GPU es mínimo (unos pocos quads y 4 mallas).
- Calle en ruinas: luna 0.8 entre nubes y humo (0.65), pocas estrellas. Bosque: luna 1.8, más grande (1.25), cielo despejado, más estrellas y vía láctea.
- **Tercera ronda (más niebla, más oscuro)**: Calle 0.0135 → 0.0162 (a 25 m se ve el 85 %), bruma baja x2.04, 20 bancos, horizonte 1.5; Bosque, bruma baja x3.66 y horizonte 1.8 (la densidad sigue al máximo de 25 m). Brillo global -15 % (Calle 1.25 → 1.06, Bosque 1.3 → 1.1, que baja también la exposición del post-procesado) y luz principal -17 %. Ventanas del fondo: ~4 % encendidas (antes 12 %) y a la mitad de brillo.
