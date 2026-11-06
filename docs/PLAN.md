# Plan por fases

Al final de cada fase el desarrollo se detiene para que se pruebe.

| Fase | Contenido | Entregable de prueba |
|------|-----------|----------------------|
| 0 | Estructura, CLAUDE.md, git, .gitignore, plan | Repo listo |
| 1 | Backend: BD, auth JWT completa (registro, login, refresh rotativo, logout, perfil), progreso, ranking, tests | `npm test` verde; probar con curl |
| 2 | Unity: `Session`, `ApiClient`, login/registro, jugador TPS con cámara sobre el hombro (Cinemachine), apuntar con clic derecho, disparo, recarga, cobertura simple (agacharse tras objetos marcados), pooling de balas | Dar Play, loguearse, apuntar, disparar y cubrirte |
| 2.5 | Look & Feel: URP + Volume por código (desaturado, contraste alto, viñeta, bloom suave, grano leve), niebla, polvo, iluminación en tiempo real afinada (baked no aplica a mapas construidos en ejecución), mapa de ruinas industriales con primitivas, perfil de calidad "Demo", texturas opcionales de Poly Haven | Captura de Unity para ajustar el look; 60 FPS |
| 3 | **Backend:** tabla `maps` + seed de 3-5 mapas validados con zod + `GET /api/maps` y `/api/maps/:id` (ver `MAPAS_EN_BD.md`). **Unity:** zombies (NavMesh + estados), zombie lanzador con proyectiles directo/arco y flanqueo (`ZOMBIE_LANZADOR.md`), WaveSpawner con spawns por los bordes, casa central y loot con pooling (`MAPA_SPAWN_LOOT.md`), caché de mapas al iniciar sesión con respaldo al mapa de pruebas local, bucle de 7 días, HUD, victoria/derrota | Jugar los 7 días con dificultad fija, usando cobertura contra lanzadores |
| 4 | IA director (rendimiento -> dificultad), tests EditMode, panel explicativo. Además elige el mapa del día y lo configura (lados de spawn, entradas bloqueadas, cantidad de loot, proporción de zombies) y usa la métrica "tiempo en cobertura" y la proporción de lanzadores/arcos | La dificultad y el mapa cambian según tu desempeño |
| 5 | Script offline `npm run content:generate` (Gemini opcional) para variantes de zombies, armas y personajes (y 2-3 mapas extra opcionales), con zod, reintentos/backoff y límite de peticiones; fallback procedural; jefes (días 3, 5, 7), con el del día 5 destruyendo coberturas | Contenido generado guardado en la BD y usado en el juego; sin Gemini el juego usa el seed |
| 6 | Menú, selección de personaje, guardado/carga de progreso, ranking, pulido | Partida completa con cuenta |
| 7 | README, guía de demo, documentación para presentar | Demo reproducible |

## Rendimiento (objetivo 60 FPS)
- Object pooling para balas y zombies; límite configurable de zombies simultáneos (el resto espera en cola de spawn).
- LODs en modelos, GPU instancing en materiales repetidos, luz baked, sombras acotadas.
- Perfil de calidad "Demo" (resolución de sombras media, sin MSAA alto, post-proceso ligero).
- Revisar con el Profiler al cierre de las fases 2.5, 3 y 6.

## Arte y assets
Solo gratuitos o primitivas. Lista y pasos de importación en `docs/ASSETS.md`.
Tras cada fase visual (2, 2.5, 3) se pide una captura de Unity para ajustar el look.

## Arquitectura

```
Unity (cliente) --UnityWebRequest--> Backend Express --Prisma--> SQLite (maps, generated_content, ...)
                                                              ^
                       script manual `npm run content:generate` (Gemini opcional, offline) / seed
```

La IA director corre dentro de Unity, sin internet. El backend **nunca llama a Gemini durante el juego**:
Gemini solo se usa desde un script manual que valida con zod y guarda en la BD. Unity solo lee contenido
ya almacenado. Los mapas están en la BD (seed de 3-5 mapas); Unity los cachea al iniciar sesión y, si el
backend no responde, usa el mapa de pruebas local.

## Cambios de diseño de la Fase 3
Detalle en la raíz del repo:
- `ZOMBIE_LANZADOR.md`: enemigo a distancia que da utilidad a la cobertura (proyectil directo bloqueado por `CoverObject`, proyectil en arco que la ignora, flanqueo si te quedas mucho en cobertura).
- `MAPA_SPAWN_LOOT.md`: spawns por los bordes con validación, casa central con 3+ entradas, loot con pooling y munición inicial escasa.
- `MAPAS_EN_BD.md`: mapas en la base de datos, Gemini solo opcional y offline.

## Director de dificultad (borrador)
`rendimiento = 0.35·vida + 0.25·precisión + 0.25·velocidad_de_limpieza − penalización_por_muertes` (rango 0–1).
Ese valor se convierte en cantidad, velocidad, daño y agresividad de zombies, tipos que aparecen y momento del jefe.
Se refinará y se probará en la Fase 4.
