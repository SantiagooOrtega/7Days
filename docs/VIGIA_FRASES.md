# Frases de VIGÍA

Catálogo cerrado. Fuente: `game/Assets/_Project/Audio/Vigia/Resources/vigia_frases.txt` (lo leen el juego, `tools/generate_vigia_voice.sh` y las pruebas `VigiaTests`, que fallan si un id no tiene subtítulo, clip o fila aquí). Voz: "Paulina" (es_MX) del comando `say` de macOS; un WAV por variante: `vigia_{id}_{n}.wav`.

Prioridades: **critica** interrumpe lo que suene; **importante** pasa delante de las informativas; **informativa** se descarta si caduca (4 s en cola) o en combate intenso.

| id | categoría | prioridad | variantes |
|----|-----------|-----------|-----------|
| `bienvenida` | partida | importante | Conexión establecida. Soy VIGÍA, tu cámara de apoyo. Intenta no morir. / VIGÍA en línea. Siete días. Yo grabo, tú sobrevives. / Sistema activo. Te sigo de cerca, pero no tanto. |
| `dia_1` | partida | importante | Día uno de siete. / Primer día. Empieza lo bueno. |
| `dia_2` | partida | importante | Día dos de siete. / Segundo día. Sigues aquí, bien. |
| `dia_3` | partida | importante | Día tres de siete. Hoy viene algo grande. / Tercer día. Detecto algo pesado en camino. |
| `dia_4` | partida | importante | Día cuatro de siete. / Cuarto día. Ya vamos por la mitad. |
| `dia_5` | partida | importante | Día cinco de siete. Otro jefe en camino. / Quinto día. Prepárate para visita importante. |
| `dia_6` | partida | importante | Día seis de siete. / Sexto día. Casi lo tienes. |
| `dia_7` | partida | importante | Día siete. El último. Hazlo contar. / Séptimo día. Fin del camino, para alguien. |
| `preparacion` | partida | informativa | Treinta segundos. Busca munición y prepárate. / Tienes treinta segundos. Úsalos bien. / Medio minuto de calma. Aprovéchalo. |
| `cuenta_10` | partida | importante | Diez segundos. / Diez segundos para la oleada. |
| `cuenta_5` | partida | importante | Cinco. / Cinco segundos. |
| `oleada` | partida | critica | Aquí vienen. / Contacto. Oleada entrante. / Se acercan. Muchos. |
| `oleada_superada` | partida | importante | Oleada superada. Respira. / Zona despejada. Por ahora. / Buen trabajo. Siguen llegando mañana. |
| `victoria` | partida | critica | Siete días. Sobreviviste. No me lo esperaba. / Fin de la transmisión. Lo lograste. |
| `derrota` | partida | critica | Señal perdida. Lo siento. / Fin de la grabación. Inténtalo otra vez. |
| `restantes_75` | progreso | informativa | Quedan tres cuartos. / Llevas un cuarto. Sigue. |
| `restantes_50` | progreso | informativa | Mitad de la oleada. / Vas por la mitad. |
| `restantes_25` | progreso | informativa | Queda un cuarto. / Ya casi. Un cuarto más. |
| `quedan_cinco` | progreso | importante | Quedan cinco. / Cinco más y terminamos. |
| `queda_uno` | progreso | importante | Queda uno. / Solo uno más. |
| `bajas_10` | progreso | informativa | Diez bajas. / Diez. Vas calentando. |
| `bajas_25` | progreso | informativa | Veinticinco bajas. / Veinticinco. Ya llevo la cuenta. |
| `bajas_50` | progreso | informativa | Cincuenta bajas. / Cincuenta. Impresionante, de verdad. |
| `bajas_100` | progreso | importante | Cien bajas. Leyenda. / Cien. Necesito más memoria. |
| `combo_1` | combo | informativa | Combo uno. / Combo uno. |
| `combo_2` | combo | informativa | Combo dos. / Combo dos. |
| `combo_3` | combo | informativa | Combo tres. / Combo tres. |
| `combo_4` | combo | informativa | Combo cuatro. / Combo cuatro. |
| `combo_5` | combo | informativa | Combo cinco. / Combo cinco. |
| `muerte_doble` | combo | importante | Muerte doble. / Doble. |
| `muerte_triple` | combo | importante | Muerte triple. / Triple. |
| `muerte_cuadruple` | combo | importante | Muerte cuádruple. / Cuádruple. |
| `racha_imparable` | combo | importante | Racha imparable. / Imparable. |
| `masacre` | combo | importante | Masacre. / Esto es una masacre. |
| `tiro_cabeza` | combo | informativa | Tiro a la cabeza. / En la cabeza. / Limpio. |
| `triple_cabeza` | combo | importante | Triple cabeza. / Tres a la cabeza seguidos. |
| `baja_distancia` | combo | informativa | Buen tiro, de lejos. / Baja a larga distancia. |
| `cuerpo_a_cuerpo` | combo | informativa | Cuerpo a cuerpo. Arriesgado. / Golpe directo. |
| `explosion_cadena` | combo | importante | Explosión en cadena. / Eso salpicó a varios. |
| `primera_sangre` | combo | informativa | Primera sangre del día. / Primera baja de hoy. |
| `cargador_vacio` | recurso | importante | Cargador vacío. / Sin balas en el cargador. |
| `recarga` | recurso | informativa | Recarga. / Recarga ahora. |
| `sin_municion` | recurso | critica | Sin munición. Busca suministros. / No te quedan balas. Busca munición. |
| `municion_baja` | recurso | importante | Munición baja. / Te quedan pocas balas. |
| `suministro_cercano` | recurso | informativa | Nuevo suministro cercano. / Detecto suministros cerca. |
| `granada_fuera` | recurso | importante | Granada fuera. / ¡Granada! (al lanzar con F) |
| `botiquin_usado` | recurso | informativa | Vendado. Sigue. / Botiquín aplicado. |
| `vida_baja` | recurso | importante | Vida baja. / Estás herido. Cúbrete. |
| `estado_critico` | recurso | critica | Estado crítico. Cúrate ya. / Estás muy mal. Cúrate. |
| `inventario_lleno` | recurso | informativa | Inventario lleno. / No te cabe nada más. |
| `jefe_bruto` | amenaza | critica | Jefe detectado. El Bruto. / Algo enorme. Es el Bruto. |
| `jefe_lanzador` | amenaza | critica | Jefe detectado. El Lanzador. / Cuidado arriba. Es el Lanzador. |
| `jefe_final` | amenaza | critica | Jefe detectado. El Final. Esto es todo. / Ahí está. El último jefe. |
| `jefe_furia` | amenaza | critica | El jefe está furioso. / Entró en furia. Aléjate. |
| `explosivo_cerca` | amenaza | importante | Explosivo cerca. / Cuidado, explosivo. Mantén distancia. |
| `lanzador_cerca` | amenaza | importante | Lanzador cerca. / Te están apuntando. Lanzador. |
| `tanque` | amenaza | importante | Tanque en camino. / Viene un tanque. |
| `flanqueo` | amenaza | importante | Te flanquean. / Por el lado. Te rodean. |
| `rodeado` | amenaza | critica | Estás rodeado. / Muchos encima. Sal de ahí. |
| `consejo_precision` | consejo | informativa | Apunta antes de disparar. Las balas no crecen en los árboles. / Precisión baja. Clic derecho ayuda. |
| `consejo_cobertura` | consejo | informativa | Llevas mucho tiempo cubierto. Ellos lo saben. / Esconderse no es un plan. |
| `consejo_recarga` | consejo | informativa | Recarga antes de que se acerquen. / Recarga en calma, no en pánico. |
| `intro_1` | partida | importante | El virus llegó un martes. Para el viernes, la ciudad ya no contestaba. |
| `intro_2` | partida | importante | Tú te quedaste. Alguien tenía que buscar a los que faltaban. |
| `intro_3` | partida | importante | Me encontraste a mí. Una cámara de seguridad con opiniones. Desde entonces te sigo. |
| `intro_4` | partida | importante | Dicen que el rescate llega en siete días. Dicen muchas cosas. |
| `intro_5` | partida | importante | Siete días. Sobrevive a cada oleada. Yo llevo la cuenta. |

## Subtítulos
Abajo y centrados, con el borde inferior al 9 % de la altura (justo sobre la vida y la munición), un recuadro más compacto (como mucho 860 px de ancho y 2 líneas, fondo al 50 %), lejos de la mira y del personaje. La altura se ajusta en Esc ("Altura subtítulos", del 4 al 30 %) y en Opciones del menú (`SubtitleLayout`, `VigiaSettings.SubtitleHeight`).
