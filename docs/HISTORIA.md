# Historia de 7 Días

Resumen del mundo y de los personajes. Sirve de referencia para los textos del juego
(intro en cómic, mensajes de radio, nombres de enemigos, títulos de los días).
La intro está en `game/Assets/_Project/Resources/Intro/` (ver `docs/UNITY_SETUP.md`).

## El pueblo y la planta

- **Valle Hondo** es un pueblo industrial que vive de la **Planta 7** de **NEXA Biotecnología**.
- NEXA probaba en secreto la **Cepa C-7**, un regenerador celular. Los sujetos se regeneraban,
  pero perdían la mente.
- Para borrar la evidencia, NEXA ordenó incinerar la planta. La planta **explotó a las 23:47**.

## El contagio

- La ceniza de la explosión llevaba la cepa. Quien la respiró cayó con una **"tos negra"** y se
  levantó horas después.
- Desde entonces se contagia **por mordida**.
- La C-7 **madura en 7 días**. El día 7, **el Devorador** completa la **"floración"**.

## El cerco

- El ejército cerró el valle. El **Coronel Ibarra** habla por radio.
- Al amanecer del **día 8** se aplicará el **Protocolo Tierra Quemada**, salvo que se confirme la
  muerte del Devorador.

## Personajes

| Personaje | Quién es | En el juego |
|-----------|----------|-------------|
| **Sam Duarte** | Rescatista de Defensa Civil. Entró al valle a rescatar a los trabajadores; su equipo no volvió. El nombre sirve para personaje hombre o mujer. | El jugador. Se refugia en la **casa del capataz**, que es la casa central del mapa. |
| **VIGÍA** | La IA de seguridad de la Planta 7. Habla por radio y guía a Sam. | La **IA director**: decide la dificultad de cada día. |
| **Coronel Ibarra** | Jefe del cerco militar. Solo se le oye por radio. | Voz de la radio militar (texto en verde). |
| **Dr. Álvaro Mena** | El paciente cero. | Jefe del día 7: **El Devorador**. |
| **Ramiro "Toro" Salas** | El capataz de la planta, dueño de la casa central. | Jefe del día 3: **Bruto Titán**. |
| **El operador de la grúa** | Trabajador de la planta. | Jefe del día 5: **Lanzador Colosal**. |

## El giro final

Al final se revela que **VIGÍA encendía luces rojas para atraer a los zombis hacia Sam**: lo usaba
de **cebo** para que el Devorador viniera.

## Bestiario (nombres de historia)

El id es el que usa el código (`ZombieIds`); el nombre de historia es el que se usa en textos y
diálogos. El nombre que hoy muestra el HUD (`displayName`) puede ser distinto.

| Id | Nombre de historia |
|----|--------------------|
| `common` | Ceniciento |
| `runner` | Corredor |
| `tank` | Mole |
| `armored` | Acorazado |
| `spitter` | Lanzador |
| `explosive` | Hinchado |
| `boss_brute` (día 3) | Bruto Titán — Ramiro "Toro" Salas, el capataz |
| `boss_launcher` (día 5) | Lanzador Colosal — el operador de la grúa |
| `boss_final` (día 7) | El Devorador — Dr. Álvaro Mena, el paciente cero |

## Títulos de los días

| Día | Título |
|-----|--------|
| 1 | La ceniza |
| 2 | Los recientes |
| 3 | El capataz |
| 4 | Los trajes |
| 5 | Lluvia de concreto |
| 6 | Silencio |
| 7 | La floración |
