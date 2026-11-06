# Distribución de props (vegetación y relleno) en JSON

El juego coloca árboles, arbustos, hierba, coches y demás desde una lista de piezas. Por defecto la genera el código
(`VegetationLayout`), pero el mismo formato JSON puede venir de un mapa de la base de datos (Fase 3).

## Formato
```json
{
  "items": [
    { "type": "tree_dead",  "x": -31.2, "z": 14.0, "yaw": 80,  "scale": 1.1, "variant": 2, "state": "" },
    { "type": "car_wreck",  "x": 6.4,   "z": -10,  "yaw": 175, "scale": 1,   "variant": 1, "state": "" },
    { "type": "grass",      "x": 4.9,   "z": 3.2,  "yaw": 20,  "scale": 1.2, "variant": 0, "state": "" }
  ]
}
```
Coordenadas en metros; el mapa jugable va de -30 a 30 en X y Z (muros en ±30.5). `yaw` en grados.

| type | variantes | obstáculo (colisión + NavMesh) | dibujo |
|------|-----------|-------------------------------|--------|
| `tree_dead` | 0 esbelto, 1 inclinado, 2 tronco roto, 3 bifurcado | sí (tronco) | GPU instancing |
| `bush` | 0 matorral, 1 ramitas | solo el matorral con scale ≥ 1.1 | GPU instancing, sin sombras |
| `grass` | 0 verde, 1 seca | no | GPU instancing, sin sombras |
| `car_wreck` | 0 a 2 (color, capó/maletero) | sí, y sirve de cobertura | primitivas |
| `pole_fallen` | 0 | sí (pequeño) | primitivas |
| `tire` | 0 | no | GPU instancing |
| `bag` | 0 a 2 (color) | no | GPU instancing |
| `sign_broken` | 0 | no | primitivas |

## Reglas de validación
`scale` entre 0.25 y 4, |x| y |z| ≤ 60, `variant` dentro del rango del tipo, números finitos, tipo conocido, tope total 600 y topes
por tipo (los de `LookSettings.Vegetation`). Lo inválido se descarta con un aviso en la Console; el resto se usa.

## Cómo usarlo
1. En Unity: menú **7Days > Exportar layout de props (JSON)** crea `Assets/_Project/Resources/Layouts/props_default.json`.
2. Edítalo a mano o genera el tuyo y renómbralo a `Assets/_Project/Resources/Layouts/props.json`: el juego lo usará en vez del generado.
3. Para volver al generado, borra `props.json`.

## Para el backend (Fase 3, tabla `maps`)
Esquema zod equivalente, para validar al guardar el mapa:
```ts
const propTypes = ['tree_dead','bush','grass','car_wreck','pole_fallen','tire','bag','sign_broken'] as const;
export const propEntry = z.object({
  type: z.enum(propTypes),
  x: z.number().min(-60).max(60),
  z: z.number().min(-60).max(60),
  yaw: z.number().min(-360).max(360).default(0),
  scale: z.number().min(0.25).max(4).default(1),
  variant: z.number().int().min(0).max(3).default(0),
  state: z.string().max(24).default(''),
});
export const propLayout = z.object({ items: z.array(propEntry).max(600) });
```

## NavMesh (Fase 3)
Árboles, arbustos grandes, coches y postes caídos tienen collider y un `NavMeshModifier` con el área **Not Walkable**. La hierba, la maleza y
las calcomanías no tienen collider y llevan `ignoreFromBuild`. Al hornear con `NavMeshSurface`, usa **Use Geometry: Physics Colliders**: así
los árboles dibujados con instancing (sin malla de escena) también cuentan como obstáculos.
