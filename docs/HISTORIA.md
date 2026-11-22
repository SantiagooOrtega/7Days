# Historia de 7 Días

Resumen del mundo, los personajes y el cómic de la versión 1.0. Sirve de referencia para los textos del
juego (láminas, frases de VIGÍA, nombres de enemigos) y para la presentación.

## El pueblo y la planta

- **Valle Hondo** es un pueblo industrial que vive de la **Planta 7** de **NEXA Biotecnología**.
- NEXA probaba en secreto la **Cepa C-7**, un regenerador celular. Los sujetos se regeneraban, pero
  perdían la mente.
- Para borrar la evidencia, NEXA ordenó incinerar la planta. La planta **explotó a las 23:47**.

## El contagio

- La ceniza de la explosión llevaba la cepa. Quien la respiró cayó con una **"tos negra"** y se levantó
  horas después.
- Desde entonces se contagia **por mordida**.
- La C-7 **madura en 7 días**. El día 7 llega **el Devorador**.

## El cerco

- **Hora 6:** el hospital de Valle Hondo deja de responder.
- **Hora 20:** el ejército cierra el valle. El **Coronel Ibarra** habla por radio.
- **Protocolo Tierra Quemada en siete días:** nadie entra y nadie sale.

## Personajes

| Personaje | Quién es | En el juego |
|-----------|----------|-------------|
| **Sam Duarte** | Rescatista de Defensa Civil. Entró al valle a rescatar a los trabajadores; su equipo no volvió. | El jugador (variante de personaje «Sam Duarte»). En el cómic lleva la chaqueta naranja de rescate. |
| **VIGÍA** | La IA de seguridad de la Planta 7. | La cámara flotante que acompaña a Sam y le habla por radio. También es la **IA director**: ajusta la dificultad de cada día. |
| **Coronel Ibarra** | Jefe del cerco militar. Solo se le oye por radio. | Aparece en la lámina 4 anunciando el Protocolo Tierra Quemada. |
| **Dr. Álvaro Mena** | El paciente cero. | Jefe del día 7: **El Devorador**. |
| **Ramiro "Toro" Salas** | El capataz de la planta, dueño de la casa central. | Jefe del día 3: **Bruto Titán**. |
| **El operador de la grúa** | Trabajador de la planta. | Jefe del día 5: **Lanzador Colosal**. |

## La intro en cómic (v1.0)

Sale al empezar cada partida nueva (Jugar > Empezar); no sale al reintentar ni al continuar. Clic,
Espacio, Enter o → avanzan; ← retrocede; Esc o el botón «SALTAR» la omiten.

- Las láminas están en `game/Assets/_Project/Intro/Resources/` (`intro_01.png` a `intro_08.png`) y traen
  el texto dibujado.
- El juego solo añade la voz y los subtítulos de VIGÍA en las láminas 6, 7 y 8, con los ids `comic_6a`,
  `comic_6b`, `comic_6c`, `comic_7` y `comic_8` de `game/Assets/_Project/Audio/Vigia/Resources/vigia_frases.txt`.
- El código está en `game/Assets/_Project/Scripts/Intro/`.

| Lámina | Texto | Qué se ve |
|--------|-------|-----------|
| 1 | «Valle Hondo vivía de la Planta 7. Nadie sabía qué fabricaba NEXA allí dentro.» | El pueblo de noche, con la planta y sus chimeneas al fondo |
| 2 | «23:47. La Planta 7 explotó.» / «Antes del amanecer, la ceniza cubría todo el valle.» | La explosión; una familia mirando por la ventana; una calle cubierta de ceniza |
| 3 | «Quien respiró la ceniza cayó con una tos negra.» / «Horas después...» / «...se levantó.» | Un obrero tosiendo de rodillas; su mano en la ceniza; el obrero ya convertido |
| 4 | «Hora 6: el hospital deja de responder.» / «Hora 20: el ejército cierra el valle.» / Coronel Ibarra: «Protocolo Tierra Quemada en siete días. Nadie entra. Nadie sale.» | El hospital abandonado; el control militar con helicóptero; la radio militar |
| 5 | «Sam Duarte entró al valle a rescatar a los trabajadores. Su equipo no volvió.» / «Ahora es el último vivo.» | Sam sentado en una casa tapiada, con linterna, radio y pistola |
| 6 | VIGÍA: «Señal vital detectada.» / «Soy VIGÍA, la seguridad de la Planta 7.» / «Puedo mantenerte con vida siete días... si haces lo que te diga.» | Una cámara de seguridad con luz roja; la planta en ruinas llena de cámaras; Sam con la radio |
| 7 | VIGÍA: «Tienes seis balas. Que cuenten.» | Sam dispara a una puerta que rompen los zombis («¡BAM!») |
| 8 | «Día 1. Quedan siete.» / VIGÍA: «Sal. Yo te cubro.» | Sam sale a la calle con VIGÍA flotando a su lado; la planta arde al fondo |

## El final (epílogo)

Al ganar el día 7, antes de la pantalla de victoria, sale el epílogo: la lámina
`game/Assets/_Project/Intro/Resources/final_01.png`, con un recorrido por sus tres viñetas, y la frase
`epilogo` de VIGÍA.

1. «Al octavo día, la Planta 7 calló.» Amanece sobre la planta en ruinas.
2. VIGÍA: «Transmisión enviada. Todo el mundo sabrá qué hicieron.» Una radio marca «Frecuencia 27.400 ·
   Mensaje enviado».
3. «Sam Duarte fue el único en salir de Valle Hondo.» Llega un helicóptero.

VIGÍA cuenta al mundo lo que hizo NEXA. En una versión anterior de esta historia VIGÍA usaba a Sam como
cebo para atraer al Devorador; esa idea quedó fuera de la versión 1.0.

Pintadas y carteles que se ven en las láminas, útiles para la presentación:

- «Aún hay gente».
- «Trabajo ayer, cenizas hoy, ¿mañana?».
- «Aquí también éramos personas».
- «Valle Hondo siempre resiste».
- «Ver, grabar, contar para que no se repita».
- «Aquí termina el silencio».

## Bestiario

El id es el que usa el código (`ZombieIds`, en `Scripts/Procedural/ZombieData.cs`). El nombre en el juego es
el que muestra hoy el HUD (`displayName`, `docs/zombies.default.json`). El nombre de historia sirve para
textos y diálogos.

| Id | Nombre en el juego (v1.0) | Nombre de historia |
|----|---------------------------|--------------------|
| `common` | Zombi | Ceniciento |
| `runner` | Corredor | Corredor |
| `tank` | Tanque | Mole |
| `armored` | Blindado | Acorazado |
| `spitter` | Lanzador | Lanzador |
| `explosive` | Explosivo | Hinchado |
| `boss_brute` (día 3) | Bruto Titán | Ramiro "Toro" Salas, el capataz |
| `boss_launcher` (día 5) | Lanzador Colosal | El operador de la grúa |
| `boss_final` (día 7) | El Devorador | Dr. Álvaro Mena, el paciente cero |

## Títulos de los días

No salen en el juego de la versión 1.0; sirven para la presentación o para una versión futura.

| Día | Título |
|-----|--------|
| 1 | La ceniza |
| 2 | Los recientes |
| 3 | El capataz |
| 4 | Los trajes |
| 5 | Lluvia de concreto |
| 6 | Silencio |
| 7 | La floración |
