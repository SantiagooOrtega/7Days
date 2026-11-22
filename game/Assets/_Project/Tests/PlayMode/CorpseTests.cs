using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Cadáveres intangibles: al morir un zombi no le queda ningún collider activo, ni agente ni obstáculo del NavMesh; las balas (rayos) lo atraviesan y dan
    /// al zombi de detrás; un zombi muerto en la escalera no impide subir a otro; vuelven al pool a los ~15 s y nunca hay más de 20 a la vista.
    /// </summary>
    public class CorpseTests
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
            Time.timeScale = 1f;
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        private IEnumerator Load()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static object Mgr => T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
        private static Component Spawn(Vector3 p)
        {
            var zm = T("SevenDays.Combat.ZombieManager");
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            return (Component)spawn.Invoke(Mgr, new object[] { "common", p, 1f, 1f, 1f, false });
        }
        private static void Kill(Component z)
        {
            var h = z.GetComponent(T("SevenDays.Core.Health"));
            var di = Activator.CreateInstance(T("SevenDays.Core.DamageInfo"));
            di.GetType().GetField("Amount").SetValue(di, 1e6f);
            h.GetType().GetMethod("TakeDamage").Invoke(h, new[] { di });
        }
        private static void Place(GameObject player, Vector3 p) { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = p; cc.enabled = true; }

        [UnityTest, Timeout(300000)]
        public IEnumerator Corpse_IsIntangible_BulletsPassThrough_DoesNotBlockTheStairs()
        {
            yield return Load();
            var map = (MapData)_arena.GetComponent(T("SevenDays.World.TestArena")).GetType().GetProperty("Map").GetValue(_arena.GetComponent(T("SevenDays.World.TestArena")));
            var h = map.houses[0]; var w = HousePlanner.Stairwell(h); float y2 = HousePlanner.UpperFloorY(h), zc = (w[2] + w[3]) / 2f;
            Vector3 W(float lx, float ly, float lz) => new Vector3(h.x + lx, ly, h.z + lz);
            var player = GameObject.FindWithTag("Player");
            Place(player, W(-2.5f, y2 + 0.05f, -1.5f));

            // un zombi muere a media escalera
            float xm = (w[0] + w[1]) / 2f, ym = y2 / 2f;
            var dead = Spawn(W(xm, ym + 0.1f, zc));
            for (int i = 0; i < 90; i++) yield return null;
            Kill(dead);
            for (int i = 0; i < 30; i++) yield return null;
            foreach (var c in dead.GetComponentsInChildren<Collider>(true)) Assert.IsFalse(c.enabled && c.gameObject.activeInHierarchy, "collider activo en el cadáver: " + c.name);
            var ag = dead.GetComponent<NavMeshAgent>(); Assert.IsTrue(ag == null || !ag.enabled, "sin agente (no entra en la evasión de los demás)");
            Assert.IsNull(dead.GetComponentInChildren<NavMeshObstacle>(), "sin obstáculo del NavMesh");
            Assert.IsTrue(dead.gameObject.activeSelf, "el cadáver sigue a la vista");

            // las balas lo atraviesan: un rayo a la altura del pecho no toca el cadáver
            Vector3 from = dead.transform.position + new Vector3(-3f, 0.4f, 0f), to = dead.transform.position + new Vector3(3f, 0.4f, 0f);
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, 6f, ~0, QueryTriggerInteraction.Collide))
                Assert.IsFalse(hit.collider.transform.IsChildOf(dead.transform), "la bala no choca con el cadáver: " + hit.collider.name);

            // otro zombi sube por encima del cadáver hasta el jugador
            var z = Spawn(W(2.8f, 0.2f, -1.6f));
            z.GetType().GetMethod("JoinHorde").Invoke(z, new object[] { 0f, 1f });
            float t1 = Time.time; bool up = false;
            while (Time.time - t1 < 120f && !up) { yield return null; up = z.transform.position.y > y2 - 0.3f; }
            Assert.IsTrue(up, "un zombi muerto en la escalera no impide subir a otro (acaba en " + z.transform.position + ")");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Corpses_ReturnToPool_AtMost20Visible()
        {
            yield return Load();
            var player = GameObject.FindWithTag("Player");
            var zs = new System.Collections.Generic.List<Component>();
            for (int i = 0; i < 24; i++) { var z = Spawn(new Vector3(-20f + (i % 8) * 2f, 0.2f, -22f + (i / 8) * 2f)); if (z != null) zs.Add(z); }
            for (int i = 0; i < 20; i++) yield return null;
            foreach (var z in zs) { Kill(z); yield return null; }
            for (int i = 0; i < 10; i++) yield return null;
            int visible = (int)Mgr.GetType().GetProperty("VisibleCorpses").GetValue(Mgr);
            Assert.LessOrEqual(visible, CorpseRules.MaxVisible, "como mucho 20 cadáveres a la vista");
            Assert.Greater(visible, 10);
            Time.timeScale = 4f;   // espera los 15 s más deprisa
            float t0 = Time.time;
            while (Time.time - t0 < CorpseRules.Seconds + 1f) yield return null;
            Time.timeScale = 1f;
            int inUse = 0; foreach (var z in zs) if ((bool)z.GetType().GetProperty("InUse").GetValue(z)) inUse++;
            Assert.AreEqual(0, inUse, "todos volvieron al pool");
        }
    }
}
