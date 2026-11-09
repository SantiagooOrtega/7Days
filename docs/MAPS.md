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
