Zombie lanzador para dar utilidad al sistema de cobertura (CoverObject ya existe de la Fase 2). Entra en la Fase 3.
1. Máquina de estados: mantener distancia, apuntar, lanzar, reposicionarse. NavMeshAgent y pooling existente.
2. Proyectil directo (línea recta): si choca con un CoverObject se destruye y NO daña al jugador. Si el jugador está en cobertura y el proyectil viene de frente, no hay daño.
3. Proyectil en arco (parabólico): cae sobre la posición del jugador, ignora la cobertura frontal, muestra una marca en el suelo 1-1.5 s antes del impacto, con daño de área pequeña. Mismo pooling.
4. Flanqueo: si el jugador pasa más de N segundos (configurable) en cobertura, el lanzador busca un punto lateral con línea de tiro libre usando NavMesh.
5. Datos configurables en ScriptableObject/JSON: tipo de proyectil (escombro, barra metálica, ácido, granada), daño, velocidad, cooldown, rango, N de flanqueo. Esquema validable con zod.
6. IA director (Fase 4): métrica "tiempo en cobertura" y parámetro "proporción de lanzadores/arcos".
7. Jefe del día 5 con ataque de objeto grande que destruye el CoverObject golpeado (respawn entre oleadas).
8. Feedback: telegraph visual, sonido placeholder, daño absorbido visible en el HUD.
9. Pruebas EditMode (bloqueo por cobertura, flanqueo, balance) y pasos numerados para lo manual.
