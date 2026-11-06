using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Net;
using SevenDays.Player;
using SevenDays.UI;
using SevenDays.Weapons;

namespace SevenDays.World
{
    /// <summary>
    /// Arena de pruebas de la Fase 2. Construye por código el suelo, muros, coberturas, blancos y al
    /// jugador, así que la escena solo necesita un objeto vacío con este componente.
    /// </summary>
    public class TestArena : MonoBehaviour
    {
        private Health _playerHealth;

        private void Start()
        {
            if (!Session.IsLoggedIn)
                Debug.LogWarning("[7Days] Jugando sin sesión (modo prueba). Para el flujo completo empieza desde la escena Login.");

            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            BuildLevel();

            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            var weapons = new List<WeaponDefinition> { DefaultWeapons.Rifle(), DefaultWeapons.Pistol() };
            var player = PlayerFactory.Create(new Vector3(0f, 0.1f, -14f), 0f, cam, weapons);
            _playerHealth = player.GetComponent<Health>();

            var hud = new GameObject("HUD").AddComponent<PlayerHud>();
            hud.Initialize(player);
        }

        private void Update()
        {
            // Atajos de depuración para probar el HUD: T = recibir 20 de daño, Y = curarse.
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null || _playerHealth == null) return;
            if (k.tKey.wasPressedThisFrame)
                _playerHealth.TakeDamage(new DamageInfo { Amount = 20f, Point = transform.position });
            if (k.yKey.wasPressedThisFrame) _playerHealth.Heal(50f);
        }

        private void BuildLevel()
        {
            var root = new GameObject("Level").transform;
            var concrete = new Color(0.42f, 0.42f, 0.4f);
            var dark = new Color(0.3f, 0.3f, 0.3f);
            var rust = new Color(0.45f, 0.27f, 0.18f);

            Prim.Create(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(60f, 0.5f, 60f),
                new Color(0.28f, 0.29f, 0.27f), root);

            // Muros perimetrales
            Wall(root, "Wall N", new Vector3(0f, 2f, 30f), new Vector3(60f, 4f, 1f), dark);
            Wall(root, "Wall S", new Vector3(0f, 2f, -30f), new Vector3(60f, 4f, 1f), dark);
            Wall(root, "Wall E", new Vector3(30f, 2f, 0f), new Vector3(1f, 4f, 60f), dark);
            Wall(root, "Wall W", new Vector3(-30f, 2f, 0f), new Vector3(1f, 4f, 60f), dark);

            // Coberturas (altura 1.2 m: agachado quedas oculto, apuntando te asomas)
            Cover(root, "Barrier 1", new Vector3(-4f, 0.6f, -8f), new Vector3(3.5f, 1.2f, 0.6f), 0f, concrete);
            Cover(root, "Barrier 2", new Vector3(4f, 0.6f, -6f), new Vector3(3.5f, 1.2f, 0.6f), 15f, concrete);
            Cover(root, "Crate 1", new Vector3(0f, 0.6f, -4f), new Vector3(1.4f, 1.2f, 1.4f), 20f, rust);
            Cover(root, "Barrier 3", new Vector3(-9f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 4f), 0f, concrete);
            Cover(root, "Crate 2", new Vector3(8f, 0.6f, 2f), new Vector3(1.4f, 1.2f, 1.4f), -10f, rust);
            Cover(root, "Barrier 4", new Vector3(0f, 0.6f, 4f), new Vector3(4f, 1.2f, 0.6f), 0f, concrete);

            // Pilares altos (no son cobertura, solo ambiente)
            for (int i = -2; i <= 2; i++)
                Wall(root, "Pillar " + i, new Vector3(i * 9f, 2.5f, 22f), new Vector3(1.2f, 5f, 1.2f), dark);

            // Blancos de práctica
            for (int i = 0; i < 6; i++)
            {
                float x = -12f + i * 4.8f;
                float z = 10f + (i % 2) * 6f;
                var d = Prim.Create(PrimitiveType.Capsule, "Dummy " + i, new Vector3(x, 1f, z),
                    new Vector3(0.8f, 1f, 0.8f), new Color(0.65f, 0.2f, 0.18f), root);
                d.AddComponent<Health>().SetMax(60f);
                var dummy = d.AddComponent<TargetDummy>();
                if (i % 3 == 2) { dummy.StrafeAmplitude = 3f; dummy.StrafeSpeed = 1.2f; }
            }
        }

        private static void Wall(Transform root, string name, Vector3 pos, Vector3 scale, Color color)
            => Prim.Create(PrimitiveType.Cube, name, pos, scale, color, root);

        private static void Cover(Transform root, string name, Vector3 pos, Vector3 scale, float yaw, Color color)
        {
            var go = Prim.Create(PrimitiveType.Cube, name, pos, scale, color, root);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
        }
    }
}
