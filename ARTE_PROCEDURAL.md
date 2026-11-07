Usa tu máximo esfuerzo. Crea el arte y la animación del jugador, los zombis y las armas, TODO PROCEDURAL POR CÓDIGO, sin descargar ni importar assets externos (ni Mixamo, ni Quaternius, ni Kenney). Prioridad absoluta: FLUIDEZ del movimiento. Mantén 60 FPS (Unity 6.3 LTS, URP, Cinemachine 3). No rompas hitboxes, cobertura, vault, ADS, recoil, melee, director ni estadísticas. Un commit pequeño por mejora, push al final de cada bloque, dime el conteo (git rev-list --count HEAD), compila scripts y tests contra las DLL y el NUnit de Unity, y antes de empezar dime qué NO podrás verificar sin el editor. Si algo choca con lo existente, decide, dime por qué y sigue.

ESTILO Y LEGAL
- Inspirado en shooters de coberturas tipo Gears of War: soldados corpulentos y anchos de hombros, armadura pesada, hombreras, casco, chaleco táctico, botas militares, paleta apagada y sucia con acentos naranjas o rojos apagados. Diseños 100 % ORIGINALES: nada de personajes, logos, emblemas ni nombres de ese juego ni de otros.
- Documenta en docs/CREDITS.md que todo el arte es procedural y propio.

BLOQUE 1 - Esqueleto y rig procedural
- Construye una jerarquía de huesos humanoide (pelvis, columna de 3 segmentos, cuello, cabeza, clavículas, brazos, antebrazos, manos, muslos, piernas, pies con punta) compatible con Avatar Humanoid de Unity si es viable, y si no, con un rig propio documentado. Las mallas (torso, extremidades, armadura, casco) se parentan a los huesos o se hacen skinned con pesos simples; que las articulaciones no se rompan.
- Las proporciones se parametrizan (altura, anchura de hombros, grosor) para generar personajes y zombis distintos desde el mismo rig. Los hitboxes por zona existentes se enlazan a estos huesos.
- Un único sistema de animación procedural (ProceduralRig) con capas: LOCOMOCIÓN (pelvis y piernas), TORSO (columna, hombros), BRAZOS Y ARMA (IK) y REACCIONES (impactos, muerte). Cada capa con su propio suavizado.

BLOQUE 2 - Animación del jugador (la prioridad: que se vea fluido)
- Locomoción con IK de piernas: colocación de pies sobre el suelo con raycast, longitud de paso proporcional a la velocidad, ciclo de zancada sincronizado con la distancia recorrida (sin patinar), balanceo de pelvis, rotación de caderas y contrabalanceo de hombros. Idle con respiración y pequeños cambios de peso.
- Estados: idle, caminar, trotar, correr (inclinación del torso hacia delante, brazos flexionados que se balancean, zancada larga), strafe en 8 direcciones (pies que cruzan correctamente, cuerpo orientado a la cámara al apuntar), agacharse (idle y caminar), saltar (impulso, aire con piernas recogidas, aterrizaje amortiguado según la altura de la caída), vault (manos al obstáculo, pierna que pasa, aterrizaje), cobertura (cuerpo pegado, deslizarse A/D, asomarse por encima y por el lado con inclinación, disparo a ciegas con el brazo extendido), recibir daño (retroceso según la dirección del golpe), morir (caída con física ligera: ragdoll simple o secuencia).
- FLUIDEZ: toda transición con suavizado crítico (SmoothDamp o spring con amortiguación crítica), nunca cambios bruscos de pose. Mezcla de estados con pesos continuos (blend tree propio basado en Speed, MoveX, MoveY), sin saltos al cambiar de estado. Anticipación y follow-through: el torso inicia el giro antes que las piernas, la cabeza se adelanta a la dirección, el arma tiene un pequeño retraso elástico al girar.
- Sincroniza el balanceo de la cámara de carrera con los pasos reales (evento de pie al suelo) sin duplicar el efecto actual; si choca, elimina el duplicado.
- Conserva los parámetros del Animator definidos en docs/ANIMATION_HOOKS.md como interfaz pública (Speed, MoveX, MoveY, IsAiming, IsCrouching, IsInCover, IsGrounded, IsJumping, IsVaulting, VerticalSpeed, Fatigue, Fire, Reload, MeleeSwing, MeleeHeavy, MeleeIndex, IsBlindFiring, Exposure): el ProceduralRig los lee, para poder cambiar a clips reales en el futuro sin tocar el resto del juego.

BLOQUE 3 - Sostener y manejar el arma (fluido)
- IK de dos manos: mano derecha en la empuñadura y mano izquierda en el guardamano (Two Bone IK propio o con Animation Rigging si compila), con puntos de agarre por arma. Poses por tipo: pistola a una o dos manos, subfusil y fusil al pecho o hombro, escopeta, francotirador.
- Apuntar: el arma sube a la mira suavemente (ease), la cabeza se alinea con el SightPoint real y la cámara ADS coincide. Correr: arma abajo e inclinada (sprint pose) con transición suave. Cobertura: arma pegada al pecho. Cambiar de arma: guardar y sacar con animación.
- Recarga animada por arma, con eventos que sincronizan sonido y momento de reposición de munición: pistola, subfusil y fusil (cargador que sale, mano a la cintura, cargador nuevo, golpe), escopeta (cartuchos uno a uno), francotirador y revólver (cerrojo o tambor). Se cancela al cambiar de arma, correr o recibir daño, según el diseño actual.
- Disparo: kickback visible, movimiento de corredera o cerrojo, casquillo, destello, leve reacción del torso y los brazos al retroceso, integrado con el recoil existente.
- Melee (barra, bate, hacha, pala): swing normal y cargado con trayectoria curva, anticipación y follow-through, estela, sincronizado con la ventana de daño actual.

BLOQUE 4 - Modelos y texturas procedurales del jugador y las armas
- 3 a 4 personajes jugables distintos (asalto pesado, explorador ligero, médico, francotirador): casco o cabeza, hombreras, chaleco, guantes, botas y mochila propios, paleta y estadísticas propias en ScriptableObject, variantes de color de uniforme.
- Mallas con volumen creíble (biseles, placas, correas, bolsillos, tornillos) usando mallas generadas con UV por metros, tangentes y sin backface culling. Texturas generadas por código (512 o 1024) con tela, costuras, placas rayadas y oxidadas, cuero, suciedad y sangre seca; mapas de color, normales, rugosidad y metalicidad; generación en hilos de fondo y cacheada, como el generador de texturas existente.
- Un modelo y material por arma (pistola, revólver, escopeta, subfusil, fusil de asalto, francotirador, variantes, barra, bate, hacha y pala): cuerpo, cañón, mira, cargador, empuñadura y guardamano separados (para que el cargador se mueva al recargar y el cerrojo al disparar), con metal desgastado, polímero, madera y óxido en bordes. Acento de color por rareza sin romper el estilo. Cada arma con SightPoint, Muzzle y punto de casquillo asignados.

BLOQUE 5 - Zombis procedurales (fluidos y distintos)
- Seis tipos con silueta distinta desde el mismo rig: común, corredor (delgado, encorvado), tanque (enorme, deforme), blindado (placas improvisadas), lanzador (abultamientos), explosivo (hinchado con venas y luz). Variación por individuo (proporciones, tono de piel, ropa, daño, accesorios) para que no se vean clones. Capa de heridas que se revela al perder vida. Ajusta los hitboxes a los huesos de cada tipo sin romper los multiplicadores de daño ni la tabla de calibración.
- Animación procedural por tipo: idle (tambaleo), caminar arrastrando, correr desbocado, atacar (golpe, doble zarpazo, mordida) con la ventana de daño sincronizada con el ataque actual, stagger según la zona golpeada, aturdido, morir (caída, desplomarse, headshot), levantarse al aparecer, y la animación propia del lanzador (lanzar sincronizada con el proyectil), del explosivo (inflado y temblor de la mecha) y del tanque (carga y golpe de suelo).
- La velocidad de la zancada se acopla a la velocidad real del NavMeshAgent (sin patinar). Variación de velocidad y fase por individuo para que no caminen al unísono.
- Rendimiento con 20 a 30 zombis: la animación procedural a distancia usa menor frecuencia de actualización y menos huesos (LOD de animación), materiales compartidos con variación por MaterialPropertyBlock, sin alocaciones por frame y con el pooling intacto. Mide y reporta los FPS.

BLOQUE 6 - Jefes
- Bruto (día 3), Lanzador Colosal (día 5) y El Devorador (día 7): modelos y texturas propias más detalladas con historia visual (cicatrices, armadura improvisada, partes mutadas), animación propia de cada ataque con telegraph claro, rugido y fases de furia.

BLOQUE 7 - Pruebas, depuración y documentación
- Panel de depuración (F6): en vivo la fase de zancada, longitud de paso, pesos de la mezcla, suavizados, IK activo y el coste en ms de la animación procedural, más una casilla para ver los huesos y los objetivos IK (gizmos).
- Pruebas EditMode donde se pueda: suavizado crítico, ciclo de zancada, colocación de pies, límites del IK, cancelación de recarga, acoplamiento de velocidad. Compila contra el NUnit de Unity.
- Pasos manuales numerados en docs/UNITY_SETUP.md. Actualiza docs/ANIMATION_HOOKS.md, docs/CREDITS.md y docs/BALANCE.md si algo cambia.

CIERRE
Haz push, dime el conteo de commits, qué quedó sin verificar y qué costó en FPS. Pídeme capturas y un clip corto con Cmd + Shift + 5: cada personaje jugable, un zombi de cada tipo, cada arma, y corriendo, recargando, apuntando y en cobertura.
