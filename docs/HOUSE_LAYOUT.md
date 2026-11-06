# Casa central (refugio)

La casa se define en JSON (el mismo formato que traerá un mapa de la base de datos). Sin archivo se usa `HouseLayoutData.Default()`.
La autopista pasa justo por el centro del mapa (|x| < 4), así que la casa va **pegada al centro, en el lado este** (centro x=11, z=0.5), con la puerta principal
mirando a la vía. Para moverla, cambia `x`, `z` y `yaw` (múltiplos de 90).

## Qué tiene por defecto
- 10 x 8 m, muros de 3 m con bisel, **2 habitaciones** (muro interior en x=0 con un paso de 1.1 m).
- **3 entradas** en muros distintos: puerta principal (oeste, mira a la autopista), puerta trasera (este) y **ventana grande** (sur, antepecho 0.9 m,
  hueco libre de 1.3 m: se salta con el vault).
- **4 ventanas pequeñas** con línea de tiro (antepecho a 1.15 m y hueco de 0.8 m: se ve y se dispara, pero no se pasa).
- **Techo parcialmente caído**: losas de 25 cm de grosor (sin backface culling), 3 vigas, alguna losa inclinada y 1-2 caídas en el piso como cobertura.
- **9 muebles** (caja, mesa, estante, sofá, cama, armario), todos `CoverObject`; los de 0.5 a 1.3 m de alto son `Vaultable` por su altura.
- **4 antorchas** (3 dentro y 1 afuera junto a la puerta principal): su fuego va en el bloque de fuego.
- Colliders de **caja** en piso, muros, marcos, techo, vigas, tablones y muebles: el jugador no los atraviesa y las balas se detienen.

## Formato
```json
{
  "x": 11, "z": 0.5, "yaw": 0, "width": 10, "depth": 8, "wallHeight": 3, "wallThickness": 0.3,
  "interiorX": 0, "roofSeed": 5, "roofCollapse": 0.4,
  "openings": [
    { "id": "front_door", "wall": "west", "offset": 0.5, "width": 1.2, "height": 2.2, "sill": 0, "kind": "door", "vaultable": false, "blockedDays": [] },
    { "id": "big_window", "wall": "south", "offset": -1.5, "width": 2.2, "height": 1.3, "sill": 0.9, "kind": "window_large", "vaultable": true, "blockedDays": [3, 4] }
  ],
  "furniture": [ { "kind": "crate", "x": -0.9, "z": 3.2, "yaw": 0 } ],
  "torches":   [ { "x": -4.7, "z": 2.5, "y": 2 } ]
}
```
- `wall`: `north` (+Z), `south`, `east` (+X), `west`, `interior`. `offset` = metros desde el centro del muro. `kind`: `door`, `window`, `window_large`.
- `blockedDays` (1 a 7): días en que el director tapa la abertura con tablones. `HouseController.SetDay(día)` los aplica; `SetBlocked(id, true/false)` lo hace a mano.
- Una puerta necesita `sill` 0, alto ≥ 1.9 y ancho ≥ 0.8. Las aberturas no se solapan ni tocan las esquinas.

## Cómo usarlo en Unity
1. Menú **7Days > Exportar casa (JSON)** crea `Assets/_Project/Resources/Layouts/house_default.json`.
2. Edítalo y renómbralo a `house.json`: el juego lo usará (si es inválido se avisa en la Console y se usa la de por defecto).
3. Para volver a la de por defecto, borra `house.json`.

## NavMesh (Fase 3)
- Muros, marcos, tablones y muebles llevan `NavMeshModifier` con el área **Not Walkable**; el techo se **ignora** (`ignoreFromBuild`).
- Cada **puerta** lleva un `NavMeshModifierVolume` con el área **Walkable** (zona transitable aunque el marco o un muro vecino sea obstáculo).
- La **ventana grande** lleva un `NavMeshLink` (de adentro hacia afuera, ancho de la ventana); `LookSettings.House.WindowsWalkable` lo activa y el director lo apaga al bloquear la ventana.
- Al hornear con `NavMeshSurface` usa **Use Geometry: Physics Colliders**.

## Esquema zod (para la tabla `maps` del backend)
```ts
const wall = z.enum(['north','south','east','west','interior']);
export const houseOpening = z.object({
  id: z.string().min(1).max(32), wall, offset: z.number().min(-10).max(10),
  width: z.number().min(0.5).max(4), height: z.number().min(0.4).max(4), sill: z.number().min(0).max(3),
  kind: z.enum(['door','window','window_large']), vaultable: z.boolean().default(false),
  blockedDays: z.array(z.number().int().min(1).max(7)).default([]),
});
export const houseLayout = z.object({
  x: z.number().min(-30).max(30), z: z.number().min(-30).max(30), yaw: z.number().refine(v => v % 90 === 0),
  width: z.number().min(6).max(20), depth: z.number().min(5).max(16), wallHeight: z.number().min(2.4).max(4.5), wallThickness: z.number().min(0.15).max(0.6),
  interiorX: z.number(), roofSeed: z.number().int(), roofCollapse: z.number().min(0).max(1),
  openings: z.array(houseOpening).max(24),
  furniture: z.array(z.object({ kind: z.enum(['crate','table','shelf','sofa','bed','cabinet']), x: z.number(), z: z.number(), yaw: z.number() })).max(40),
  torches: z.array(z.object({ x: z.number(), z: z.number(), y: z.number().default(2) })).max(12),
});
```
