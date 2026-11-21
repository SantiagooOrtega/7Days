using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Los tres mapas en la misma escena: cada uno carga de su JSON, la auditoría de colisiones queda en orden, todos los spawns están sobre el NavMesh y tienen
    /// camino hasta el jugador, el botín se coloca y las casas existen. Con ZOMBIE_SHOTS guarda una captura con la cámara del juego.
    /// </summary>
    public class MapsTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        public static string[] Ids() => new[] { "calle_en_ruinas", MapLibrary.NeighborhoodId, MapLibrary.ForestId };

        /// <summary>Otras pruebas (p. ej. ArenaAuditTests) pueden dejar su arena viva: se quita antes de empezar.</summary>
        [UnitySetUp]
        public IEnumerator CleanStart()
        {
            foreach (var a in UnityEngine.Object.FindObjectsByType(T("SevenDays.World.TestArena"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)a).gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Map_Loads_AuditIsClean_SpawnsReachThePlayer([ValueSource(nameof(Ids))] string id)
        {
            var log = new System.Collections.Generic.List<string>();
            Application.LogCallback cb = (msg, st, type) => { if (type != LogType.Log || msg.Contains("Auditoría")) log.Add(type + ": " + msg); };
            Application.logMessageReceived += cb;
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); var arena = _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Application.logMessageReceived -= cb;
            var map = (MapData)arena.GetType().GetProperty("Map").GetValue(arena);
            Assert.AreEqual(id, map.id, "cargó el mapa pedido");
            string audit = log.Find(l => l.Contains("Auditoría de colisiones"));
            Debug.Log("[Mapas] " + id + ": " + audit);
            Assert.IsNotNull(audit); Assert.IsTrue(audit.Contains("Todo en orden"), id + ": " + audit);
            foreach (var l in log) Assert.IsFalse(l.StartsWith("Error") || l.StartsWith("Exception"), id + ": " + l);

            // spawns: sobre el NavMesh y con camino completo hasta el jugador
            var player = GameObject.FindWithTag("Player").transform;
            NavMesh.SamplePosition(player.position, out NavMeshHit ph, 3f, NavMesh.AllAreas);
            int ok = 0;
            foreach (var s in map.spawns)
            {
                Assert.IsTrue(NavMesh.SamplePosition(new Vector3(s.x, 0f, s.z), out NavMeshHit hit, 2.5f, NavMesh.AllAreas), id + " spawn " + s.id + " fuera del NavMesh");
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(hit.position, ph.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) ok++;
                else Debug.LogWarning("[Mapas] " + id + " spawn " + s.id + " sin camino completo (" + path.status + ") desde " + hit.position + " hasta " + ph.position + " acaba en " + (path.corners.Length > 0 ? path.corners[path.corners.Length - 1].ToString() : "-") + " esquinas " + path.corners.Length);
            }
            Assert.AreEqual(map.spawns.Length, ok, id + ": spawns con camino al jugador");
            Assert.AreEqual(map.houses.Length, GameObject.FindObjectsByType(T("SevenDays.World.HouseController"), FindObjectsSortMode.None).Length, "casas");
            var lm = T("SevenDays.World.Loot.LootManager").GetProperty("Instance").GetValue(null);
            Debug.Log("[Mapas] " + lm.GetType().GetMethod("Describe").Invoke(lm, null));
            Assert.Greater((int)lm.GetType().GetProperty("ContainerCount").GetValue(lm), 6, "contenedores de botín colocados");
            Assert.AreEqual(0, lm.GetType().GetField("SkippedPoints").GetValue(lm), "ningún punto de botín sin su mueble o coche");
            Shot(id, Camera.main);
        }

        private static void Shot(string name, Camera cam)
        {
            string dir = Environment.GetEnvironmentVariable("ZOMBIE_SHOTS");
            if (string.IsNullOrEmpty(dir) || cam == null) return;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "mapa_" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prev; RenderTexture.active = null; UnityEngine.Object.Destroy(tex); rt.Release();
        }

        [UnityTest]
        public IEnumerator DistantLights_WindowsInTheCity_HalosInTheForest([Values("calle_en_ruinas", MapLibrary.ForestId)] string id)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            var sky = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.SkylineLights"));
            var fog = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.FogBeacons"));
            if (id == "calle_en_ruinas")
            {
                Assert.IsNotNull(sky, "ventanas en los edificios lejanos");
                int facades = (int)sky.GetType().GetProperty("Facades").GetValue(sky), beacons = (int)sky.GetType().GetProperty("Beacons").GetValue(sky);
                Debug.Log("[Luces] " + id + ": " + facades + " fachadas con ventanas, " + beacons + " balizas rojas");
                Assert.Greater(facades, 30); Assert.Greater(beacons, 2);
                foreach (var l in ((Component)sky).GetComponentsInChildren<Light>(true)) Assert.Fail("sin luces reales: " + l.name);
                foreach (var r in ((Component)sky).GetComponentsInChildren<Renderer>(true)) Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, "sin sombras");
                Assert.IsNull(fog);
            }
            else
            {
                Assert.IsNull(sky, "el bosque no tiene skyline");
                Assert.IsNotNull(fog, "luces lejanas en la niebla");
                Assert.AreEqual(5, (int)fog.GetType().GetProperty("Count").GetValue(fog));
                float half = ((MapData)_arena.GetComponent(T("SevenDays.World.TestArena")).GetType().GetProperty("Map").GetValue(_arena.GetComponent(T("SevenDays.World.TestArena")))).halfSize;
                foreach (Transform h in ((Component)fog).transform) Assert.Greater(new Vector2(h.position.x, h.position.z).magnitude, half + 5f, "fuera del mapa");
            }
        }

        [UnityTest]
        public IEnumerator Loot_AfterSevenDaysAnd30DropsOutside_AllInsideOnNavMeshAndReachable([Values("calle_en_ruinas", MapLibrary.ForestId)] string id)
        {
            var log = new System.Collections.Generic.List<string>();
            Application.LogCallback cb = (msg, st, type) => { if (msg.Contains("Loot: ")) log.Add(msg); };
            Application.logMessageReceived += cb;
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); var arena = _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var map = (MapData)arena.GetType().GetProperty("Map").GetValue(arena);
            var lmT = T("SevenDays.World.Loot.LootManager");
            var lm = lmT.GetProperty("Instance").GetValue(null);
            Assert.IsTrue((bool)lmT.GetProperty("NavReady").GetValue(lm), "auditoría tras hornear el NavMesh");
            // 7 días de reposición (vaciando el suelo) y 30 drops de zombis, muchos fuera del perímetro o bajo el suelo
            var rng = new System.Random(4);
            var spawnForced = lmT.GetMethod("SpawnForced");
            for (int day = 2; day <= 7; day++)
            {
                lmT.GetMethod("NewDay").Invoke(lm, new object[] { day });
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * (map.halfSize + 10f), z = (float)(rng.NextDouble() * 2 - 1) * (map.halfSize + 10f);
                float y = i % 5 == 0 ? -3f : 0.35f;
                spawnForced.Invoke(lm, new object[] { LootItem.Ammo(AmmoType.Pistol, 10), new Vector3(x, y, z) });
            }
            yield return null;
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(map.playerStart[0], map.playerStart[1], map.playerStart[2]), out var start, 4f, NavMesh.AllAreas));
            int n = 0;
            foreach (var o in (System.Collections.IEnumerable)lmT.GetProperty("Active").GetValue(lm))
            {
                var c = (Component)o; if (c == null || !c.gameObject.activeSelf) continue;
                var pos = c.transform.position; n++;
                Assert.IsTrue(LootPlacement.Inside(map.halfSize, pos.x, pos.z), id + ": fuera del perímetro en " + pos);
                Assert.Greater(pos.y, -0.5f, id + ": bajo el suelo en " + pos);
                // se muestrea a la altura del objeto (como el juego): bajo la calzada hay otra capa de NavMesh desconectada a 2 cm
                Assert.IsTrue(NavMesh.SamplePosition(pos + Vector3.up * 0.35f, out var hit, 1.5f, NavMesh.AllAreas), id + ": fuera del NavMesh en " + pos);
                bool okPath = NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                Assert.IsTrue(okPath, id + ": inalcanzable en " + pos);
                Assert.IsTrue((bool)o.GetType().GetProperty("Reachable").GetValue(o));
            }
            int bad = (int)lmT.GetMethod("Audit").Invoke(lm, new object[] { "prueba" });
            Application.logMessageReceived -= cb;
            Debug.Log("[Botín] " + id + ": " + n + " pickups en el suelo tras 7 días y 30 drops; " + string.Join(" | ", log));
            Assert.Greater(n, 20);
            Assert.AreEqual(0, bad, "0 fuera de rango");
            Assert.IsTrue(log.Exists(l => l.Contains("al construir el mapa") && l.Contains(" 0 fuera de rango")), "auditoría al construir");
            Assert.IsTrue(log.Exists(l => l.Contains("reposición del día 7")), "auditoría al reponer");
        }
    }
}
