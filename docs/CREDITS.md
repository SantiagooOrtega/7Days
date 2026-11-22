# Créditos y origen del arte

**Todo el arte y la animación del juego son procedurales y propios**: se generan por código al ejecutar el juego. No se descarga ni se importa ningún modelo, textura, animación ni sonido externo (ni Mixamo, ni Quaternius, ni Kenney, ni Poly Haven por defecto). Las únicas excepciones son las láminas del cómic y la música (ver al final).

| Qué | Cómo se hace | Código |
|-----|--------------|--------|
| Personajes (esqueleto, cuerpo, armadura) | esqueleto humanoide propio generado por proporciones y piezas colgadas de cada hueso | `Scripts/Procedural/RigMath.cs`, `Scripts/Animation/` |
| Animación del jugador y los personajes | animación procedural por capas (zancada por distancia, IK de pies y manos, muelles críticos), sin clips | `Scripts/Animation/ProceduralRig.cs` |
| Texturas del mapa (asfalto, ladrillo, concreto, metal, cajas...) | generadas por código en hilos de fondo | `Scripts/Procedural/SurfaceGenerator.cs`, `Scripts/Look/SurfaceLibrary.cs` |
| Señales de tráfico, decals, suelo | generadas por código | `Scripts/Look/` |
| Sonidos (disparos, pasos, respiración, zombis, jefes) | sintetizados por código | `Scripts/Weapons/ProceduralAudio.cs`, `Combat/` |
| Soldado (uniforme, chaleco, protecciones, botas, guantes, casco, placa calavera, gorra, capucha) | mallas generadas por piezas con biseles, tornos, cúpulas y láminas con relieve | `Scripts/Procedural/MeshKit.cs`, `SoldierModel.cs`, `SkullFace.cs` |
| Texturas de personajes y armas (camuflaje, cordura, cuero, metal, madera, cinta, calavera pintada...) | atlas generados por código en hilos (color, normales y máscara de metal/oclusión/suavidad) | `Scripts/Procedural/CharacterTextures.cs`, `Scripts/Animation/CharacterArt.cs` |
| Armas (pistola, revólver, subfusil, escopeta, fusil, francotirador, barra) | piezas detalladas generadas por código | `Scripts/Procedural/WeaponModel.cs`, `Scripts/Weapons/GunParts.cs` |
| Zombis (6 tipos, 20 variantes) | cuerpo anatómico, ropa, accesorios y armadura improvisada por código sobre el mismo esqueleto del jugador; una malla con pesos de hueso y 3 niveles de detalle | `Scripts/Procedural/ZombieLook.cs`, `ZombieModel.cs`, `ZombieTypeFeatures.cs`, `ZombieSkinning.cs`, `Scripts/Combat/ZombieBody.cs` |
| Texturas de zombis (piel por tipo, ropa, heridas) | atlas de 512 px generados por código en hilos (color, normales, máscara y emisión) | `Scripts/Procedural/ZombieTextures.cs`, `Scripts/Animation/ZombieArt.cs` |
| Animación de zombis (atacar, reaccionar, aturdirse, emerger, morir) | procedural por código, sin clips; poses finales de la muerte ajustadas al suelo con la malla | `Scripts/Procedural/ZombieMotion.cs` |

**Estilo:** militar moderno táctico, oscuro y sucio, con acentos apagados (naranja, rojo óxido), inspirado en los shooters de coberturas. Diseños 100 % originales:
no se usa ningún personaje, máscara, logo, emblema ni nombre de otros juegos (ni de Call of Duty, ni de Gears of War). La placa calavera es un diseño propio
(cuencas hexagonales inclinadas, nariz en triángulo invertido, pómulos en diagonal, fila de dientes grabados); la "cruz" del médico es un aspa genérica del color de
acento (no el emblema protegido de la Cruz Roja); los parches usan galones abstractos; los números de serie son glifos ilegibles.

**Zombis:** horror post-apocalíptico realista y sucio con los mismos tonos apagados del mapa. Diseños 100 % originales: personas descompuestas genéricas (camisa, bata, traje, mono de obra, harapos), sin criaturas ni
personajes de otros juegos. Violencia contenida: heridas, moratones y sangre seca; un headshot mortal hace desaparecer la cabeza con un golpe seco (queda el muñón del cuello), sin vísceras ni desmembramiento.

Opcional: si alguien añade texturas propias en `Resources/Look` (ver `docs/ASSETS.md`), debe anotar aquí su fuente y licencia.

## Voz de VIGÍA
- Voz sintetizada "Paulina" (español de México) incluida en macOS, generada offline con el comando `say` (`tools/generate_vigia_voice.sh`) y convertida a WAV con `afconvert`. Respaldo: "Mónica" (es_ES). Los WAV se guardan en el repo; el juego no usa `say` ni internet. El filtro de radio, el eco, los bips y la compresión se aplican por código (`Procedural/RadioFx.cs`).

## Láminas del cómic (intro y epílogo)
- `game/Assets/_Project/Intro/Resources/intro_01.png` a `intro_08.png` y `final_01.png`: ilustraciones generadas con IA de imágenes a partir de un guion y prompts propios (`docs/HISTORIA.md`). No usan personajes, logos ni arte de otros juegos.
- `intro_08.png` conserva en sus metadatos la marca C2PA de OpenAI (generada con ChatGPT). Las demás no traen metadatos de origen: **por confirmar** la herramienta de cada una antes de la entrega (el plan era usar Google Gemini).

## Música
- `game/Assets/_Project/Audio/Music/musica_juego.mp3` (unos 3 minutos). **Por confirmar** su autor, su origen y su licencia (por ejemplo CC0 o una licencia libre de regalías). El archivo no trae esos datos en sus etiquetas.

