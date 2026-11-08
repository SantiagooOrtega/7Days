Usa tu máximo esfuerzo y la mejor calidad visual posible. Crea mallas, texturas y animación de los zombis y los jefes, TODO PROCEDURAL POR CÓDIGO, sin descargar assets externos. Unity 6.3 LTS, URP. No rompas rig, hitboxes por zona, tabla de calibración de daño (docs/BALANCE.md), NavMesh, IA de estados, tanque sin entrar a la casa, explosivo tipo creeper, lanzador, drops, director, estadísticas, resolución dinámica ni gobernador de calidad. Un commit pequeño por mejora, push al final de cada bloque, dime el conteo (git rev-list --count HEAD), compila scripts y tests contra las DLL y el NUnit de Unity y corre el Test Runner real. Antes de empezar dime qué NO podrás verificar sin el editor. Si algo choca, decide y explícame por qué.

ESTILO Y LEGAL
- Horror post-apocalíptico realista y sucio, con los mismos tonos apagados del mapa. Diseños 100 % originales, sin criaturas ni personajes de otros juegos. Documenta en docs/CREDITS.md. Violencia: heridas y sangre seca sí; sin gore extremo ni desmembramiento explícito (la muerte por headshot puede ser un golpe seco con sangre, sin mostrar vísceras).

PRESUPUESTO (obligatorio)
- 60 FPS con 20 a 30 zombis y la resolución dinámica actual. Zombi normal: máx. ~6 000 triángulos y 1-2 materiales compartidos con variación por MaterialPropertyBlock. Tanque y jefes: hasta ~12 000. Texturas 512 (1024 solo jefes), generadas en hilos de fondo y cacheadas, con mipmaps; color comprimido BC y normales sin comprimir. LOD de malla (3 niveles) y LOD de animación por distancia. Sin alocaciones por frame y con el pooling intacto. Mide y reporta ms de GPU y CPU en el panel F6.

BLOQUE C - Zombis (los 6 tipos con el mismo rig procedural del jugador)
- Migra los zombis del modelo viejo de primitivas al ProceduralRig, con proporciones por tipo desde ProportionSettings:
  * Común: persona normal descompuesta, hombros caídos, cabeza ladeada.
  * Corredor: delgado, músculo tenso, encorvado, brazos largos, piel más tensa.
  * Tanque: enorme (~2.6 m), torso hinchado y deforme, un brazo mucho más grande, piel con placas de tejido cicatrizado. Sigue sin poder entrar en la casa.
  * Blindado: persona con chaleco, placas de metal, puertas o neumáticos atados al cuerpo; la cabeza queda expuesta (vulnerable).
  * Lanzador: abultamientos y bolsas en la espalda y hombros, brazo derecho largo para lanzar.
  * Explosivo: abdomen hinchado con pústulas y venas luminosas naranjas que pulsan cada vez más rápido durante la mecha, y el inflado actual.
- Modelado con volumen anatómico (como el jugador): mandíbula y cráneo, cuello, caja torácica visible en flacos, músculos y tendones, manos con dedos largos y uñas rotas, pies descalzos o con botas rotas. Cada tipo con 3 a 4 variantes de cabeza, pelo (calvo, mechones, pelo largo apelmazado), mandíbula deformada o expuesta en algunos, y una ropa distinta (camisa, uniforme militar, bata, traje, ropa de trabajo, harapos).
- Texturas procedurales: piel pálida grisácea o verdosa con venas, moratones, manchas de livor, heridas abiertas, mordeduras, costras, sangre seca y fresca en boca, manos y pecho; ropa rota, sucia, con barro, quemaduras y desgarros; ojos con velo lechoso y brillo tenue. Mapas de color, normales, rugosidad y oclusión.
- Variación por individuo con MaterialPropertyBlock (tono de piel, mugre, desgaste de la ropa, sangre, tinte de ojos, semilla de heridas) y selección aleatoria de variante y escala (±6 %), de modo que 20 zombis no parezcan clones.
- Capa de daño progresivo: al perder vida se revelan heridas, sangre y desgarros de ropa (en cuartos de vida, 75/50/25 %), por zona golpeada (cabeza, torso, brazos, piernas) con coherencia con las zonas de hitbox. Headshot mortal: la cabeza desaparece con efecto de golpe seco y sangre (como ya existe), y queda el cuerpo cayendo.
- Animación procedural por tipo con el ProceduralRig, con la misma calidad de fluidez que el jugador (spring crítico, rodillas que se doblan, pies que no patinan, zancada acoplada a la velocidad real del NavMeshAgent, variación de velocidad y fase por individuo):
  * Idle: tambaleo, cabeza que cuelga, gruñido de sonido placeholder; caminar arrastrando un pie (cojera según individuo); correr desbocado con torso adelantado (corredor muy agresivo).
  * Atacar: golpe, doble zarpazo, mordida (según tipo) con la ventana de daño existente sincronizada al impacto visual; anticipación y follow-through.
  * Stagger según la zona golpeada (cabeza hacia atrás, torso doblado, brazo, pierna que cede), aturdido, caer, levantarse al aparecer (emerge del suelo o de detrás de un obstáculo), morir (varias formas: desplomarse hacia delante, de espaldas, de rodillas; headshot sin cabeza), trepar o saltar vallas si aplica.
  * Específicos: lanzador (carga el brazo, lanza sincronizado con el proyectil existente), explosivo (inflado, temblor y brillo creciente, sincronizado con la mecha de 1.35 s), tanque (carga con la cabeza baja, golpe de suelo con onda de polvo).
- Hitboxes: ajusta los colliders de cabeza, torso, brazos y piernas a los huesos y proporciones de cada tipo SIN cambiar multiplicadores de daño ni la tabla de calibración. Verifica con pruebas que la tabla BALANCE.md sigue igual (TTK por arma y tipo).
- Rendimiento: LOD de malla y de animación (menos huesos y menor frecuencia a distancia, congelar animación lejana y fuera de pantalla), materiales compartidos, pooling intacto, culling por distancia. Prueba de carga con 30 zombis y reporte de FPS y ms en F6.

>>> DETENTE AQUÍ al terminar el BLOQUE C: haz push y pídeme capturas y prueba de FPS antes de seguir con los jefes.

BLOQUE D - Jefes (Bruto día 3, Lanzador Colosal día 5, El Devorador día 7)
- Mallas y texturas propias, más detalladas (hasta ~12 000 triángulos, 1024 px), con historia visual:
  * Bruto: gigante con hombros enormes, armadura improvisada soldada (placas de chapa, cadenas, un casco de obra incrustado), brazos como mazas, cicatrices de quemaduras, ojos rojos.
  * Lanzador Colosal: torso hipertrofiado, un brazo gigante de lanzar con vendajes y neumáticos, espalda con bolsas y un depósito de residuos, pústulas verdosas.
  * El Devorador: criatura final alta y deformada, mandíbula doble que se abre, columna expuesta con púas, partes mutadas, puntos débiles luminosos que se exponen en ciertos momentos (coherentes con las hitboxes críticas existentes), huesos y tejido a la vista sin gore extremo.
- Animación propia por ataque con telegraph claro (carga, rugido, golpe de suelo, lanzamiento, onda de choque, invocación), cambios visuales en fases de furia (50 % y 25 %: ojos y venas brillantes, pose agresiva, humo o vapor), reacción al daño más contenida (resistencia al aturdimiento ya existente).
- Barra grande y nombre se mantienen; verifica que los telegraphs (marcas en el suelo, avisos) siguen sincronizados con los ataques.

BLOQUE E - Integración y pruebas
- Definición de variantes (tipos, cabezas, ropas, paletas, semillas) por ScriptableObject o JSON con esquema zod validable, para que el backend y Gemini (offline) puedan definir variantes más adelante. Actualiza docs/ZOMBIES.md y docs/BOSSES.md.
- Pruebas EditMode con mallas reales: cada tipo dentro del presupuesto de triángulos, UV y tangentes válidas, ningún vértice bajo el suelo, hitboxes en los huesos, que la capa de daño cambia por cuartos de vida, que la variación genera apariencias distintas, que los LOD reducen triángulos, y que la memoria de texturas queda documentada.
- Panel F6: selector para generar cualquier tipo o jefe, activar el ciclo de animaciones (idle, caminar, correr, atacar, stagger, morir) con cámara lateral, y el reparto de costes.
- Pasos manuales numerados en docs/UNITY_SETUP.md. Actualiza ANIMATION_HOOKS.md, CREDITS.md y BALANCE.md si algo cambia.

CIERRE
Haz push, dime commits, resultados del Test Runner, triángulos por tipo, FPS con 30 zombis y qué quedó sin verificar. Pídeme capturas: un zombi de cada tipo (frente y lado), tres variantes del común, la capa de daño al 100/50/25 %, y cada jefe.
