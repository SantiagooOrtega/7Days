# Arquitectura

Vista general de cómo está hecho 7 DÍAS en la versión 1.0: las piezas, dónde está cada cosa y cómo se hablan.
El detalle de cada sistema está en su documento (`API.md`, `MAPS.md`, `BALANCE.md`, `BOSSES.md`...).

## Las piezas

```
┌──────────────────────────── Unity (escena Game) ────────────────────────────┐
│  Menú (FrontEnd) ─► Intro ─► Bucle de 7 días (DayLoop) ─► Victoria/Derrota   │
│                                   │                                          │
│        Jugador · Armas · Zombis · Jefes · Botín · HUD · VIGÍA (voz)         │
│                                   │                                          │
│      Director de dificultad (C# puro, sin internet, con pruebas)            │
│                                                                              │
│  Datos locales: Resources/Maps/*.json · perfil.json (perfil y ranking local) │
└───────────────────────────────────┬──────────────────────────────────────────┘
                                    │ HTTP + JSON (UnityWebRequest), token JWT
                                    ▼
┌────────────── Backend (Node.js + TypeScript + Express) ─────────────────────┐
│  /api/auth (registro, login, refresh, logout) · /api/me                     │
│  /api/progress (día y estadísticas) · /api/rankings                         │
│  zod en cada entrada · argon2 · helmet · CORS · límite de intentos de login │
└───────────────────────────────────┬──────────────────────────────────────────┘
                                    │ Prisma
                                    ▼
             SQLite en desarrollo (backend/prisma/dev.db; compatible con PostgreSQL)
             Tablas: users, refresh_tokens, game_progress, rankings, generated_content
```

- **Todo corre en local**, sin internet. Sin backend el juego también funciona en «Modo prueba».
- **Una sola escena** (`Assets/Scenes/Game`): el menú, la intro y la partida se crean por código. Los mapas no
  son escenas sino JSON que se convierten en el nivel al empezar.
- **Sin assets externos:** personajes, zombis, armas, texturas, animación y sonidos se generan por código.

## Dónde está cada cosa (`game/Assets/_Project/Scripts/`)

| Carpeta | Qué hay |
|---------|---------|
| `Procedural/` | La lógica pura, sin Unity, y por eso con pruebas: reglas de los 7 días (`DayCycle.cs`), nota y director (`Performance.cs`), daño (`CombatModel.cs`), botín, jefes (`BossBrain.cs`), mapas (`MapData.cs`, `MapLibrary.cs`) y la generación de mallas y texturas |
| `World/` | Lo que monta el nivel y el bucle de la partida: `DayLoop.cs`, `MapIO.cs` (lee los JSON), botín, bosque, perímetro, cielo |
| `Combat/` | Zombis y jefes en Unity: movimiento, ataques, cuerpo, métricas para el director |
| `Player/` | Jugador: movimiento, cámara sobre el hombro, cobertura, disparo, granadas |
| `Weapons/` | Armas y sonido sintetizado de los disparos |
| `Animation/` | Esqueleto y animación procedural (sin clips) |
| `Look/` | Post-procesado, niebla, luces, texturas de superficies y calidad |
| `UI/` | Menú, pausa, HUD, pantallas de muerte y victoria, estilo táctico común |
| `Intro/` | Reproductor del cómic de la intro y del epílogo |
| `Vigia/` | Voz y subtítulos de VIGÍA |
| `Net/` | Cliente del backend (`ApiClient`, sesión, progreso) |
| `Core/` | Entrada de teclado y ratón, audio, atajos de prueba |

Las pruebas están en `game/Assets/_Project/Tests/` (EditMode, EditModeVisual y PlayMode) y las del backend en
`backend/src/tests/`.

**Regla del proyecto:** la lógica que se puede probar va en `Procedural/` como C# puro; los `MonoBehaviour`
solo la conectan con Unity. Así se puede probar el director, los días o el daño sin abrir una escena.
