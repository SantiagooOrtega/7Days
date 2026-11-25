# Glosario

Palabras que salen en el juego, en el código y en los documentos, explicadas en una línea.

## Del juego

| Palabra | Qué es |
|---------|--------|
| **Día** | Una ronda de la partida: preparación, oleada y resumen. Hay 7. |
| **Preparación** | Los 30 s antes de la oleada para buscar botín y colocarse. Enter la salta. |
| **Oleada** | Los zombis de un día. Van de 14 (día 1) a 56 (día 7). |
| **Jefe** | Zombi gigante de los días 3, 5 y 7, con barra de vida propia. |
| **Furia** | Cuando un jefe baja del 50 % y del 25 % de vida: va más rápido y ataca más. |
| **Telegraph (aviso)** | La señal antes de un ataque del jefe o de un tiro en arco: marca roja en el suelo, tono y texto. |
| **Punto débil** | El núcleo naranja del pecho de El Devorador; recibe ×4 de daño cuando está expuesto. |
| **Cobertura** | Objeto tras el que te cubres con C (cajas, muros bajos, troncos, rocas). |
| **Asomarse (pop-out)** | Salir de la cobertura apuntando con clic derecho para disparar. |
| **A ciegas (blind fire)** | Disparar desde la cobertura sin asomarse: mucha dispersión y el 60 % del daño. |
| **Vault** | Pasar por encima de una caja, un muro bajo o un coche con Espacio. |
| **Headshot** | Disparo a la cabeza: ×2.5 de daño o más. |
| **Rareza** | Lo buena que es un arma: común (gris), poco común (verde), rara (azul) y épica (morada). |
| **Drop** | Lo que suelta un zombi al morir. |
| **Pity** | Ayuda automática: si pasas 60 s sin balas de un tipo, aparece un paquete cerca. |
| **VIGÍA** | La IA de seguridad de la Planta 7: habla por radio y es el director de dificultad. |

## Del código y la técnica

| Palabra | Qué es |
|---------|--------|
| **Director** | La parte que mide cómo juegas y ajusta la dificultad del día siguiente (`Procedural/Performance.cs`). |
| **Nota del día** | Puntuación de 0 a 100 que calcula el director; 50 es lo esperado. |
| **Función pura** | Código que, con los mismos datos, da siempre el mismo resultado y no toca nada más. Se puede probar sin Unity. |
| **Procedural** | Generado por código en lugar de dibujado o modelado a mano. |
| **NavMesh** | El mapa de zonas caminables que usan los zombis para moverse. |
| **Pool (pooling)** | Reutilizar objetos (balas, zombis, botín) en lugar de crearlos y destruirlos, para no perder FPS. |
| **LOD** | Versiones más simples de un modelo que se usan cuando está lejos. |
| **GPU instancing** | Dibujar muchas copias del mismo objeto (árboles, alambre) de una sola vez. |
| **Post-procesado** | Filtros sobre la imagen final: desaturado, contraste, viñeta, bloom y grano. |
| **EditMode / PlayMode** | Los dos tipos de pruebas de Unity: sin dar Play y dando Play. |
| **JWT** | Token firmado que demuestra que iniciaste sesión; en 7 DÍAS dura 15 min. |
| **Refresh token** | Token largo que sirve para pedir un JWT nuevo sin volver a escribir la contraseña. |
| **argon2** | Algoritmo para guardar contraseñas de forma segura (hash lento con sal). |
| **zod** | Librería que valida los datos que llegan al backend. |
| **Prisma** | Librería que conecta el backend con la base de datos. |
| **Modo prueba** | Jugar sin iniciar sesión ni backend; el ranking se guarda solo en la Mac. |
