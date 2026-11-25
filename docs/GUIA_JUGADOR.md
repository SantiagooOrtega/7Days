# Guía del jugador

Cómo se juega 7 DÍAS en la versión 1.0. Los números salen del código del juego (`Procedural/`), así que si
alguien cambia el balance hay que actualizar esta guía.

## Objetivo

Aguantar **7 días** en Valle Hondo. Cada día es una oleada de zombis y los días **3, 5 y 7** llega un jefe.
Se gana al limpiar la oleada del día 7; se pierde al morir (se puede reintentar ese mismo día).

## Cómo es un día

1. **Preparación (30 s):** busca botín, cúrate y elige dónde pelear. **Enter** la salta.
2. **Oleada:** los zombis salen por los bordes del mapa, a 15 m o más de ti y a ser posible donde no los ves.
3. **Jefe** (días 3, 5 y 7): aparece cuando ha caído la mitad de la oleada.
4. **«OLEADA SUPERADA»:** sale un resumen corto y a los 3 s empieza el día siguiente. Al empezar cada día
   desde el 2 recuperas el **30 % de tu vida máxima** y reaparece una parte pequeña del botín.

| Día | Zombis de la oleada | Vivos a la vez (máx.) | Qué aparece por primera vez |
|-----|---------------------|-----------------------|-----------------------------|
| 1 | 14 | 8 | Zombis comunes |
| 2 | 21 | 10 | Corredores |
| 3 | 28 | 12 | Lanzadores · jefe **Bruto Titán** |
| 4 | 35 | 14 | Explosivos y blindados |
| 5 | 42 | 16 | Tanques · jefe **Lanzador Colosal** |
| 6 | 49 | 18 | — |
| 7 | 56 | 20 | Jefe **El Devorador** |

VIGÍA, el director, ajusta cada día la vida, la velocidad, el daño y la cantidad de zombis especiales según
cómo jugaste el día anterior (como mucho un 25 % arriba o abajo). Pulsa **F4** para ver por qué.

## Personajes

Se eligen en la pantalla de Preparación (Jugar). Las diferencias son pequeñas a propósito, para que el
balance de los 7 días sirva para todos.

| Personaje | Vida | Velocidad | Curación | Balanceo del arma |
|-----------|------|-----------|----------|-------------------|
| Calavera | 100 | ×1.00 | ×1.00 | ×1.00 |
| Explorador | 90 | ×1.05 | ×1.00 | ×1.00 |
| Médico | 100 | ×1.00 | ×1.30 | ×1.00 |
| Francotirador | 95 | ×1.00 | ×1.00 | ×0.80 (apunta más estable) |
| Sam Duarte | 100 | ×1.00 | ×1.00 | ×1.00 (el protagonista del cómic) |

## Mapas

- **Calle en ruinas:** calle industrial con una casa central de dos pisos (escalera interior y balcón).
- **Bosque oscuro:** noche con niebla densa, troncos y rocas como cobertura y una cabaña como refugio.

## Controles

| Acción | Tecla |
|--------|-------|
| Moverse / correr / agacharse / saltar | WASD / Shift / Ctrl / Espacio |
| Apuntar / disparar o golpear / modo de disparo | Clic derecho / clic izquierdo / X |
| Recargar / cambiar de arma / rueda de armas | R / 1-5 o rueda del ratón / mantener Tab |
| Soltar el arma activa | G |
| Granada | Mantener F (se ve el arco y dónde cae) y soltar |
| Recoger / buscar | E / mantener E en cajas, coches, muebles y escombros |
| Botiquín / venda | H / B |
| Cobertura / deslizarse / cambiar de hombro o de lado | C / A y D / Q |
| Empezar la oleada (en la preparación) | Enter |
| Panel del director / ayuda / pausa | F4 / F1 / Esc |

Espacio también sirve para **pasar por encima** de cajas, muros bajos y coches (corriendo o desde la
cobertura, al otro lado).

## La cobertura

Pulsa **C** junto a una caja, un muro bajo, un tronco o una roca. Arriba aparece tu estado:

- **CUBIERTO:** el muro te protege. Es el momento de recargar.
- **ASOMADO** (mantén clic derecho): te levantas o te inclinas por el lado de tu hombro (Q lo cambia) y
  disparas con normalidad, pero quedas expuesto.
- **A CIEGAS** (clic izquierdo sin apuntar): disparas sin asomarte, con mucha dispersión y el **60 % del
  daño**. Sirve para frenar a uno que se acerca, no para matar de lejos.

La cobertura no lo resuelve todo:

- El **lanzador** tira un proyectil directo que la cobertura bloquea. Pero si llevas más de 1 s escondido,
  cambia a un **tiro en arco** que cae donde estás: verás una **marca roja en el suelo** antes del impacto.
  Muévete.
- Si te quedas mucho tiempo quieto en la misma cobertura, los lanzadores **te rodean** por un lado.
- El director premia usarla entre el 10 % y el 60 % del tiempo. Pasar el día entero escondido baja tu
  puntuación.
- El jefe del día 5 **destruye coberturas**, y las granadas y explosivos rompen las ligeras (cajas, barriles,
  tablones). Las rotas vuelven entre oleadas.

## Armas y munición

Empiezas con la **pistola** (6 balas en el cargador y 30 de reserva) y la **barra metálica**. Puedes llevar
hasta 5 armas. Las demás se encuentran buscando en el mapa o las sueltan los zombis (las mejores, casi
siempre, los jefes).

| Arma | Rareza | Daño | Cargador | Munición | Para qué sirve |
|------|--------|------|----------|----------|----------------|
| Pistola | común | 18 | 12 | pistola | 2 tiros a la cabeza matan a un zombi común |
| Revólver | poco común | 48 | 6 | pistola | potente y lento; atraviesa algo de armadura |
| Subfusil | poco común | 12 | 30 | pistola | automático; bueno contra corredores, malo contra tanques |
| Escopeta | poco común | 12 × 8 perdigones | 6 | escopeta | solo de cerca: a 3 m mata en 1-2 tiros |
| Fusil de asalto | rara | 36 | 30 | rifle | automático; 2-3 tiros al cuerpo por zombi común |
| Rifle de francotirador | épica | 90 | 5 | rifle | 1 tiro a la cabeza; sin caída de daño |
| Barra metálica | común | 38 | — | — | cuerpo a cuerpo, sin gastar balas |

- **Colores de rareza:** gris (común), verde (poco común), azul (rara) y morado (épica).
- Hay **tres reservas** de munición, compartidas por las armas del mismo tipo: pistola (máx. 132),
  escopeta (máx. 50) y rifle (máx. 225). Con la reserva llena, la munición de ese tipo se queda en el suelo.
- Si recoges un arma que ya tienes, se convierte en munición.
- **La cabeza** recibe ×2.5 de daño (más en algunas armas). Contra blindados es casi obligatorio.
- Si te quedas 60 s sin balas de un tipo, VIGÍA deja un paquete doble cerca de ti.

## Botín

- **Buscar:** mantén **E** de 1 a 2 s en muebles, maleteros de coches, escombros, arbustos y cajas de
  suministros. Las cajas lejanas y expuestas tienen mejor botín.
- **Drops:** algunos zombis sueltan algo al morir; el jefe siempre suelta algo raro o mejor, con una columna
  de luz.
- Los suministros cercanos se marcan con un icono y con flechas en el borde de la pantalla. La munición y
  los botiquines se recogen solos al pasar por encima.

## Curación

| Objeto | Cura | Tarda | Máx. que llevas |
|--------|------|-------|-----------------|
| Venda (**B**) | 15 | 1.2 s | 5 |
| Botiquín pequeño | 30 | 2.5 s | 3 |
| Botiquín mediano | 60 | 4 s | 2 |
| Botiquín grande | 100 | 6 s | 1 |

- **H** usa el botiquín que mejor encaja con la vida que te falta; **B** usa una venda.
- Sale una barra: si **recibes daño** antes de que termine, se cancela y **no se gasta** el botiquín.
  Cúrate detrás de una cobertura o lejos de los zombis.
- El Médico cura un 30 % más con todo.
- Entre días recuperas el 30 % de la vida máxima sin gastar nada.

## Granadas

- Empiezas con **2** y puedes llevar **6**. Hay 2 sueltas por el mapa al empezar y 1 más cada día; algunos
  zombis las sueltan (el jefe, siempre 2).
- **Mantén F** para ver el arco y la marca de impacto; **suelta** para lanzarla. Explota a los 2.5 s y
  rebota en paredes y suelo.
- Daño fuerte en 6 m: un zombi común muere seguro a 4.5 m o menos. Contra los jefes casi no sirve.
- **Cuidado:** a menos de 4 m también te daña a ti. Rompe las coberturas ligeras cercanas.

## Zombis y cómo vencerlos

| Zombi | Desde el día | Vida | Cómo es | Consejo |
|-------|--------------|------|---------|---------|
| **Zombi** (común) | 1 | 100 | equilibrado | 2 tiros de pistola a la cabeza |
| **Corredor** | 2 | 55 | muy rápido (más del doble que el común) y frágil | no corras de él: apunta, que con poco cae |
| **Lanzador** | 3 | 80 | se queda a unos 12 m y te lanza cosas | mátalo primero; si te tira en arco, cambia de sitio |
| **Explosivo** | 4 | 70 | al llegar a 3 m se para, se hincha y explota (radio 3.6 m) | dispárale de lejos; si ya parpadea, aléjate a más de 5.5 m y la mecha se apaga |
| **Blindado** | 4 | 160 | el torso casi no recibe daño (×0.35) | apunta siempre a la cabeza |
| **Tanque** | 5 | 400 | lento, resiste el 35 % del daño y no cabe por las puertas | revólver, fusil o francotirador; la pistola y el subfusil casi no le hacen nada |

- El **explosivo** también explota si lo matas a tu lado, y daña a los zombis de alrededor: úsalo a tu favor
  cuando está en medio de un grupo.
- El **tanque** no entra a las casas: si estás dentro, golpea los muros desde fuera (menos daño) y luego
  rodea la casa buscándote. Aprovecha para dispararle por una ventana.
- Cada día nuevo, el HUD avisa con «Nuevo zombi: …» cuando aparece un tipo por primera vez.

## Jefes

Aparecen cuando ha caído la mitad de la oleada de su día, con una barra grande arriba. Antes de cada ataque
**avisan**: alzan los brazos, sale una marca roja en el suelo, suena un tono y la barra muestra el nombre del
ataque. Al bajar del 50 % y del 25 % de vida entran en **furia**: más rápidos y atacan más a menudo.

| Día | Jefe | Vida | Qué hace | Cómo vencerlo |
|-----|------|------|----------|---------------|
| 3 | **Bruto Titán** | 1500 | carga en línea recta y golpea el suelo | ponte delante de un muro o una cobertura pesada y esquiva la carga en el último momento: si choca, queda **aturdido 2.5 s** y recibe ×1.5 de daño |
| 5 | **Lanzador Colosal** | 2400 | lanza objetos enormes en arco que **destruyen las coberturas**, llama a corredores y golpea el suelo | no te quedes en una cobertura: muévete al ver la marca roja y limpia a los corredores primero |
| 7 | **El Devorador** | 3000 | onda expansiva, oleadas de zombis y golpe en área | **salta** (Espacio) cuando grite «¡ONDA EXPANSIVA! ¡SALTA!»; dispara al **núcleo naranja del pecho** (×4) cuando se abren sus costillas: pasa 4 s de cada 14 y justo después de la onda |

- Los disparos casi no los frenan ni los empujan.
- Las granadas les hacen muy poco daño: guárdalas para los zombis que los acompañan.
- Al morir sueltan siempre un arma o un objeto raro o mejor.

## Puntuación del día (la que usa el director)

Al terminar cada día, VIGÍA te pone una nota de 0 a 100. **50 es lo esperado.** Entre 45 y 55 la dificultad
no cambia; por encima sube y por debajo baja. Si mueres, la nota del día queda en 20 como máximo.

| Parte | Puntos | Cómo se gana |
|-------|--------|--------------|
| Precisión | 25 | acertar el 60 % de los disparos o más da el máximo |
| Cabeza | 15 | que el 40 % de tus bajas o más sean a la cabeza |
| Daño evitado | 30 | recibir poco daño por cada zombi que matas |
| Rapidez | 20 | limpiar la oleada en unos 6 s por zombi o menos |
| Cobertura | 10 | estar cubierto entre el 10 % y el 60 % del tiempo |

La nota que ve el director es una media: 60 % la de antes y 40 % la de hoy, para que un mal día no lo cambie
todo de golpe.

## Puntos del ranking

Al terminar la partida (ganando o perdiendo) se guardan en el Perfil y, si iniciaste sesión, en el ranking
del servidor:

```
puntos = días superados × 1000 + bajas × 10 + bajas a la cabeza × 5 + 5000 si ganas + nota media × 10
```

## Al morir o al ganar

La pantalla final muestra tus estadísticas, las barras de la nota de cada día, una gráfica por día y las
insignias que ganaste:

| Insignia | Cómo se consigue |
|----------|------------------|
| Francotirador | precisión del 55 % o más, 40 % de bajas a la cabeza y 8 bajas o más |
| Sin cobertura | 10 bajas o más pasando menos del 5 % del tiempo cubierto |
| Racha | 8 bajas seguidas con menos de 4 s entre una y otra |
| Granadero | 5 bajas con granada |
| Intocable | menos de 2 de daño recibido por baja, con 10 bajas o más |
| Superviviente | superar 5 días o más |

Opciones: **Enter** reintenta el día en el que caíste, **Esc** vuelve al menú y **Tab** abre la tabla de
estadísticas por día. Desde el menú, **Continuar** retoma el día guardado.
