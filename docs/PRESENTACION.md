# Presentación del proyecto

Contenido de las diapositivas para presentar 7 DÍAS (unos 10 minutos más la demo en vivo de `docs/DEMO.md`).
Cada diapositiva lleva el título, lo que se muestra y lo que se dice. Los datos están comprobados con el código
de la versión 1.0.

| # | Diapositiva | Qué se muestra | Qué se dice |
|---|-------------|----------------|-------------|
| 1 | **7 DÍAS** | Logo del menú y nombres del equipo | Shooter de supervivencia en tercera persona hecho en Unity 6 con backend propio. |
| 2 | **La idea** | Captura de una oleada | Sobrevivir 7 días, una oleada por día, con jefes los días 3, 5 y 7. Estilo shooter de coberturas: apuntar sobre el hombro, cubrirse y asomarse. |
| 3 | **La historia** | Láminas 2, 4 y 6 de la intro | La Planta 7 de NEXA explota, la ceniza convierte al pueblo y el ejército cierra Valle Hondo. Sam Duarte es el último vivo y VIGÍA, la IA de la planta, lo guía por radio (`docs/HISTORIA.md`). |
| 4 | **Cómo se juega** | Tabla de los 7 días (de `docs/GUIA_JUGADOR.md`) | Preparación de 30 s, oleada de 14 a 56 zombis, 6 tipos que se desbloquean por día y 3 jefes con ataques que avisan antes de golpear. |
| 5 | **Cobertura que importa** | Captura de un lanzador y la marca roja del tiro en arco | La cobertura bloquea los tiros directos, pero si te escondes demasiado el lanzador tira en arco o te rodea, y el jefe del día 5 rompe las coberturas. |
| 6 | **VIGÍA, el director** | Panel F4 | Una función pura convierte tu rendimiento en una nota de 0 a 100 (precisión, cabeza, daño evitado, rapidez y cobertura). Entre 45 y 55 no cambia nada; si no, ajusta vida, velocidad, daño y especiales, como mucho un 25 %. Explica cada cambio y tiene pruebas automáticas. |
| 7 | **Todo generado por código** | Personajes, zombis y armas de cerca | Sin assets externos: mallas, texturas, animación y sonidos salen del código. Mapas en JSON, no en escenas. |
| 8 | **Rendimiento** | Captura con F3 (FPS) | Objetivo 60 FPS en una Mac: pools de balas y zombis, tope de zombis vivos, LODs, GPU instancing y un gobernador que baja la calidad solo si hace falta. |
| 9 | **Arquitectura** | Diagrama de `docs/ARQUITECTURA.md` | Unity habla por HTTP con un backend Node + Express + Prisma (SQLite, compatible con PostgreSQL). Guarda cuentas, progreso y ranking. |
| 10 | **Seguridad** | Lista corta | Contraseñas con argon2, JWT de 15 min, refresh token rotativo guardado como hash (reutilizar uno viejo cierra la sesión), validación con zod, límite de intentos de login, helmet y CORS. |
| 11 | **Pruebas** | Test Runner en verde y `npm test` | Pruebas EditMode y PlayMode en Unity, 22 pruebas del backend y una lista de pruebas manuales (`docs/PRUEBAS_MANUALES.md`). |
| 12 | **Demo en vivo** | El juego | Seguir el guion de `docs/DEMO.md` (5 minutos). |
| 13 | **Qué falta y qué aprendimos** | Lista corta | Pendiente: servir los mapas desde la base de datos, un ranking con puntaje validado por el servidor y el contenido generado offline. Lo aprendido lo cuenta cada miembro del equipo con sus palabras. |
| 14 | **Créditos y preguntas** | Créditos del juego | Origen de las láminas del cómic, la voz y la música en `docs/CREDITS.md`. |

## Consejos para presentar

- Una idea por diapositiva y poco texto: el detalle va en lo que se dice.
- Las capturas se sacan del editor con Play Maximized (ver `docs/DEMO.md`). Las imágenes de la presentación no
  se suben al repo si pasan de 2 MB.
- Ensayar con reloj: diapositivas 1-11 en unos 10 minutos y la demo en 5.

## Preguntas probables y cómo responderlas

| Pregunta | Respuesta corta |
|----------|-----------------|
| ¿Dónde está la inteligencia artificial del juego? | En el director (VIGÍA): C# puro, sin internet. Mide cómo juegas y ajusta el día siguiente con reglas explicables y probadas, no con una caja negra. El panel F4 muestra el porqué. |
| ¿Usaron IA generativa? | El plan incluía un script offline con Gemini para generar variantes, pero en la v1.0 no está: el contenido sale de los valores por defecto y de JSON. Las láminas del cómic se hicieron con IA de imágenes a partir de un guion propio (`docs/CREDITS.md`). |
| ¿Por qué todo procedural? | Para no depender de assets de pago y que el arte sea original. También obliga a que el rendimiento esté controlado desde el código. |
| ¿Qué pasa si se cae el servidor? | El juego sigue: «Modo prueba» y el perfil local. Si el token caduca durante la partida, el cliente lo renueva solo. |
| ¿Se puede hacer trampa en el ranking? | Sí, el puntaje lo calcula el cliente. Para la demo es suficiente; la solución sería que el servidor reciba los eventos de la partida y calcule el puntaje él mismo. |
| ¿Cómo guardan las contraseñas? | Con argon2 (hash lento y con sal). El hash nunca sale en las respuestas. |
| ¿Por qué SQLite? | Es un archivo, fácil de llevar a una demo local. El esquema de Prisma es compatible con PostgreSQL: se cambia el proveedor y la `DATABASE_URL`. |
| ¿Cómo sabemos que el director funciona? | La nota y el ajuste son funciones puras con pruebas EditMode: dado un rendimiento, el resultado es siempre el mismo y nunca se sale de ±25 %. |
| ¿Por qué una sola escena? | El menú, la intro y los mapas se construyen por código y los mapas son datos (JSON). Así no hay escenas que mantener ni conflictos al unir ramas. |
| ¿Qué fue lo más difícil? | Que lo responda quien trabajó en esa parte, con un ejemplo concreto. |
