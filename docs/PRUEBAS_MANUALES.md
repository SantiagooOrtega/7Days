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

## 2. Intro en cómic y epílogo

- [ ] Al pulsar **Empezar** en una partida nueva sale la intro con las 8 láminas, en orden.
- [ ] Clic, Espacio, Enter y → avanzan; ← vuelve a la lámina anterior.
- [ ] En las láminas 6, 7 y 8 se oye la voz de VIGÍA con subtítulos, y coinciden con el texto dibujado.
- [ ] El botón **SALTAR** y la tecla Esc saltan la intro y empieza el día 1. Ese Esc **no** abre la pausa.
- [ ] Al **reintentar** un día o al **continuar** una partida, la intro no sale.
- [ ] Esc > Opciones > «Ver la intro otra vez» la vuelve a mostrar y se puede saltar.
- [ ] Al ganar el día 7 sale el epílogo (`final_01`) recorriendo sus 3 viñetas, con la frase de VIGÍA, y
      después la pantalla de victoria. Para probarlo rápido en el editor: N para saltar fases hasta el día 7.
- [ ] Las láminas se ven nítidas a pantalla completa (sin estirarse ni cortarse el texto).

## 3. Combate, cobertura y botín

- [ ] Clic derecho apunta (la cámara se acerca sobre el hombro) y Q cambia de hombro.
- [ ] Clic izquierdo dispara; R recarga; X cambia el modo de disparo en las armas automáticas.
- [ ] 1-5, la rueda del ratón y mantener Tab cambian de arma; G suelta la activa.
- [ ] Un zombi común muere con 2 tiros de pistola a la cabeza; la cabeza desaparece con el headshot.
- [ ] Con la barra metálica, clic izquierdo golpea cuerpo a cuerpo y no gasta balas.
- [ ] C junto a una caja o un muro bajo: arriba sale **CUBIERTO**. A/D desliza a lo largo de la cobertura.
- [ ] En cobertura, clic derecho → **ASOMADO** y el disparo pasa por encima o por el lado (no choca con el
      muro).
- [ ] En cobertura, clic izquierdo sin apuntar → **A CIEGAS**.
- [ ] Espacio pasa por encima de una caja o de un muro bajo.
- [ ] Mantener F muestra el arco de la granada; al soltar, explota a los 2.5 s y daña a los zombis
      cercanos. El contador del HUD baja.
- [ ] Mantener E en un mueble, un coche o una caja: sale la barra de búsqueda y luego el objeto.
- [ ] La munición y los botiquines se recogen al pasar por encima; con la reserva llena sale «Munición
      llena» y el objeto se queda en el suelo.
- [ ] H y B curan con su barra; si un zombi te golpea durante la barra, se cancela y no se gasta el objeto.
- [ ] Con la vida completa, H avisa y no gasta nada.

## 4. Días, jefes, pausa y final

- [ ] Día 1: sale la preparación con su cuenta atrás de 30 s; Enter la salta y aparece «¡OLEADA 1!».
- [ ] Los zombis salen por los bordes del mapa, nunca encima del jugador.
- [ ] Al limpiar la oleada sale «OLEADA SUPERADA», un resumen pequeño que no bloquea y a los 3 s el día 2.
- [ ] Al empezar el día 2 se recupera vida y sale «Nuevo zombi: Corredor».
- [ ] F4 abre el panel del director con la nota del día y por qué cambió la dificultad.
- [ ] Día 3 (en el editor, N para saltar fases): «Hoy viene un JEFE» y, a mitad de la oleada, el **Bruto
      Titán** con su barra grande. Si su carga choca con un muro, queda aturdido.
- [ ] Día 5: el **Lanzador Colosal** rompe las coberturas donde caen sus lanzamientos, y vuelven al día
      siguiente.
- [ ] Día 7: **El Devorador** avisa «¡ONDA EXPANSIVA! ¡SALTA!»; saltando no hace daño. Su núcleo naranja
      se abre a ratos y recibe mucho más daño.
- [ ] Esc abre la pausa: Reanudar, Opciones, Reiniciar día, Volver al menú principal y Salir del juego.
- [ ] En Opciones de la pausa se cambian sensibilidad, calidad, volúmenes, música, subtítulos y tamaño de
      texto, y los cambios se mantienen al volver a abrir el juego.
- [ ] Morir: sale «HAS CAÍDO — DÍA N» con estadísticas. Enter reintenta ese día, Esc vuelve al menú y Tab
      abre la tabla por día.
- [ ] Ganar el día 7: epílogo y luego la pantalla de victoria con las insignias.
- [ ] Tras la partida, el Perfil y el Ranking muestran la partida nueva.
- [ ] Salir a mitad de partida y volver: **Continuar** retoma el día guardado en el mismo mapa.
