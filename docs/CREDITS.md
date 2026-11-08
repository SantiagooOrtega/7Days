# Créditos y origen del arte

**Todo el arte y la animación del juego son procedurales y propios**: se generan por código al ejecutar el juego. No se descarga ni se importa ningún modelo, textura, animación ni sonido externo (ni Mixamo, ni Quaternius, ni Kenney, ni Poly Haven por defecto).

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

**Estilo:** militar moderno táctico, oscuro y sucio, con acentos apagados (naranja, rojo óxido), inspirado en los shooters de coberturas. Diseños 100 % originales:
no se usa ningún personaje, máscara, logo, emblema ni nombre de otros juegos (ni de Call of Duty, ni de Gears of War). La placa calavera es un diseño propio
(cuencas hexagonales inclinadas, nariz en triángulo invertido, pómulos en diagonal, fila de dientes grabados); la "cruz" del médico es un aspa genérica del color de
acento (no el emblema protegido de la Cruz Roja); los parches usan galones abstractos; los números de serie son glifos ilegibles.

Opcional: si alguien añade texturas propias en `Resources/Look` (ver `docs/ASSETS.md`), debe anotar aquí su fuente y licencia.
