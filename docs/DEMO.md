# Guion de la demo (5 minutos, versión 1.0)

Guía para presentar 7 DÍAS en vivo: qué preparar, qué mostrar minuto a minuto y qué hacer si algo falla.
Todo corre en local, sin internet.

## Dos formas de mostrar el juego

| Forma | Ventaja | Límite |
|-------|---------|--------|
| **Editor de Unity** (recomendada para la demo) | Funcionan los atajos de prueba: saltar al día siguiente (N), sacar un jefe (B), etc. Se puede enseñar todo en 5 minutos. | Necesita Unity 6000.3.28f1 en la Mac que presenta. |
| **App de la versión 1.0** (plan B) | No necesita Unity: se descarga desde el enlace del `README.md` (solo macOS). La primera vez se abre con clic derecho > Abrir porque no está firmada. | Los atajos de prueba están desactivados (solo funcionan en el editor o en una Development Build). |

## Antes de presentar (15 minutos antes)

### 1. Backend (para iniciar sesión y el ranking)

La primera vez, copia la plantilla `.env.example` (está en la raíz del repo) a `backend/.env` y rellena el
secreto. `backend/.env` no se sube a git.

```bash
cp .env.example backend/.env
openssl rand -hex 32        # copia el resultado en JWT_ACCESS_SECRET dentro de backend/.env
```

La plantilla ya trae `PORT=3001` y `DATABASE_URL=file:./dev.db`. `JWT_ACCESS_SECRET` debe tener al menos 32
caracteres: si falta, el backend no arranca y avisa con «Configuración inválida en .env».

Luego, en una terminal que se queda abierta toda la demo:

```bash
cd backend
npm install          # solo la primera vez
npm run db:push      # crea o actualiza la base de datos SQLite
npm run dev          # http://localhost:3001
```

Sin backend el juego también funciona: en la pantalla de sesión se elige «Modo prueba (sin sesión)» y el
ranking se guarda solo en la Mac.

### 2. Juego

1. **Editor:** abre la carpeta `game` en Unity Hub, abre la escena `Assets/Scenes/Game` (el menú y el inicio
   de sesión están dentro de esa escena) y revisa que la Console no tenga errores rojos. En la pestaña
   Game, activa **Play Maximized**.
2. Da Play una vez, **crea la cuenta de la demo** (Crear cuenta) y sal. Así en vivo solo hay que entrar.
3. Cierra el resto de programas pesados (navegador con muchas pestañas, videollamada con video).

## Guion minuto a minuto

| Tiempo | Qué se hace | Qué se dice |
|--------|-------------|-------------|
| 0:00 – 0:20 | Pantalla de inicio: «PULSA ENTER PARA EMPEZAR» | «7 DÍAS es un shooter de supervivencia en tercera persona: hay que aguantar 7 días, una oleada por día, con jefes los días 3, 5 y 7.» |
| 0:20 – 0:50 | Enter > **Entrar** con la cuenta de la demo > menú principal | «Las cuentas viven en nuestro backend: Node, Express y Prisma con SQLite. Contraseñas con argon2, token JWT de 15 minutos y refresh token rotativo.» |
| 0:50 – 1:10 | **Jugar** > elegir personaje y mapa > **Empezar** | «Hay dos mapas: Calle en ruinas y Bosque oscuro. Son datos en JSON, los mismos que guarda el backend en su tabla de mapas.» |
| 1:10 – 1:50 | Intro: láminas 1, 2 y 6 (→ para avanzar), luego **SALTAR** | «La historia: la Planta 7 de NEXA explota, la ceniza convierte al pueblo y el ejército cierra el valle. VIGÍA, la IA de la planta, guía a Sam por radio.» |
| 1:50 – 2:50 | Día 1: Enter empieza la oleada. Apuntar (clic derecho), disparar, recargar (R), cubrirse (C) y asomarse | «Cámara sobre el hombro y cobertura: los lanzadores tiran proyectiles directos que la cobertura bloquea, y en arco que no.» |
| 2:50 – 3:30 | **F4**: panel del director. **N** (editor) para pasar de fase hasta el día siguiente | «VIGÍA también es el director: una función pura convierte tu rendimiento en dificultad, con pruebas automáticas. Aquí se ve por qué subió o bajó.» |
| 3:30 – 4:10 | **B** (editor): aparece un jefe de prueba (cicla entre los de los días 3, 5 y 7) | «Cada jefe obliga a jugar distinto; el del día 5 destruye las coberturas y el del día 7, el Devorador, tiene un punto débil que solo se expone a ratos.» |
| 4:10 – 4:40 | **Esc**: menú de pausa > Opciones (VIGÍA, subtítulos, música) > «Volver al menú principal» > **Ranking** | «El progreso y las partidas se suben al backend; con Continuar se retoma el día guardado.» |
| 4:40 – 5:00 | Volver al menú | «Personajes, zombis, armas, texturas, animación y sonidos se generan por código. La versión 1.0 se puede descargar desde el README.» |

Con la app (plan B), en lugar de N y B: muestra el día 1, F4, el menú de pausa y el ranking, y cuenta los
jefes con palabras.

## Teclas útiles

| Tecla | Qué hace | ¿En la app? |
|-------|----------|-------------|
| Enter | Empieza la oleada del día (salta la preparación) | Sí |
| F4 | Panel del director (rendimiento y ajuste de dificultad) | Sí |
| F1 | Muestra u oculta la ayuda de controles | Sí |
| Esc | Menú de pausa: Reanudar, Opciones, Reiniciar día, Volver al menú principal, Salir del juego | Sí |
| N | Salta la fase actual (preparación > oleada > resumen > día siguiente) | No, solo editor |
| B | Jefe de prueba 20 m delante. Ojo: B también usa una venda si tienes | No, solo editor |
| Z / V | Un zombi de prueba / una mini oleada de 8 | No, solo editor |
| M | Siguiente mapa | No, solo editor |
| L | Botín de prueba delante del jugador | No, solo editor |
| T / Y | Recibir daño / curarse | No, solo editor |
| F3 / Shift+F3 | Línea de FPS / ambiente y calidad | No, solo editor |

## Si algo falla (plan B)

| Problema | Qué hacer |
|----------|-----------|
| El backend no arranca | Revisa que exista `backend/.env` (copia de `.env.example`) con `JWT_ACCESS_SECRET` relleno, y que el puerto 3001 esté libre. Si no hay tiempo: «Modo prueba (sin sesión)». |
| Error en Prisma o argon2 al instalar | Usa Node 22 LTS en vez de Node 25. |
| Unity no abre o tarda demasiado | Usa la app de la versión 1.0 (ver la tabla de arriba). |
| Van lentos los FPS | Esc > Opciones > Calidad: baja un nivel. Cierra otras ventanas. |
| Se cuelga todo | Ten a mano una grabación de pantalla de la demo (no se sube al repo). |

## Preguntas típicas

- **¿Dónde está la IA?** En el director de dificultad (C# puro, sin internet, con pruebas EditMode), que
  además habla como VIGÍA. Las láminas del cómic se generaron antes con IA de imágenes y están guardadas
  como archivos (ver `docs/CREDITS.md`).
- **¿Por qué procedural?** Para no depender de assets de pago y que todo sea original.
- **¿Cómo se guarda el progreso?** En la Mac y, con sesión iniciada, también en el backend (progreso y
  ranking por usuario).
