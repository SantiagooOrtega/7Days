using System.Collections;
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
        private GameObject _player;
        private HouseController _house;
        private FireManager _fire;
        private LookSettings _look;
        private SevenDays.Procedural.PropLayoutData _layout;
        private bool _loading;

        private IEnumerator Start()
        {
            if (!Session.IsLoggedIn)
                Debug.LogWarning("[7Days] Jugando sin sesión (modo prueba). Para el flujo completo empieza desde la escena Login.");

            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // Primero el look (niebla, post-procesado) y luego las texturas procedurales, que se generan
            // en hilos de fondo mientras se muestra "Generando texturas...".
            _look = new LookSettings();
            LookAndFeel.Apply(cam, _look);
            _loading = true;
            _layout = Vegetation.BuildLayout(_look);
            // texturas del mapa y piso se generan a la vez, en hilos de fondo
            yield return Coroutines.All(SurfaceLibrary.Build(_look.EffectiveSurface()), GroundLibrary.Build(_look.Ground, Mathf.Pow(_look.GlobalBrightness, 0.6f)));
            yield return SignLibrary.Build(TrafficSigns.Needed(_layout.items), _look.Signs.TextureSize, _look.Signs.Seed);
            _loading = false;

            _fire = FireManager.Create(transform, _look.Fire); // antes del mapa: los barriles, fogatas, coches y antorchas lo usan
            BuildLevel();

            var weapons = new List<WeaponDefinition> { DefaultWeapons.Rifle(), DefaultWeapons.Pistol() };
            var player = PlayerFactory.Create(new Vector3(0f, 0.1f, -14f), 0f, cam, weapons, _look.Movement);
            _playerHealth = player.GetComponent<Health>();
            _player = player;
            _fire.SetPlayer(player);

            Particles.CreateDust(player.transform);
            Atmosphere.Build(transform, player.transform, _look);

            var hud = new GameObject("HUD").AddComponent<PlayerHud>();
            hud.Initialize(player);
        }

        private void OnGUI()
        {
            if (!_loading) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(new Rect(0, Screen.height / 2f - 30, Screen.width, 60), "Generando texturas...", style);
        }

        private void Update()
        {
            // Atajos de depuración: T = recibir 20 de daño, Y = curarse, U = subir 6 m (probar la caída).
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null || _playerHealth == null) return;
            if (k.tKey.wasPressedThisFrame)
                _playerHealth.TakeDamage(new DamageInfo { Amount = 20f, Point = transform.position });
            if (k.yKey.wasPressedThisFrame) _playerHealth.Heal(50f);
            if (k.uKey.wasPressedThisFrame && _player != null)
            {
                // Prueba de caída: sube al jugador 6 m y lo suelta (más de 4 m hace daño y sacude la cámara)
                var cc = _player.GetComponent<CharacterController>();
                cc.enabled = false;
                _player.transform.position += Vector3.up * 6f;
                cc.enabled = true;
            }
        }

        private void BuildLevel()
        {
            var root = new GameObject("Level").transform;
            IndustrialRuins.Build(root, _look);
            Skyline.Build(root, _look);
            if (_look.House.Enabled) IndustrialRuins.Anchors.AddRange(HouseIO.PerimeterAnchors(HouseIO.Load())); // maleza y sangre también junto a la casa
            GroundDetails.Build(root, _look, IndustrialRuins.Anchors);
            Vegetation.Build(root, _look, _layout);
            RoadMarkings.Build(root, _look);
            if (_look.House.Enabled) _house = House.Build(root, _look, HouseIO.Load());
            Physics.SyncTransforms();
            ColliderAudit.Run(root);

            // Blancos de práctica: cuerpo claro, cabeza pálida y ojos brillantes para que se distingan del fondo
            for (int i = 0; i < 6; i++)
            {
                float x = -12f + i * 4.8f;
                float z = 10f + (i % 2) * 6f;
                var d = Prim.Create(PrimitiveType.Capsule, "Dummy " + i, new Vector3(x, 1f, z),
                    new Vector3(0.8f, 1f, 0.8f), new Color(0.85f, 0.32f, 0.26f), root);
                var head = Prim.Create(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.72f, 0f), Vector3.one * 0.55f,
                    new Color(0.82f, 0.74f, 0.62f), d.transform, false);
                var eye = Prim.GetEmissive(new Color(1f, 0.35f, 0.1f), 6f);
                for (int e = -1; e <= 1; e += 2)
                    Prim.Create(PrimitiveType.Sphere, "Eye", new Vector3(e * 0.16f, 0.12f, 0.42f), Vector3.one * 0.14f, eye, head.transform, false);
                d.AddComponent<Health>().SetMax(60f);
                var dummy = d.AddComponent<TargetDummy>();
                if (i % 3 == 2) { dummy.StrafeAmplitude = 3f; dummy.StrafeSpeed = 1.2f; }
            }
        }
    }
}
