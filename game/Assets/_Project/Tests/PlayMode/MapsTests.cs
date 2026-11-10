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
    }
}
