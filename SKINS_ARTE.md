Usa tu máximo esfuerzo y la mejor calidad visual posible. Crea mallas detalladas y texturas para el jugador, las armas, los zombis y los jefes, TODO PROCEDURAL POR CÓDIGO, sin descargar ni importar assets externos. Unity 6.3 LTS, URP. No rompas rig, piernas, agarre, hitboxes, cobertura, ADS, recarga, melee, director, estadísticas, la resolución dinámica ni el gobernador de calidad. Un commit pequeño por mejora, push al final de cada bloque, dime el conteo (git rev-list --count HEAD), compila scripts y tests contra las DLL y el NUnit de Unity y corre el Test Runner real como hiciste con las piernas. Antes de empezar dime qué NO podrás verificar sin el editor. Si algo choca, decide y explícame por qué.

ESTILO Y LEGAL
- Militar moderno táctico, oscuro y sucio, con acentos apagados (naranja, rojo óxido). Diseños 100 % ORIGINALES: nada de personajes, máscaras, emblemas, logos ni nombres de Call of Duty, Gears of War ni otros juegos. Documenta en docs/CREDITS.md que todo es procedural y propio.

PRESUPUESTO (obligatorio)
- Mantener 60 FPS con la resolución dinámica actual. Jugador: máx. ~15 000 triángulos y 3-4 materiales. Zombi: máx. ~6 000 triángulos y 1-2 materiales compartidos con variación por MaterialPropertyBlock. Arma: máx. ~3 000. Texturas 512 o 1024 (2048 solo para el casco del jugador si cabe), generadas en hilos de fondo y cacheadas, con mipmaps, compresión de color BC y normales sin compresión. Mide y reporta ms de GPU en el panel Render de F6.

BLOQUE A - Jugador con casco calavera (personaje principal)
- Soldado esbelto, sin mochila, uniforme de combate con camuflaje procedural (manchas digitales o woodland oscuro en verde oliva, negro, gris y marrón), chaleco táctico ligero con portacargadores, radio, correas y velcro, hombreras finas, coderas, rodilleras, guantes tácticos con nudillos reforzados, cinturón con fundas y botas con cordones.
- CASCO INTEGRAL ORIGINAL: casco balístico tipo FAST con rieles laterales, cubierta de tela, montura frontal para visión nocturna (levantada), orejeras de comunicación y una placa facial rígida con forma de cráneo estilizado (diseño propio: cuencas oscuras angulosas, pómulos marcados, mandíbula con dientes grabados en relieve, pintura blanca desgastada sobre metal oscuro, rayones y suciedad). Visor ahumado opcional que cubre las cuencas con un leve brillo. Que no se parezca a ninguna máscara de otro juego.
- Detalle: mallas con biseles, placas, costuras, hebillas, tornillos y bolsillos con volumen. UV por metros, tangentes, sin backface culling, todo parentado a los huesos sin romper articulaciones.
- Texturas: tela con trama, costuras, desgaste en bordes, barro en botas y rodillas, polvo, sangre seca leve, metal rayado en casco y placa. Mapas de color, normales, rugosidad, metalicidad y oclusión.
- 3 variantes jugables con la misma base: "Calavera" (el principal, con el casco anterior), "Explorador" (casco con gorra y pañuelo, camuflaje más claro), "Médico" (casco con cruz propia genérica y brazalete) y opcional "Francotirador" (capucha con red y velo). Cada una en un ScriptableObject con paleta, piezas y estadísticas. Selector de variante en F6.

BLOQUE B - Armas
- Modelo y material por arma (pistola, revólver, escopeta, subfusil, fusil de asalto, francotirador, variantes y barra metálica): cuerpo, cañón, mira (punto rojo o telescópica), cargador, empuñadura, guardamano y culata como piezas separadas; corredera, bomba, cerrojo y cargador se siguen moviendo con las animaciones existentes.
- Materiales: metal pavonado y desgastado en bordes, polímero texturizado, madera con veta, tornillos, grabados de número de serie genéricos, cinta adhesiva en la empuñadura y acento de color por rareza (común, poco común, rara, épica) sin romper el estilo.
- Conserva SightPoint, Muzzle, expulsión de casquillo y GripPoints, y verifica con pruebas que las manos siguen a menos de 2 cm de sus puntos y que la mira ADS se alinea con el SightPoint.

>>> DETENTE AQUÍ: al terminar los bloques A y B, haz push y pídeme capturas antes de seguir.

BLOQUE C - Zombis (los 6 tipos con el mismo rig procedural)
- Pasa los zombis al rig procedural con proporciones por tipo: común, corredor (delgado, encorvado), tanque (enorme, deforme; sigue sin entrar en la casa), blindado (placas y chalecos improvisados), lanzador (abultamientos y bolsas en la espalda), explosivo (hinchado, con venas y pústulas luminosas).
- Texturas: piel pálida y grisácea descompuesta, venas, moratones, heridas abiertas, mandíbula expuesta en algunos, ropa civil y militar rota y sucia, sangre seca. Variación por individuo (tono, ropa, daño, pelo, accesorios) para que no haya clones, y capa de heridas que se revela al perder vida. Ojos con brillo tenue.
- Animación procedural por tipo con el ProceduralRig: tambaleo, caminar arrastrando, correr desbocado, atacar (golpe, doble zarpazo, mordida) sincronizado con la ventana de daño actual, stagger según la zona golpeada, aturdido, morir (varias formas, headshot sin cabeza), levantarse, lanzar, mecha del explosivo, carga y golpe del tanque. Rodillas que se doblan como las del jugador y zancada acoplada a la velocidad del NavMeshAgent, sin patinar.
- Hitboxes ajustados a los huesos sin cambiar multiplicadores ni la tabla de calibración. Rendimiento con 20-30 zombis: LOD de animación y de malla, materiales compartidos, sin alocaciones por frame, pooling intacto.

BLOQUE D - Jefes
- Bruto (día 3), Lanzador Colosal (día 5) y El Devorador (día 7): mallas y texturas propias más detalladas con historia visual (cicatrices, armadura improvisada soldada, partes mutadas, huesos expuestos), telegraph de ataque visible y colores de furia.

BLOQUE E - Integración y pruebas
- Variantes de textura y color por ScriptableObject o JSON validable (para el backend y Gemini offline).
- Pruebas EditMode con mallas reales: cada pieza dentro del presupuesto de triángulos, UV y tangentes válidas, ningún vértice bajo el suelo, rig y hitboxes en cada tipo, memoria de texturas documentada.
- Pasos manuales numerados en docs/UNITY_SETUP.md. Actualiza ANIMATION_HOOKS.md, CREDITS.md y BALANCE.md si algo cambia.

CIERRE
Haz push, dime commits, resultados del Test Runner, ms de GPU y FPS con 20 zombis, y qué quedó sin verificar. Pídeme capturas: cada personaje jugable de frente, lado y espalda (el casco calavera de cerca), cada arma de cerca, un zombi de cada tipo y cada jefe.
