using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Escalera de la casa con piso 2 libre de verdad: el jugador (su CharacterController, con colisiones reales) sube y baja sin quedarse trabado, y un zombi
    /// sube persiguiéndolo y otro baja. En los mapas sin piso 2 (Bosque) no hay escalera que probar.
    /// </summary>
    public class StairsTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        [UnitySetUp]
        public IEnumerator CleanStart()
        {
            foreach (var a in UnityEngine.Object.FindObjectsByType(T("SevenDays.World.TestArena"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)a).gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        private IEnumerator Load(string id)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static MapData Map(GameObject arena) => (MapData)arena.GetComponent(T("SevenDays.World.TestArena")).GetType().GetProperty("Map").GetValue(arena.GetComponent(T("SevenDays.World.TestArena")));

        private static void Place(GameObject player, Vector3 p) { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = p; cc.enabled = true; }

        /// <summary>Lleva al jugador por los puntos moviendo su CharacterController (como al andar); falla si se queda trabado 2 s seguidos.</summary>
        private static IEnumerator Walk(GameObject player, Vector3[] pts, Action<bool> done)
        {
            var cc = player.GetComponent<CharacterController>();
            int k = 0; float stuck = 0f, t0 = Time.time; Vector3 last = player.transform.position;
            while (k < pts.Length && Time.time - t0 < 25f)
            {
                var to = pts[k] - player.transform.position; to.y = 0f;
                if (to.magnitude < 0.25f) { k++; continue; }
                cc.Move(to.normalized * 3f * Time.deltaTime + Vector3.down * 4f * Time.deltaTime);
                yield return null;
                if ((player.transform.position - last).magnitude < 0.002f) { stuck += Time.deltaTime; if (stuck > 2f) break; } else stuck = 0f;
                last = player.transform.position;
            }
            done(k >= pts.Length);
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator PlayerAndZombie_GoUpAndDownTheStairs([Values("calle_en_ruinas", "bosque_oscuro")] string id)
        {
            yield return Load(id);
            var map = Map(_arena);
            var h = Array.Find(map.houses, x => x.upperFloor);
            if (h == null) { Assert.Pass(id + ": sin casa de dos pisos (no hay escalera)"); yield break; }
            var w = HousePlanner.Stairwell(h); float y2 = HousePlanner.UpperFloorY(h), zc = (w[2] + w[3]) / 2f;
            Vector3 W(float lx, float ly, float lz) => new Vector3(h.x + lx, ly, h.z + lz);
            var foot = HousePlanner.StairFoot(h); var top = HousePlanner.StairTop(h);
            Vector3 footW = W(foot.X, 0.3f, foot.Z), topW = W(top.X, y2 + 0.3f, top.Z);
            Vector3 bottomStep = W(w[1] + 0.3f, 0.3f, zc), upperStep = W(w[0] - 0.4f, y2 + 0.3f, zc);
            var player = GameObject.FindWithTag("Player");

            // jugador: sube y baja
            Place(player, footW);
            bool ok = false;
            yield return Walk(player, new[] { bottomStep, W((w[0] + w[1]) / 2f, 0f, zc), upperStep, topW }, r => ok = r);
            Assert.IsTrue(ok, id + ": el jugador sube sin trabarse (acaba en " + player.transform.position + ")");
            Assert.Greater(player.transform.position.y, y2 - 0.3f, "arriba");
            yield return Walk(player, new[] { upperStep, W((w[0] + w[1]) / 2f, 0f, zc), bottomStep, footW }, r => ok = r);
            Assert.IsTrue(ok, id + ": el jugador baja sin trabarse (acaba en " + player.transform.position + ")");
            Assert.Less(player.transform.position.y, 0.8f, "abajo");

            // zombi: sube hasta el jugador (arriba) y otro baja hasta él (abajo)
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            Place(player, W(-2.5f, y2 + 0.05f, -1.5f));
            var z1 = (Component)spawn.Invoke(mgr, new object[] { "common", W(2.8f, 0.2f, -1.6f), 1f, 1f, 1f, true });
            z1.GetType().GetMethod("JoinHorde").Invoke(z1, new object[] { 0f, 1f });   // como en una oleada: va a por el jugador (sin depender de que lo vea)
            float t1 = Time.time; bool up = false;
            float nextLog = 10f;
            while (Time.time - t1 < 120f && !up)
            {
                yield return null; up = z1.transform.position.y > y2 - 0.3f;
                if (Time.time - t1 > nextLog) { nextLog += 10f; var ag = z1.GetComponent<UnityEngine.AI.NavMeshAgent>(); Debug.Log("[Escalera z1] t=" + (Time.time - t1).ToString("0") + " pos=" + z1.transform.position + " estado=" + z1.GetType().GetProperty("State")?.GetValue(z1) + (ag != null && ag.enabled && ag.isOnNavMesh ? " dest=" + ag.destination + " path=" + ag.pathStatus : " sin agente")); }
            }
            Assert.IsTrue(up, id + ": el zombi sube (acaba en " + z1.transform.position + ")");
            z1.gameObject.SetActive(false);
            Place(player, W(2.8f, 0.2f, -1.6f));
            var z2 = (Component)spawn.Invoke(mgr, new object[] { "common", W(-2f, y2 + 0.1f, -1.5f), 1f, 1f, 1f, true });
            z2.GetType().GetMethod("JoinHorde").Invoke(z2, new object[] { 0f, 1f });
            t1 = Time.time; bool down = false;
            while (Time.time - t1 < 120f && !down) { yield return null; down = z2.transform.position.y < 0.6f; }
            Assert.IsTrue(down, id + ": el zombi baja (acaba en " + z2.transform.position + ")");
        }
    }
}
