Arregla el disparo desde cobertura: hoy, al estar "EN COBERTURA", al disparar el tiro choca con el muro porque el jugador no se asoma. Implementa el sistema estilo Gears of War. Mantén 60 FPS, un commit pequeño por mejora, push, dime el conteo (git rev-list --count HEAD), compila los tests contra el NUnit de Unity y antes de empezar dime qué NO podrás verificar sin el editor. No rompas cobertura, vault, ADS, recoil, hitboxes ni la cámara de carrera.

1. Asomarse para disparar (pop-out):
   - En cobertura, clic derecho (apuntar) hace que el jugador se levante o se incline y se asome: por ENCIMA si la cobertura es baja (caja, muro bajo) o por el LADO (según el hombro activo con Q) si es alta o está en el borde. Transición suave de 0.15-0.25 s con la cámara acompañando.
   - Al soltar el apuntado, vuelve a cubrirse con la misma transición.
   - Asomado, el jugador queda expuesto (cabeza y torso visibles para zombis y proyectiles). Cubierto, el muro lo protege, como ya funciona con los proyectiles del lanzador.

2. Origen del disparo desde la posición asomada:
   - El raycast de la cámara sale de la mira, pero la trayectoria desde el arma debe partir de la posición asomada, no del cuerpo detrás del muro. Si el arma queda dentro del collider de la cobertura, desplaza el origen del disparo hasta el borde libre más cercano. Si no hay línea de tiro libre, no dispares y muestra un aviso discreto.
   - Ignora solo el collider de la propia cobertura en el primer tramo cuando el jugador está asomado y pegado a ella; nunca otras coberturas ni muros.

3. Disparo a ciegas (blind fire):
   - Clic izquierdo sin apuntar estando en cobertura: dispara a ciegas por encima o por el lado, con dispersión muy alta, daño reducido (configurable, por defecto x0.6) y exposición parcial. Hook de Animator: IsBlindFiring.

4. Exposición:
   - Valor de 0 a 1 según cuánto se asoma. Determina la protección frente a proyectiles, el daño recibido y la visibilidad para la IA de los zombis.
   - Exponlo al director y a las estadísticas: tiempo cubierto, tiempo asomado y tiempo en blind fire, para la regla de "abuso de cobertura".

5. HUD y controles:
   - Indicador: "CUBIERTO", "ASOMADO" o "A CIEGAS". Q sigue cambiando de hombro y de lado de asomada. Documenta las teclas en docs/UNITY_SETUP.md.

6. Compatibilidad:
   - Recargar solo cubierto. Salir con C, saltar y vault siguen funcionando. El cuerpo a cuerpo desactiva el pop-out.
   - Si no hay espacio por un lado, prueba el otro o por encima; si no hay ninguno, solo blind fire.

7. Pruebas EditMode: elección de lado (encima o lado), desplazamiento del origen fuera del collider, exposición, dispersión y daño del blind fire. Pasos numerados para lo manual.

CIERRE
Haz push, dime los commits y qué quedó sin verificar. Pídeme capturas: asomado por encima de un muro bajo, asomado por el lado de un muro alto y disparando a ciegas.
