# 7Days — Juego 3D de supervivencia zombie (proyecto universitario)

Sobrevivir 7 días (1 día = 1 oleada) contra zombies. Jefes en los días 3, 5 y 7.
El usuario tiene poca experiencia en Unity: explica las decisiones de forma simple.

## Estructura
- `backend/` Node.js + TypeScript + Express + Prisma (SQLite en dev, esquema compatible con PostgreSQL).
- `game/` Proyecto Unity (C#). Código propio en `Assets/_Project/`. Cobertura: componente `CoverObject`.
- Documentos de diseño de la Fase 3 en la raíz: `ZOMBIE_LANZADOR.md`, `MAPA_SPAWN_LOOT.md`, `MAPAS_EN_BD.md`.
- `docs/` Plan, arquitectura, API, guía de demo.

## Reglas de trabajo
- Trabajar por fases (ver `docs/PLAN.md`) y detenerse al final de cada una para que el usuario pruebe.
- Commits pequeños y claros. Preguntar antes de decisiones que cambien mucho el alcance; si no, elegir lo más simple y decirlo.
- No crear archivos `.meta` (Unity los genera). No crear escenas a mano: usar código o dar pasos numerados para el editor.
- Todo debe correr en local para la demo. Siempre dar comandos para levantar el backend y pasos para dar Play.
- No depender de assets de pago: primitivas, low-poly gratuito o generado por código.

## Decisiones
- **Cámara/jugador (cambiado):** tercera persona con cámara sobre el hombro (Cinemachine), estilo shooter de coberturas inspirado en Gears of War (sin copiar assets ni personajes). Apuntar con clic derecho, disparo por raycast desde la cámara, recarga y cobertura simple (objetos marcados con componente/tag `Cover`). Reemplaza el FPS planeado antes.
- **Look:** URP + Volume de post-procesado (desaturado, contraste alto, viñeta, bloom suave, grano leve), niebla, partículas de polvo, iluminación baked, mapa de ruinas industriales (Fase 2.5). Tras cada fase visual, pedir captura de Unity al usuario para ajustar.
- **Rendimiento:** objetivo 60 FPS en la Mac del usuario. Object pooling (balas, zombies), límite configurable de zombies simultáneos, LODs, GPU instancing, perfil de calidad "Demo".
- **Assets:** solo gratuitos (Mixamo, Kenney, Quaternius, Polyhaven, Asset Store gratis) o primitivas. Lista y pasos de importación en `docs/ASSETS.md`.
- **IA director:** C# puro, función pura `rendimiento -> parámetros`, explicable y con tests EditMode. Sin internet.
- **IA generativa (cambiado):** Gemini es opcional y OFFLINE: solo desde el script manual `npm run content:generate` (zombies, armas, personajes y opcionalmente 2-3 mapas extra), con zod, reintentos/backoff y límite de peticiones; se guarda en la BD con origen `gemini`. El backend nunca llama a Gemini durante el juego ni hay endpoints que generen en caliente. Sin ejecutar el script, el juego usa el seed. Fallback procedural local. Verificar modelo y límites en la doc actual de Gemini antes de la Fase 5.
- **Mapas en BD (nuevo):** tabla `maps` (nombre, JSON, dificultad base, origen `seed|gemini|procedural`) con seed de 3-5 mapas; `GET /api/maps` y `/api/maps/:id` con JWT. Unity los cachea al iniciar sesión y, si el backend no responde, usa el mapa de pruebas local. El director elige y configura el mapa del día. Ver `MAPAS_EN_BD.md`.
- **Cobertura útil (nuevo):** zombie lanzador con proyectil directo (lo bloquea `CoverObject`), proyectil en arco (ignora cobertura frontal, con marca en el suelo) y flanqueo si el jugador se queda en cobertura más de N segundos. Jefe del día 5 destruye coberturas. Ver `ZOMBIE_LANZADOR.md`.
- **Spawn, casa y loot (nuevo):** spawns por los bordes (mín. 8, validados: distancia, sin línea de visión, sobre NavMesh), casa central con 3+ entradas, loot con pooling y munición inicial escasa. Ver `MAPA_SPAWN_LOOT.md`. El mapa de pruebas actual se mantiene como respaldo.
- **BD:** tablas `users`, `refresh_tokens`, `game_progress`, `rankings`, `generated_content` (tipos `zombie|weapon|character|boss`) y, desde la Fase 3, `maps`. JSON como texto y enums como string validado con zod (portabilidad SQLite/PostgreSQL).
- **Auth:** contraseñas con argon2 (bcrypt si no compila). JWT de acceso de 15 min; refresh token aleatorio, guardado con hash SHA-256, rotativo; reutilizar uno viejo revoca la sesión. zod, rate limiting en login, helmet, CORS.
- **Unity:** token solo en memoria (clase estática `Session`). Contenido como JSON cargado en ejecución con `JsonUtility`; ScriptableObjects solo para valores por defecto.

## Convenciones
- Backend: TypeScript estricto, módulos por dominio en `src/modules/`, validación zod en cada entrada, errores con formato uniforme, tests con Vitest + Supertest.
- Unity/C#: PascalCase para tipos y métodos, `_camelCase` para campos privados, un `MonoBehaviour` por archivo, lógica pura separada de MonoBehaviours para poder testearla.
- Secretos solo en `backend/.env` (ignorado por git). `.env.example` sin valores reales.
- Idioma: documentación y mensajes de commit en español; código en inglés.

## Pendientes
- Unity 6000.3.28f1 (URP 17.3.0, Input System 1.20.1 solo-nuevo, Cinemachine 3.1.7 -> namespace `Unity.Cinemachine`, AI Navigation 2.0.16). Ver `docs/UNITY_SETUP.md`.
- Versión de Node: la máquina tiene v25 (no LTS); si Prisma/argon2 fallan, usar Node 22 LTS.
- Puerto 3000 ocupado en la Mac del usuario por otro programa: el backend usa 3001.
