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

## Fase 2.5 (ambiente: ruinas industriales) — OPCIONAL, solo 4 archivos
El mapa funciona sin descargar nada (colores lisos). Estas texturas solo le dan más realismo al concreto y al metal.
Todas son CC0 de Poly Haven: [Concrete](https://polyhaven.com/a/concrete), [Rusty Metal 02](https://polyhaven.com/a/rusty_metal_02).

| Qué descargar | Se guarda como (en `Assets/_Project/Resources/Look/`) |
|---------------|-------------------------------------------------------|
| Concrete, 1K, mapa **Diffuse** (JPG) | `concrete_diff.jpg` |
| Concrete, 1K, mapa **Normal (GL)** (JPG o PNG) | `concrete_nor.jpg` |
| Rusty Metal 02, 1K, mapa **Diffuse** (JPG) | `metal_diff.jpg` |
| Rusty Metal 02, 1K, mapa **Normal (GL)** (JPG o PNG) | `metal_nor.jpg` |

Pasos:
1. Abre cada enlace, en el panel de descarga elige resolución **1K** y el formato JPG, y descarga solo el mapa Diffuse y el Normal GL (no hace falta el resto).
2. Renombra los archivos exactamente como en la tabla (respeta mayúsculas/minúsculas y `_diff` / `_nor`).
3. Arrástralos a la carpeta `Resources/Look` dentro de `Assets/_Project` en la ventana Project de Unity.
4. No toques nada más: un script del proyecto (`LookTextureImporter`) marca los `_nor` como mapas de normales y limita el tamaño a 1024 px.
5. Pulsa Play: las texturas se aplican solas al suelo, muros, barreras y contenedores. Si borras los archivos, el mapa vuelve a los colores lisos.

No uses el HDRI de Poly Haven: el look usa fondo sólido del color de la niebla; un cielo real rompería la atmósfera.

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
