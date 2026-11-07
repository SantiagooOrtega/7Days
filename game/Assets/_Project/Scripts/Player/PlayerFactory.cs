using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>
    /// Construye al jugador completo por código (cápsula + arma + cámara), sin prefabs.
    /// Cuando haya modelos de Mixamo (Fase 2.5) solo hay que reemplazar el "Visual".
    /// </summary>
    public static class PlayerFactory
    {
        public static GameObject Create(Vector3 position, float yaw, Camera cam, LootLayoutData loot, MovementSettings movement = null)
        {
            var root = new GameObject("Player") { tag = "Player" };
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;

            // Parte visual (se aplasta al agacharse)
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Cuerpo: esqueleto humanoide procedural con animación por capas (sin clips ni assets externos). Las mallas cuelgan de los huesos.
            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(visual, false);
            var rig = rigGo.AddComponent<SevenDays.Animation.ProceduralRig>();
            rig.Build(SevenDays.Animation.ProportionSettings.Load());
            SevenDays.Animation.RigVisuals.BuildSoldier(rig, SevenDays.Animation.RigPalette.Soldier);

            // Soporte del arma y boca del cañón
            var holder = new GameObject("WeaponHolder").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(0.3f, 1.2f, 0.25f);
            // pose del arma (sway, bob, kickback, centrado al apuntar): hija del soporte, que sube y baja al agacharse
            var pose = new GameObject("WeaponPose").transform;
            pose.SetParent(holder, false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pose, false);

            var health = root.AddComponent<Health>();
            health.SetMax(100f);

            var playerCamera = root.AddComponent<PlayerCamera>();
            var cover = root.AddComponent<PlayerCover>();
            var motor = root.AddComponent<PlayerMotor>();
            var inventory = root.AddComponent<PlayerInventory>();
            var shooting = root.AddComponent<PlayerShooting>();
            var healing = root.AddComponent<PlayerHealing>();
            var interact = root.AddComponent<PlayerInteract>();
            var vault = root.AddComponent<PlayerVault>();
            var impulse = root.AddComponent<PlayerImpulse>();
            var handling = root.AddComponent<WeaponHandling>();
            root.AddComponent<PlayerAudio>();
            root.AddComponent<PlayerAnimatorHooks>(); // parámetros del Animator listos para la Fase 3
            movement = movement ?? new MovementSettings();

            playerCamera.Initialize(root.transform, cam, yaw);
            cover.Initialize(playerCamera);
            // el daño cuerpo a cuerpo que llega depende de cuánto te asomas: cubierto del todo, solo una fracción
            health.DamageScale = info => info.ScaledByCover && cover.Active ? CoverProtection.DamageMultiplier(cover.Exposure.Value, playerCamera.Settings.coveredDamageFloor) : 1f;
            impulse.Initialize(movement, playerCamera.Settings);
            motor.Initialize(playerCamera, cover, visual, holder, movement, vault, impulse);
            vault.Initialize(movement, cover, motor, impulse);
            inventory.Initialize(loot);
            shooting.Initialize(playerCamera, motor, pose, muzzle, inventory);
            handling.Initialize(pose, null, playerCamera, motor, shooting);   // el torso lo inclina ahora el rig (aim offset y lean)
            motor.ScaleVisualOnCrouch = false;                                  // agacharse es una pose del rig, no aplastar el modelo
            motor.HolderCrouchHeight = 0.9f;                                    // el arma sigue al pecho agachado (antes quedaba a la altura de la tripa)
            root.AddComponent<SevenDays.Animation.PlayerRigDriver>().Initialize(rig, pose);
            healing.Initialize(inventory);
            interact.Initialize(inventory);

            root.AddComponent<RimLight>().Initialize(root.transform, cam.transform);

            // "Ignore Raycast" (capa 2): los disparos y la cámara no chocan con el propio jugador.
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            return root;
        }
    }
}
