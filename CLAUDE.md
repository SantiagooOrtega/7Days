# 7Days — Juego 3D de supervivencia zombie (proyecto universitario)

Sobrevivir 7 días (1 día = 1 oleada) contra zombies. Jefes en los días 3, 5 y 7.
El usuario tiene poca experiencia en Unity: explica las decisiones de forma simple.

## Estructura
- `backend/` Node.js + TypeScript + Express + Prisma (SQLite en dev, esquema compatible con PostgreSQL).
- `game/` Proyecto Unity (C#). Se crea en la Fase 2. Código propio en `Assets/_Project/`.
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
- **IA generativa:** Gemini solo desde el backend. Se genera por lotes, se valida con zod y se guarda en `generated_content`. Unity solo lee lo guardado. Fallback procedural local si Gemini falla o no hay cuota. Verificar modelo y límites en la doc actual de Gemini antes de la Fase 5.
- **BD:** tablas `users`, `refresh_tokens`, `game_progress`, `rankings`, `generated_content`. JSON como texto y enums como string validado con zod (portabilidad SQLite/PostgreSQL).
- **Auth:** contraseñas con argon2 (bcrypt si no compila). JWT de acceso de 15 min; refresh token aleatorio, guardado con hash SHA-256, rotativo; reutilizar uno viejo revoca la sesión. zod, rate limiting en login, helmet, CORS.
- **Unity:** token solo en memoria (clase estática `Session`). Contenido como JSON cargado en ejecución con `JsonUtility`; ScriptableObjects solo para valores por defecto.

## Convenciones
- Backend: TypeScript estricto, módulos por dominio en `src/modules/`, validación zod en cada entrada, errores con formato uniforme, tests con Vitest + Supertest.
- Unity/C#: PascalCase para tipos y métodos, `_camelCase` para campos privados, un `MonoBehaviour` por archivo, lógica pura separada de MonoBehaviours para poder testearla.
- Secretos solo en `backend/.env` (ignorado por git). `.env.example` sin valores reales.
- Idioma: documentación y mensajes de commit en español; código en inglés.

## Pendientes
- Versión de Unity del usuario: sin confirmar (define la versión de Cinemachine: 2.x en 2022.3, 3.x en Unity 6, y el template URP).
- Versión de Node: la máquina tiene v25 (no LTS); si Prisma/argon2 fallan, usar Node 22 LTS.
