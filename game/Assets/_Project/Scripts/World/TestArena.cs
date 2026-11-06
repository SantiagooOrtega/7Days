using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
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

            LookAndFeel.Apply(cam);
            Particles.CreateDust(player.transform);

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
            IndustrialRuins.Build(root);

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
    }
}
