using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>
    /// Construye al jugador completo por código (cápsula + arma + cámara), sin prefabs.
    /// Cuando haya modelos de Mixamo (Fase 2.5) solo hay que reemplazar el "Visual".
    /// </summary>
    public static class PlayerFactory
    {
        public static GameObject Create(Vector3 position, float yaw, Camera cam, IEnumerable<WeaponDefinition> weapons)
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
            // Colores claros y un acento naranja: el jugador se distingue del fondo gris aunque esté en sombra
            Prim.Create(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f),
                new Color(0.62f, 0.72f, 0.6f), visual, false);
            Prim.Create(PrimitiveType.Sphere, "Helmet", new Vector3(0f, 1.62f, 0f), new Vector3(0.46f, 0.4f, 0.46f),
                new Color(0.8f, 0.8f, 0.74f), visual, false);
            Prim.Create(PrimitiveType.Cube, "Visor", new Vector3(0f, 1.6f, 0.2f), new Vector3(0.34f, 0.1f, 0.12f),
                new Color(0.08f, 0.1f, 0.12f), visual, false); // indica hacia dónde mira
            Prim.Create(PrimitiveType.Cube, "Vest Stripe", new Vector3(0f, 1.15f, 0.3f), new Vector3(0.56f, 0.12f, 0.08f),
                new Color(0.95f, 0.55f, 0.1f), visual, false);
            Prim.Create(PrimitiveType.Cube, "Backpack", new Vector3(0f, 1.05f, -0.34f), new Vector3(0.4f, 0.5f, 0.22f),
                new Color(0.9f, 0.5f, 0.12f), visual, false);

            // Soporte del arma y boca del cañón
            var holder = new GameObject("WeaponHolder").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(0.3f, 1.2f, 0.25f);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(holder, false);

            var health = root.AddComponent<Health>();
            health.SetMax(100f);

            var playerCamera = root.AddComponent<PlayerCamera>();
            var cover = root.AddComponent<PlayerCover>();
            var motor = root.AddComponent<PlayerMotor>();
            var shooting = root.AddComponent<PlayerShooting>();

            playerCamera.Initialize(root.transform, cam, yaw);
            cover.Initialize(playerCamera);
            motor.Initialize(playerCamera, cover, visual, holder);
            shooting.Initialize(playerCamera, motor, holder, muzzle, weapons);

            root.AddComponent<RimLight>().Initialize(root.transform, cam.transform);

            // "Ignore Raycast" (capa 2): los disparos y la cámara no chocan con el propio jugador.
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            return root;
        }
    }
}
