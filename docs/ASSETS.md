# Assets gratuitos

Regla: todo debe ser gratis y con licencia que permita uso en un proyecto universitario. Anota la fuente y licencia
de cada cosa que importes en `docs/CREDITS.md`. Los nombres exactos de los packs pueden cambiar en los sitios:
si uno no aparece, busca por la categoría indicada. Descarga solo lo que se pida en cada fase.

## Fase 2 (jugador y armas) — se importan en la Fase 3, junto a los zombies
Los modelos animados necesitan un Animator, así que se integran con los zombies en la Fase 3. No los descargues todavía.

| Qué | Dónde | Licencia | Uso |
|-----|-------|----------|-----|
| Personaje humanoide (p. ej. "X Bot"/"Y Bot") | mixamo.com (cuenta Adobe gratis) | Gratis, uso en proyectos | Modelo del jugador |
| Animaciones: idle, walk, run, strafe con rifle, aim, fire, reload, crouch/cover idle | mixamo.com | Gratis | Animator del jugador |
| Arma (rifle/pistola low-poly) | [Kenney Blaster Kit](https://kenney.nl/assets/blaster-kit) o [Quaternius](https://quaternius.com) | CC0 | Arma del jugador (Fase 3) |

## Fase 2.5 (ambiente: ruinas industriales) — no hay que descargar nada
Las texturas de asfalto, muro de ladrillo, concreto y metal oxidado (color + normales) se **generan por código** al iniciar
(`Scripts/Procedural`, `Scripts/Look/SurfaceLibrary.cs`). Se calculan una sola vez en hilos de fondo y se guardan en caché.
Sus parámetros (colores, óxido, grietas, suciedad, tamaño 512/1024) están en `LookSettings.Surface`.

### Opcional: sustituir una textura procedural por una tuya
Si copias archivos a `Assets/_Project/Resources/Look/` con estos nombres exactos, **tienen prioridad** sobre la procedural de ese material
(y para ese material ya no se genera nada):

| Material | Color | Normales (opcional) |
|----------|-------|---------------------|
| floor (asfalto) | `floor_diff.jpg` | `floor_nor.jpg` |
| wall (ladrillo/revoque) | `wall_diff.jpg` | `wall_nor.jpg` |
| concrete | `concrete_diff.jpg` | `concrete_nor.jpg` |
| metal (tuberías, barriles, farolas) | `metal_diff.jpg` | `metal_nor.jpg` |
| container (chapa corrugada) | `container_diff.jpg` | `container_nor.jpg` |
| crate (caja de madera, una cara completa) | `crate_diff.jpg` | `crate_nor.jpg` |
| cardboard (caja de cartón, una cara completa) | `cardboard_diff.jpg` | `cardboard_nor.jpg` |

Fuentes CC0 de Poly Haven que encajan: [Concrete](https://polyhaven.com/a/concrete) y [Rusty Metal 02](https://polyhaven.com/a/rusty_metal_02)
(resolución 1K, mapas Diffuse y Normal GL). Un script (`LookTextureImporter`) marca los `_nor` como mapas de normales y limita el tamaño a 1024 px.
Para volver a lo procedural basta borrar el archivo.

## Fase 3 (zombies)
| Qué | Dónde | Licencia |
|-----|-------|----------|
| Modelo zombie o humanoide para recolorear | [Quaternius Zombie Apocalypse Kit](https://quaternius.com/packs/zombieapocalypsekit.html) (4 personajes jugables con 20 animaciones, 4 enemigos, FBX/glTF, CC0) o Mixamo | CC0 / gratis |
| Animaciones: idle, walk, run, attack, hit, death | mixamo.com | Gratis |

Los jefes y variantes se obtienen escalando y coloreando el mismo modelo, así que no hacen falta más descargas.

## Cómo importar desde Mixamo (paso a paso)
1. Entra a mixamo.com con tu cuenta Adobe y elige el personaje.
2. Descarga el personaje: Format `FBX for Unity (.fbx)`, Pose `T-pose`.
3. Para cada animación: elige la animación, marca `In Place` si es de movimiento, Format `FBX for Unity`, Skin `Without Skin`.
4. En Unity, crea `Assets/_Project/Art/Characters/` y arrastra los FBX ahí.
5. Selecciona el FBX del personaje, pestaña `Rig`: Animation Type `Humanoid`, Apply.
6. Selecciona cada FBX de animación, pestaña `Rig`: `Humanoid`, Apply. Pestaña `Animation`: marca `Loop Time` en idle/walk/run, Apply.
7. En el personaje, pestaña `Materials`: `Extract Materials...` a una carpeta `Materials`. Si salen rosados, ver el punto siguiente.
8. Materiales rosados: Edit > Rendering > Materials > Convert Selected Built-in Materials to URP.

## Cómo importar packs de Kenney, Quaternius y Polyhaven
1. Descarga el `.zip`, descomprímelo en una carpeta temporal fuera del proyecto.
2. Copia solo los archivos que uses (FBX/OBJ/GLB y texturas) a `Assets/_Project/Art/<Pack>/`.
3. Formatos `.glb`/`.gltf` requieren el paquete glTFast (Window > Package Manager > Add by name: `com.unity.cloud.gltfast`). Prefiere FBX u OBJ si el pack los trae.
4. Texturas Polyhaven: usa Diffuse/Albedo, Normal (GL) y Roughness. En la textura normal marca `Texture Type: Normal map`.
5. Convierte materiales a URP como en el paso 8 de Mixamo.
6. HDRI de Polyhaven: Texture Shape `Cube`, luego crea un material `Skybox/Cubemap` y asígnalo en Window > Rendering > Lighting > Environment.

## Rendimiento al importar
- Marca `Generate Lightmap UVs` en modelos estáticos que vayan a hornearse.
- Texturas: máximo 1024–2048 y compresión activada.
- Activa `Enable GPU Instancing` en los materiales que se repitan mucho.
- Usa LOD Group en modelos con más de ~5k triángulos.
