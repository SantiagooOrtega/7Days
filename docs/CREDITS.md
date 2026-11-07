# Créditos y origen del arte

**Todo el arte y la animación del juego son procedurales y propios**: se generan por código al ejecutar el juego. No se descarga ni se importa ningún modelo, textura, animación ni sonido externo (ni Mixamo, ni Quaternius, ni Kenney, ni Poly Haven por defecto).

| Qué | Cómo se hace | Código |
|-----|--------------|--------|
| Personajes (esqueleto, cuerpo, armadura) | esqueleto humanoide propio generado por proporciones y piezas colgadas de cada hueso | `Scripts/Procedural/RigMath.cs`, `Scripts/Animation/` |
| Animación del jugador y los personajes | animación procedural por capas (zancada por distancia, IK de pies y manos, muelles críticos), sin clips | `Scripts/Animation/ProceduralRig.cs` |
| Texturas del mapa (asfalto, ladrillo, concreto, metal, cajas...) | generadas por código en hilos de fondo | `Scripts/Procedural/SurfaceGenerator.cs`, `Scripts/Look/SurfaceLibrary.cs` |
| Señales de tráfico, decals, suelo | generadas por código | `Scripts/Look/` |
| Sonidos (disparos, pasos, respiración, zombis, jefes) | sintetizados por código | `Scripts/Weapons/ProceduralAudio.cs`, `Combat/` |

**Estilo:** inspirado en los shooters de coberturas (soldados corpulentos, armadura pesada, paleta apagada con acentos naranjas gastados), con diseños 100 % originales: no se usa ningún personaje, logo, emblema ni nombre de otros juegos.

Opcional: si alguien añade texturas propias en `Resources/Look` (ver `docs/ASSETS.md`), debe anotar aquí su fuente y licencia.
