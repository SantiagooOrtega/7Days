# Hooks de animación del jugador

**Hoy la animación es procedural** (sin clips ni modelos externos, ver `ARTE_PROCEDURAL.md`): `Animation/ProceduralRig` anima un esqueleto humanoide propio y `Animation/PlayerRigDriver` lo alimenta leyendo ESTOS mismos parámetros de `PlayerAnimatorHooks` (la interfaz pública no cambia). Si un día se usan clips reales, basta con asignar un Animator con estos parámetros y quitar el driver: el resto del juego no se toca.

## Esqueleto procedural
Huesos con nombres estilo Humanoid: `Hips, Spine, Chest, UpperChest, Neck, Head, Left/RightShoulder, Left/RightUpperArm, Left/RightLowerArm, Left/RightHand, Left/RightUpperLeg, Left/RightLowerLeg, Left/RightFoot, Left/RightToes` (22). Pose de reposo con brazos y piernas hacia abajo; eje de las extremidades = -Y local, columna = +Y. Proporciones en `RigProportions` (altura, hombros, cadera, grosor, encorvado...). No se construye un Avatar Humanoid: toda la animación es procedural y no hace falta; los nombres permiten mapearlo después con `AvatarBuilder` si se quisiera.

## Capas del ProceduralRig
| Capa | Qué mueve | Suavizado (`RigSmoothing`) |
|------|-----------|----------------------------|
| Locomoción | zancada ligada a la distancia real (no patina), pies plantados con raycast + IK de dos huesos, pelvis (rebote, balanceo, giro de cadera), idle con cambios de peso y pasos correctivos, aire y aterrizaje | `pelvis` 0.06 s, `footPlanted` 0.02 s, `footSwing` 0.05 s |
| Torso | inclinación al correr y agachado, contrabalanceo de hombros, respiración (más con fatiga), lean lateral de cobertura, aim offset, anticipación del giro | `torso` 0.12 s |
| Cabeza | mira hacia la mira con límites y se adelanta al torso | `head` 0.06 s |
| Brazos y arma | IK de dos huesos a la empuñadura y al guardamano (pesos continuos), balanceo libre al correr | `hands` 0.05 s, `weights` 0.15 s |
| Reacciones | retroceso según la dirección del golpe, aterrizaje amortiguado, muerte (se desploma y cae de espaldas) | muelles propios |
Todo pasa por muelles con amortiguación crítica (`CriticalSpring`): nunca hay saltos de pose. La mezcla de estados es continua por pesos (`LocomotionBlend`: idle/caminar/trotar/correr por velocidad, 8 direcciones por MoveX/MoveY).

# Parámetros (interfaz pública)

`Player/PlayerAnimatorHooks.cs` escribe cada frame estos parámetros en el `Animator` que le asignes (campo **Animator**). Solo escribe los que existan en el controlador, así que uno parcial no da errores. El menú **7Days > Crear Animator del jugador** genera `Assets/_Project/Animation/PlayerAnimator.controller` con todos los parámetros, el blend tree de 8 direcciones y las capas: solo hay que arrastrar los clips a cada ranura.

| Parámetro | Tipo | Valor | Animación que espera (Quaternius Zombie Apocalypse Kit / Mixamo) |
|-----------|------|-------|------------------------------------------------------------------|
| `Speed` | float | m/s horizontales (0 – 6.8) | umbral idle ↔ caminar ↔ correr si usas un blend 1D |
| `MoveX` | float | -1..1 lateral respecto al cuerpo (1 = correr) | blend tree 2D: strafe izquierda/derecha |
| `MoveY` | float | -1..1 adelante/atrás (1 = correr) | blend tree 2D: caminar/correr adelante, caminar atrás |
| `IsAiming` | bool | clic derecho | capa superior: pose de apuntar con rifle/pistola (aim idle) |
| `IsBlindFiring` | bool | disparo a ciegas desde la cobertura | capa UpperBody: brazo extendido sobre el borde o por el lado, arma apuntando al frente sin mirar |
| `Exposure` | float | 0 (cubierto) .. 1 (asomado); 1 fuera de cobertura | blend del estado Cover: cover idle (0) ↔ asomado por encima/lado (1); el lado lo da el signo del hombro (Q) |
| `IsCrouching` | bool | Ctrl o en cobertura sin apuntar | estado Crouch (idle y caminar agachado) |
| `IsInCover` | bool | pegado a una `CoverObject` | estado Cover (cover idle, deslizarse lateral) |
| `IsGrounded` | bool | en el suelo | sale del estado Airborne |
| `IsJumping` | bool | durante el salto | Airborne: jump start / loop |
| `IsVaulting` | bool | pasando un obstáculo | estado Vault (vault over / climb) |
| `VerticalSpeed` | float | m/s (positivo sube) | Airborne: blend subida ↔ caída |
| `Fatigue` | float | 0..1 (cámara de carrera) | capa aditiva opcional: respiración agitada, correr cansado |
| `Fire` | trigger | cada disparo | capa UpperBody: retroceso del arma (fire) |
| `Reload` | trigger | al empezar a recargar | capa UpperBody: recarga (dura `reloadTime` del arma) |
| `MeleeSwing` | trigger | golpe con la barra | capa UpperBody: golpe con arma cuerpo a cuerpo |
| `MeleeHeavy` | trigger | (reservado: golpe pesado) | capa UpperBody: golpe pesado |
| `MeleeIndex` | int | índice de combo (hoy 0) | variantes del golpe |

Ranuras del blend tree `Locomotion` (2D Freeform Directional, MoveX/MoveY): (0,0) idle; (0,0.6) caminar adelante; (0,-0.6) caminar atrás; (±0.6,0) strafe; (±0.45,±0.45) diagonales; (0,1) correr.

## Aim offset y manos (Animation Rigging)
Hoy el *aim offset* es procedural sobre las primitivas: el arma (`WeaponPose`) sigue el pitch de la mira al apuntar o disparar y el cuerpo se inclina un poco (`WeaponHandling`); el lean en cobertura inclina el cuerpo y desplaza la cámara. El paquete Animation Rigging no está instalado para no cambiar los paquetes del proyecto sin los modelos. Al importar el modelo humanoide:
1. Window > Package Manager > Unity Registry > **Animation Rigging** > Install.
2. En el modelo del jugador: Animation Rigging > Rig Setup (añade `RigBuilder` y un hijo `Rig`).
3. En `Rig`, un hijo con **Multi-Aim Constraint**: Constrained Object = columna (Spine2/Chest), Source Object = `SightAnchor` (lo crea `PlayerCamera` y siempre mira a donde apunta la mira), Aim Axis Z, peso 0.6. Otro igual para la cabeza (peso 0.8).
4. Dos hijos con **Two Bone IK Constraint**: mano derecha (Root = brazo, Mid = antebrazo, Tip = mano, Target = un hijo de `WeaponPose` en la empuñadura) y mano izquierda (Target = un hijo de `WeaponPose` en el guardamanos).
5. Mueve el `SightPoint` (hijo de `WeaponPose`) a la mira real del arma importada: es donde se coloca la cámara de primera persona del francotirador.
6. Asigna el `Animator` del modelo al campo **Animator** de `PlayerAnimatorHooks` y el controlador generado arriba.
