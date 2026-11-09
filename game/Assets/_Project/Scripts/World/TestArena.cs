using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Combat;
using SevenDays.Look;
using SevenDays.Net;
using SevenDays.Player;
using SevenDays.UI;
using SevenDays.World.Loot;

namespace SevenDays.World
{
    /// <summary>
    /// Arena de pruebas de la Fase 2. Construye por código el suelo, muros, coberturas y al
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
        private Transform _levelRoot;
        private SevenDays.Procedural.LootLayoutData _lootLayout;
        private LootManager _lootManager;
        private int _day = 1;
        private SevenDays.Combat.ZombieManager _zombies;
        private int _zombieKind;
        private int _bossKind;

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
            _lootLayout = LootIO.Load(_layout.items, HouseIO.Load()); // botín: tablas, drops y puntos (coches y arbustos de este mapa)
            _lootManager = LootManager.Create(transform, _lootLayout, 20240517);
            BuildLevel();

            var player = PlayerFactory.Create(new Vector3(0f, 0.1f, -14f), 0f, cam, _lootLayout, _look.Movement);
            _playerHealth = player.GetComponent<Health>();
            _player = player;
            _fire.SetPlayer(player);
            _lootManager.SetPlayer(player);
            LootBuilder.Build(_levelRoot, _lootLayout, _lootManager);
            _lootManager.PlaceGuaranteedWeapons();   // 3 armas visibles: casa, zona expuesta y caja intermedia
            Debug.Log(_lootManager.Describe()); // contenedores buscables y objetos sueltos (necesita la casa y los coches ya construidos)
            Physics.SyncTransforms();
            ColliderAudit.Run(_levelRoot);

            _zombies = ZombieManager.Create(transform, ZombieIO.Load());
            _zombies.BakeNavMesh(); // después de construir el nivel y de la auditoría: usa los colliders ya definitivos
            _zombies.SetPlayer(player.transform);
            BloodFx.Create(transform);
            BossHud.Create(transform);
            BlastFx.Create(transform);
            CombatMetricsTracker.Create(transform, player, _zombies);
            CombatFeedback.Create(transform, player.GetComponent<SevenDays.Player.PlayerShooting>());

            Particles.CreateDust(player.transform);
            Atmosphere.Build(transform, player.transform, _look);

            var hud = new GameObject("HUD").AddComponent<PlayerHud>();
            hud.Initialize(player);
            hud.gameObject.AddComponent<SettingsOverlay>().Initialize(player.GetComponent<PlayerCamera>());
            hud.gameObject.AddComponent<CombatDebugPanel>().Initialize(player);
            hud.gameObject.AddComponent<RigDebugPanel>().Initialize(player.GetComponentInChildren<SevenDays.Animation.ProceduralRig>());
            hud.gameObject.AddComponent<RigDemoCycle>().Initialize(player.transform);
            hud.gameObject.AddComponent<WeaponHud>().Initialize(player);   // selector de armas con iconos y rueda radial (Tab)
            hud.gameObject.AddComponent<ZombieSoundPanel>();   // F6: probar cada sonido de zombi
            hud.gameObject.AddComponent<RenderDebugPanel>();   // F6: panel Render (resolución, ms de CPU/GPU, draw calls, luces, calidad)   // F7: ciclo automático de piernas con cámara lateral
            hud.gameObject.AddComponent<ScopeOverlay>().Initialize(player.GetComponent<PlayerCamera>());
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
            if (k.lKey.wasPressedThisFrame && _player != null && _lootManager != null)
            {
                // Drop de prueba delante del jugador (cuenta para el tope diario y el de pickups en el suelo)
                _lootManager.DebugDrop(_player.transform.position + _player.transform.forward * 2.5f);
            }
            if (k.nKey.wasPressedThisFrame && _lootManager != null)
            {
                // Siguiente día: el director hace reaparecer un poco de botín y tapa las entradas de la casa según el día
                _day = _day % 7 + 1;
                _lootManager.NewDay(_day);
                // fin del día: las estadísticas de hoy se suman a las guardadas, se envían al backend y las reglas del director las leen para ajustar el día siguiente
                var tracker = CombatMetricsTracker.Instance;
                var totals = tracker != null ? tracker.EndDay(_day) : null;
                if (_zombies != null)
                {
                    var reasons = tracker != null ? tracker.DirectorReasons : new List<string>();
                    reasons.Clear();
                    _zombies.SetTuning(SevenDays.Procedural.DirectorRules.Adjust(SevenDays.Procedural.DirectorTuning.ForDay(_day), totals, reasons));
                    foreach (var r in reasons) Debug.Log("[7Days] Director: " + r);
                }
                if (_house != null) _house.SetDay(_day);
            }
            if (k.zKey.wasPressedThisFrame && _player != null && _zombies != null)
            {
                // Z = zombi de prueba 12 m delante (cicla por los tipos)
                string id = SevenDays.Procedural.ZombieIds.All[_zombieKind % 6]; _zombieKind++;
                var spawn = _player.transform.position + _player.transform.forward * 12f;
                var z = _zombies.Spawn(id, spawn);
                if (z != null) HudMessages.Add("Zombi: " + z.Type.displayName, Color.white);
            }
            if (k.vKey.wasPressedThisFrame && _player != null && _zombies != null)
            {
                // V = mini oleada de 8 zombis con las proporciones del día (prueba de rendimiento y de reparto de tipos)
                for (int i = 0; i < 8; i++)
                {
                    float ang = i / 8f * Mathf.PI * 2f;
                    _zombies.SpawnByTuning(_player.transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 18f);
                }
                HudMessages.Add("Oleada de prueba (día " + _day + ")", Color.white);
            }
            if (k.bKey.wasPressedThisFrame && _player != null && _zombies != null)
            {
                // B = jefe de prueba 20 m delante; cicla por los de los días 3, 5 y 7. Shift+B repone las coberturas rotas.
                if (k.leftShiftKey.isPressed) { SevenDays.World.Destructible.RestoreAll(); _zombies.RequestNavMeshRefresh(); HudMessages.Add("Coberturas repuestas", Color.white); }
                else
                {
                    int day = new[] { 3, 5, 7 }[_bossKind++ % 3];
                    _zombies.Spawn(SevenDays.Procedural.ZombieCatalog.BossForDay(day), _player.transform.position + _player.transform.forward * 20f);
                }
            }
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
            _levelRoot = root;
            IndustrialRuins.Build(root, _look);
            Skyline.Build(root, _look);
            if (_look.House.Enabled) IndustrialRuins.Anchors.AddRange(HouseIO.PerimeterAnchors(HouseIO.Load())); // maleza y sangre también junto a la casa
            GroundDetails.Build(root, _look, IndustrialRuins.Anchors);
            Vegetation.Build(root, _look, _layout);
            RoadMarkings.Build(root, _look);
            if (_look.House.Enabled) _house = House.Build(root, _look, HouseIO.Load());
        }
    }
}
