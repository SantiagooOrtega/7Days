# 7Days


https://drive.google.com/file/d/1mxI75rIg41idLFZ1wy7I0Rd26eXNz4zR/view?usp=sharing

Juego 3D de supervivencia zombi en tercera persona (proyecto universitario). Hay que sobrevivir **7 días**
(un día = una oleada) en Valle Hondo, con jefes los días 3, 5 y 7. Te guía **VIGÍA**, la IA de seguridad de
la Planta 7, que además ajusta la dificultad según cómo juegues.

> La ceniza de la Planta 7 de NEXA convirtió al pueblo. El ejército cerró el valle y en siete días aplicará
> el Protocolo Tierra Quemada. Sam Duarte, rescatista de Defensa Civil, es el último vivo.
> Historia completa: [`docs/HISTORIA.md`](docs/HISTORIA.md).

## Descargar el juego

[Descargar 7Days v1.0](https://github.com/SantiagooOrtega/7Days/releases/tag/v1.0) — solo para macOS. La primera vez, abre la app con clic derecho sobre 7Days > Abrir (no está firmada).

## Video explicativo

[Ver el video explicativo](https://drive.google.com/file/d/1U9rBeNI8plXCDgf9w4k2A67D5MEfpvmp/view?usp=sharing) — hecho por Santiago Ortega Bolaños.

## Qué tiene

- **Shooter en tercera persona:** cámara sobre el hombro, apuntado, recarga y cobertura para asomarse y
  disparar.
- **7 días con oleadas:** 6 tipos de zombis y 3 jefes. El del día 5 destruye las coberturas.
- **IA director:** C# puro, sin internet y con pruebas. Convierte tu rendimiento en dificultad y lo
  explica en un panel (F4).
- **VIGÍA:** voz por radio con subtítulos, comentarios de combate, intro en cómic y epílogo.
- **Menú completo:** inicio de sesión, selección de personaje y mapa, ranking, perfil, opciones, créditos
  y menú de pausa.
- **Backend propio:** cuentas con JWT, progreso, ranking y mapas.
- **Arte procedural:** personajes, zombis, armas, texturas, animación y sonidos se generan por código. Las
  excepciones (láminas del cómic y música) están en [`docs/CREDITS.md`](docs/CREDITS.md).

## Cómo correrlo desde el código

**Backend** (Node 22 LTS recomendado):

```bash
cd backend
npm install
# crea backend/.env con PORT, DATABASE_URL y JWT_ACCESS_SECRET (ver docs/DEMO.md)
npm run db:push
npm run dev          # http://localhost:3001
npm test             # pruebas del backend
```

Sin backend también se puede jugar: en la pantalla de sesión elige «Modo prueba (sin sesión)».

**Unity** (6000.3.28f1):

1. Abre la carpeta `game` en Unity Hub.
2. Abre la escena `Assets/Scenes/Game` y da Play. El menú y el inicio de sesión están dentro de esa escena.
3. Pruebas: Window > General > Test Runner > EditMode (y PlayMode) > Run All.

Configuración de una sola vez y pruebas manuales: [`docs/UNITY_SETUP.md`](docs/UNITY_SETUP.md).

## Controles

| Acción | Tecla |
|--------|-------|
| Moverse / correr / agacharse / saltar | WASD / Shift / Ctrl / Espacio |
| Apuntar / disparar / modo de disparo | Clic derecho / clic izquierdo / X |
| Recargar / cambiar de arma / soltar arma | R / 1-5 o rueda del ratón / G |
| Cobertura | C junto a una caja o un muro bajo; A/D te deslizas; Q cambia de hombro o de lado |
| Recoger / buscar | E / mantener E en cajas, coches, muebles y escombros |
| Botiquín / venda | H / B |
| Empezar la oleada | Enter |
| Panel del director / ayuda / pausa | F4 / F1 / Esc |

Los atajos de prueba (saltar fase, jefe de prueba...) solo funcionan en el editor: ver
[`docs/DEMO.md`](docs/DEMO.md).

## Estructura

| Carpeta | Qué hay |
|---------|---------|
| `backend/` | Node.js + TypeScript + Express + Prisma (SQLite en desarrollo), pruebas con Vitest |
| `game/` | Proyecto de Unity 6 (URP). Código propio en `game/Assets/_Project/` |
| `docs/` | Plan, API, balance, jefes, historia, créditos y guion de la demo |
| `tools/` | Script para generar la voz de VIGÍA (macOS) |

## Documentación

- [`docs/HISTORIA.md`](docs/HISTORIA.md): mundo, personajes, intro en cómic, epílogo y bestiario.
- [`docs/DEMO.md`](docs/DEMO.md): guion de la demo de 5 minutos.
- [`docs/PLAN.md`](docs/PLAN.md): fases y arquitectura.
- [`docs/API.md`](docs/API.md): endpoints del backend.
- [`docs/BOSSES.md`](docs/BOSSES.md) y [`docs/BALANCE.md`](docs/BALANCE.md): jefes y balance.
- [`docs/UNITY_SETUP.md`](docs/UNITY_SETUP.md): configuración de Unity y pruebas manuales.
- [`docs/CREDITS.md`](docs/CREDITS.md): origen del arte, la voz, las láminas y la música.
