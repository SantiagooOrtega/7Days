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
