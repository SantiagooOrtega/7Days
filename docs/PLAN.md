# Plan por fases

Al final de cada fase el desarrollo se detiene para que se pruebe.

| Fase | Contenido | Entregable de prueba |
|------|-----------|----------------------|
| 0 | Estructura, CLAUDE.md, git, .gitignore, plan | Repo listo |
| 1 | Backend: BD, auth JWT completa (registro, login, refresh rotativo, logout, perfil), progreso, ranking, tests | `npm test` verde; probar con curl |
| 2 | Unity: `Session`, `ApiClient`, login/registro, jugador FPS, movimiento, disparo, recarga | Dar Play, loguearse y disparar |
| 3 | Zombies (NavMesh + estados), oleadas, bucle de 7 días, HUD, victoria/derrota | Jugar los 7 días con dificultad fija |
| 4 | IA director (rendimiento -> dificultad), tests EditMode, panel explicativo | La dificultad cambia según tu desempeño |
| 5 | Gemini + zod + reintentos/backoff + fallback procedural; mapas, zombies, armas, personajes, jefes (días 3, 5, 7) | Contenido generado y usado en el juego |
| 6 | Menú, selección de personaje, guardado/carga de progreso, ranking, pulido | Partida completa con cuenta |
| 7 | README, guía de demo, documentación para presentar | Demo reproducible |

## Arquitectura

```
Unity (cliente) --UnityWebRequest--> Backend Express --Prisma--> SQLite
                                         |
                                         +--> Gemini (solo backend, por lotes) | fallback procedural
```

La IA director corre dentro de Unity, sin internet. El contenido generativo se pide al backend,
que lo valida y lo guarda; durante la partida Unity solo lee contenido ya almacenado.

## Director de dificultad (borrador)
`rendimiento = 0.35·vida + 0.25·precisión + 0.25·velocidad_de_limpieza − penalización_por_muertes` (rango 0–1).
Ese valor se convierte en cantidad, velocidad, daño y agresividad de zombies, tipos que aparecen y momento del jefe.
Se refinará y se probará en la Fase 4.
