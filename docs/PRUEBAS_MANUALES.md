# Pruebas manuales (versión 1.0)

Lista para revisar el juego a mano antes de una entrega o de unir una rama a `main`. Complementa las pruebas
automáticas (Test Runner en Unity y `npm test` en el backend), que no ven la pantalla.

**Cómo usarla:** copia la lista en el Pull Request o en un mensaje y marca cada punto con `[x]` si va bien.
Si algo falla, anótalo con la plantilla del final.

**Antes de empezar**

- [ ] Backend levantado (`npm run dev` en `backend/`, ver `docs/DEMO.md`).
- [ ] Unity 6000.3.28f1, escena `Assets/Scenes/Game` abierta y Console sin errores rojos.
- [ ] Test Runner > EditMode > Run All: todo en verde.

## 1. Inicio, sesión y menú

- [ ] Al dar Play sale la pantalla de inicio con «PULSA ENTER PARA EMPEZAR» y el aviso de VIGÍA.
- [ ] Enter lleva a «Iniciar sesión» con los campos EMAIL y CONTRASEÑA (8+); Tab pasa de un campo a otro.
- [ ] **Crear cuenta** añade el campo USUARIO (3-20). Con un email nuevo, «Registrarme» entra al menú principal.
- [ ] Usuario de 2 letras o contraseña de menos de 8: sale un error y no se crea la cuenta.
- [ ] Crear otra cuenta con el **mismo email**: sale un error claro y no se cuelga.
- [ ] **Entrar** con una contraseña incorrecta: sale un error y se puede volver a intentar.
- [ ] **Entrar** con la cuenta buena: llega al menú principal.
- [ ] **Modo prueba (sin sesión)**: llega al menú principal sin backend.
- [ ] El menú tiene Jugar, Continuar, Ranking, Perfil, Opciones, Créditos y Salir, y se mueve con las flechas
      y con el ratón.
- [ ] El panel «Información» cambia al pasar por cada opción.
- [ ] **Continuar** sin partida guardada dice «No hay partida a medias.».
- [ ] **Ranking** con sesión muestra las partidas del servidor; sin sesión, las de este equipo.
- [ ] **Perfil** muestra la última partida (mapa, día y si fue victoria).
- [ ] **Opciones** cambia Volumen, Voz de VIGÍA, Subtítulos, Altura de subtítulos y Pantalla completa, y
      «Volver» regresa al menú.
- [ ] **Créditos** muestra los nombres del equipo (hoy salen de `Resources/creditos.txt`).
- [ ] **Jugar** abre la Preparación: se elige personaje y mapa, y **Empezar** arranca la partida.
