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
            var sky2 = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.NightSkyRenderer"));
            Assert.IsNotNull(sky2, "luna y estrellas");
            Assert.Greater((int)sky2.GetType().GetProperty("StarCount").GetValue(sky2), 100);
            var mdir = (Vector3)sky2.GetType().GetProperty("MoonDirection").GetValue(sky2);
            Assert.Greater(Vector3.Dot(mdir, -RenderSettings.sun.transform.forward), 0.99f, "la luna está donde la luz principal");
            Debug.Log("[Cielo] " + id + ": visibilidad " + ((float)sky2.GetType().GetProperty("Visible").GetValue(sky2)).ToString("0.00"));
            var per = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.PerimeterBarrier"));
            Assert.IsNotNull(per, "perímetro trinchera/troncos");
            Assert.Greater((int)per.GetType().GetProperty("Instances").GetValue(per), 500);
            Debug.Log("[Perímetro] " + id + ": " + T("SevenDays.World.PerimeterBarrier").GetMethod("Describe").Invoke(null, null));
            foreach (var wn in new[] { "Wall N", "Boundary N" })
            {
                var w = GameObject.Find(wn); if (w == null) continue;
                Assert.IsTrue(w.GetComponent<Collider>().enabled, "el muro sigue bloqueando");
                Assert.IsFalse(w.GetComponent<Renderer>().enabled, "el muro liso ya no se ve");
            }
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

        [UnityTest]
        public IEnumerator HouseUpperFloor_ZombiesReachItByTheStairs_TankDoesNot_LootUpstairs()
        {
            var log = new System.Collections.Generic.List<string>();
            Application.LogCallback cb = (msg, st, type) => { if (type != LogType.Log || msg.Contains("Auditoría") || msg.Contains("house_upper")) log.Add(type + ": " + msg); };
            Application.logMessageReceived += cb;
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); var arena = _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            Application.logMessageReceived -= cb;
            var map = (MapData)arena.GetType().GetProperty("Map").GetValue(arena);
            var h = map.houses[0];
            Assert.IsTrue(h.upperFloor, "la casa de la Calle tiene piso 2");
            float y2 = HousePlanner.UpperFloorY(h);
            Vector3 W(float lx, float ly, float lz) => new Vector3(h.x + lx, ly, h.z + lz);   // yaw 0
            var start = new Vector3(map.playerStart[0], map.playerStart[1], map.playerStart[2]);
            Assert.IsTrue(NavMesh.SamplePosition(start, out var s0, 4f, NavMesh.AllAreas));
            var targets = new[] { ("centro del piso 2", W(-2f, y2, -1.5f)), ("balcón", W(-2.5f, y2, -h.depth / 2f - 1.1f)), ("llegada de la escalera", W(HousePlanner.StairTop(h).X - 0.3f, y2, HousePlanner.StairTop(h).Z)) };
            var path = new NavMeshPath();
            foreach (var (name, t) in targets)
            {
                Assert.IsTrue(NavMesh.SamplePosition(t + Vector3.up * 0.3f, out var hit, 0.8f, NavMesh.AllAreas), name + ": NavMesh arriba");
                Assert.Greater(hit.position.y, y2 - 0.3f, name + ": en el piso 2, no en el de abajo");
                Assert.IsTrue(NavMesh.CalculatePath(s0.position, hit.position, NavMesh.AllAreas, path), name);
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, name + ": los zombis llegan por la escalera");
            }
            // el tanque (otro tipo de agente) no sube
            var zm = T("SevenDays.Combat.ZombieManager"); var inst = zm.GetProperty("Instance").GetValue(null);
            int giant = (int)zm.GetProperty("GiantAgentTypeId").GetValue(inst);
            if (giant >= 0)
            {
                var f = new NavMeshQueryFilter { agentTypeID = giant, areaMask = NavMesh.AllAreas };
                bool up = NavMesh.SamplePosition(targets[0].Item2 + Vector3.up * 0.3f, out var gh, 0.8f, f) && gh.position.y > y2 - 0.3f
                    && NavMesh.SamplePosition(start, out var gs, 6f, f) && NavMesh.CalculatePath(gs.position, gh.position, f, path) && path.status == NavMeshPathStatus.PathComplete;
                Assert.IsFalse(up, "el tanque no sube al piso 2");
            }
            // auditoría de colisiones limpia y la caja de arriba es buscable
            Assert.IsFalse(log.Exists(l => (l.StartsWith("Warning") || l.StartsWith("Error")) && !l.Contains("sin sesión")), string.Join("\n", log));
            Assert.IsTrue(log.Exists(l => l.Contains("Auditoría de colisiones") && l.Contains("Todo en orden")), "auditoría N/N sin avisos");
            var lm = T("SevenDays.World.Loot.LootManager").GetProperty("Instance").GetValue(null);
            Assert.AreEqual(0, (int)lm.GetType().GetField("SkippedPoints").GetValue(lm), "todos los muebles con botín encontrados (también arriba)");
        }

        [UnityTest]
        public IEnumerator Grenade_ThrowCountsDown_ExplodesAfterTheFuse_KillsCommonsAround()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var player = GameObject.FindWithTag("Player");
            var gmT = T("SevenDays.Combat.GrenadeManager"); var gm = gmT.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(gm, "gestor de granadas");
            var inv = player.GetComponent(T("SevenDays.Player.PlayerInventory"));
            var pouch = (GrenadePouch)inv.GetType().GetProperty("Grenades").GetValue(inv);
            Assert.AreEqual(GrenadeRules.Start, pouch.Count, "empiezas con 2");
            // tres comunes juntos lejos del jugador: una explosión entre ellos los mata
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            var center = player.transform.position + player.transform.forward * 12f;
            var zs = new System.Collections.Generic.List<Component>();
            foreach (var off in new[] { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0.5f), new Vector3(0f, 0f, -1.2f) })
                zs.Add((Component)spawn.Invoke(mgr, new object[] { "common", center + off, 1f, 1f, 1f, true }));
            yield return null;
            int killsBefore = (int)gmT.GetProperty("Kills").GetValue(gm);
            int k = (int)gmT.GetMethod("Explode").Invoke(gm, new object[] { center + Vector3.up * 0.2f });
            Assert.AreEqual(3, k, "los tres comunes mueren");
            Assert.AreEqual(killsBefore + 3, (int)gmT.GetProperty("Kills").GetValue(gm), "se cuentan las bajas por granada");
            // lanzar: el contador baja, vuela y explota a los 2.5 s
            var pg = player.GetComponent(T("SevenDays.Player.PlayerGrenade"));
            Assert.IsNotNull(pg, "F para lanzar");
            int n = (int)pg.GetType().GetMethod("Predict").Invoke(pg, new object[] { Vector3.zero, Vector3.zero });
            Assert.Greater(n, 2, "trayectoria");
            Assert.IsTrue((bool)pg.GetType().GetMethod("ThrowNow").Invoke(pg, null));
            Assert.AreEqual(GrenadeRules.Start - 1, pouch.Count);
            Assert.AreEqual(1, (int)gmT.GetProperty("Airborne").GetValue(gm));
            int det = (int)gmT.GetProperty("Detonations").GetValue(gm);
            float t1 = Time.time;
            while (Time.time - t1 < GrenadeRules.FuseSeconds + 1f && (int)gmT.GetProperty("Detonations").GetValue(gm) == det) yield return null;
            Assert.AreEqual(det + 1, (int)gmT.GetProperty("Detonations").GetValue(gm), "explotó");
            Assert.That(Time.time - t1, Is.InRange(GrenadeRules.FuseSeconds - 0.2f, GrenadeRules.FuseSeconds + 0.3f), "mecha de 2.5 s");
            Assert.AreEqual(0, (int)gmT.GetProperty("Airborne").GetValue(gm), "vuelve al pool");
        }

        [UnityTest]
        public IEnumerator Zombie_WalksUpTheStairs_NoTeleport()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); var arena = _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var map = (MapData)arena.GetType().GetProperty("Map").GetValue(arena);
            var h = map.houses[0]; float y2 = HousePlanner.UpperFloorY(h);
            Vector3 W(float lx, float ly, float lz) => new Vector3(h.x + lx, ly, h.z + lz);
            Assert.IsNull(UnityEngine.Object.FindObjectsByType(Type.GetType("Unity.AI.Navigation.NavMeshLink, Unity.AI.Navigation"), FindObjectsSortMode.None) is UnityEngine.Object[] ls ? Array.Find(ls, l => l.name == "Link stairs") : null, "sin enlace que salte: la escalera se hornea");
            // el jugador espera arriba; un zombi común abajo, en la habitación este
            var player = GameObject.FindWithTag("Player");
            var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            player.transform.position = W(-2.5f, y2 + 0.05f, -1.5f);
            if (cc != null) cc.enabled = true;
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            var z = (Component)spawn.Invoke(mgr, new object[] { "common", W(2.8f, 0.2f, -1.6f), 1f, 1f, 1f, true });
            Assert.IsNotNull(z);
            Vector3 prev = z.transform.position; float maxStep = 0f, t1 = Time.time; bool up = false; int frames = 0;
            while (Time.time - t1 < 60f)   // tarda 9-25 s según cuándo termina de emerger
            {
                yield return null; frames++;
                var p = z.transform.position;
                float step = Vector3.Distance(p, prev); if (frames > 3) maxStep = Mathf.Max(maxStep, step / Mathf.Max(1f, Time.deltaTime * 60f));
                prev = p;
                if (p.y > y2 - 0.3f) { up = true; break; }
            }
            Debug.Log("[Escalera] subió=" + up + " en " + (Time.time - t1).ToString("0.0") + " s, salto máximo por frame (60 fps) " + maxStep.ToString("0.000") + " m, final " + z.transform.position);
            Assert.IsTrue(up, "el zombi llega al piso 2");
            Assert.Less(maxStep, 0.35f, "posición continua: sin teletransporte");
        }

        [UnityTest]
        public IEnumerator Death_ShowsTacticalStatsScreen_BlocksEsc()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var player = GameObject.FindWithTag("Player");
            var h = player.GetComponent(T("SevenDays.Core.Health"));
            var di = Activator.CreateInstance(T("SevenDays.Core.DamageInfo"));
            di.GetType().GetField("Amount").SetValue(di, 100000f);
            h.GetType().GetMethod("TakeDamage").Invoke(h, new[] { di });
            for (int i = 0; i < 90; i++) yield return null;
            var st = T("SevenDays.UI.StatsScreen");
            Assert.IsTrue((bool)st.GetProperty("Active").GetValue(null), "pantalla de muerte");
            Assert.IsTrue((bool)st.GetProperty("Blocking").GetValue(null));
            Assert.IsTrue((bool)T("SevenDays.Core.GameInput").GetField("EscapeBlocked").GetValue(null), "Esc no abre los ajustes");
            var inst = st.GetProperty("Instance").GetValue(null);
            Assert.AreEqual(1, (int)st.GetProperty("Rows").GetValue(inst), "una fila por día jugado");
            var scaler = ((Component)inst).GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(3, ((Component)inst).GetComponentsInChildren<UnityEngine.UI.Button>().Length, "Reintentar día, Menú, Ver estadísticas");
            T("SevenDays.UI.StatsScreen").GetMethod("Hide").Invoke(null, null);
            UnityEngine.Object.Destroy((Component)inst);
            yield return null;
        }
    }
}
