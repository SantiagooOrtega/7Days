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
    /// Bucle de días en la arena real (los 2 mapas jugables): empieza en preparación del día 1, la oleada genera zombis lejos del jugador y por el borde del mapa,
    /// y al saltar la oleada se pasa al resumen y luego al día 2 con su preparación.
    /// </summary>
    public class DayLoopTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        public static string[] Ids() => new[] { "calle_en_ruinas", MapLibrary.ForestId };

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
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            T("SevenDays.Combat.ZombieAudio").GetMethod("StopAll").Invoke(null, null);
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Wave_SpawnsAtEdges_ThenSummary_ThenDay2([ValueSource(nameof(Ids))] string id)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            var loopType = T("SevenDays.World.DayLoop");
            var inst = loopType.GetProperty("Instance");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && inst.GetValue(null) == null) yield return null;
            var loop = inst.GetValue(null);
            Assert.IsNotNull(loop, "DayLoop creado");
            var cycle = (DayCycle)loopType.GetProperty("Cycle").GetValue(loop);
            Assert.AreEqual(1, cycle.Day); Assert.AreEqual(DayPhase.Preparation, cycle.Phase);

            var skip = loopType.GetMethod("DebugSkip");
            skip.Invoke(loop, null);
            Assert.AreEqual(DayPhase.Wave, cycle.Phase);
            var wave = (WaveState)loopType.GetProperty("Wave").GetValue(loop);
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && wave.Spawned < 3) yield return null;
            Assert.GreaterOrEqual(wave.Spawned, 3, "la oleada genera zombis");

            var player = GameObject.FindWithTag("Player").transform;
            var mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
            var all = (IEnumerable)mgr.GetType().GetProperty("All").GetValue(mgr);
            int checkedZ = 0;
            foreach (Component z in all)
            {
                if (!(bool)z.GetType().GetProperty("InUse").GetValue(z)) continue;
                checkedZ++;
                // aparecen por el borde (dentro de unos metros del límite) y lejos del jugador (pueden haber caminado un poco)
                Assert.Greater(Vector3.Distance(z.transform.position, player.position), 8f, "lejos del jugador");
            }
            Assert.Greater(checkedZ, 0);
            Assert.AreEqual(wave.Plan.totalZombies - wave.Spawned + checkedZ, wave.Remaining(checkedZ));

            skip.Invoke(loop, null);
            Assert.AreEqual(DayPhase.Summary, cycle.Phase);
            yield return null;
            skip.Invoke(loop, null);
            Assert.AreEqual(2, cycle.Day); Assert.AreEqual(DayPhase.Preparation, cycle.Phase);
            yield return null;
        }
    
        /// <summary>
        /// Horda: durante la oleada ningún zombi se queda quieto más de 3 s (salvo atacando o aturdido). El jugador no se mueve y se cura cada frame para que siga vivo.
        /// </summary>
        [UnityTest]
        public IEnumerator Horde_NoZombieIdleMoreThan3s()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            var loopType = T("SevenDays.World.DayLoop");
            var inst = loopType.GetProperty("Instance");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && inst.GetValue(null) == null) yield return null;
            var loop = inst.GetValue(null);
            loopType.GetMethod("DebugSkip").Invoke(loop, null);
            var playerGo = GameObject.FindWithTag("Player");
            var health = playerGo.GetComponent(T("SevenDays.Core.Health"));
            var heal = health.GetType().GetMethod("Heal");
            var mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
            var all = (IEnumerable)mgr.GetType().GetProperty("All").GetValue(mgr);
            var anchor = new System.Collections.Generic.Dictionary<Component, (Vector3 pos, float time)>();
            float worst = 0f; string worstInfo = "";
            int seen = 0;
            float start = Time.time;
            while (Time.time - start < 25f)
            {
                heal.Invoke(health, new object[] { 1000f });
                foreach (Component z in all)
                {
                    bool inUse = (bool)z.GetType().GetProperty("InUse").GetValue(z);
                    var zh = z.GetType().GetProperty("Health").GetValue(z);
                    bool dead = (bool)zh.GetType().GetProperty("IsDead").GetValue(zh);
                    if (!inUse || dead) { anchor.Remove(z); continue; }
                    var state = (ZombieState)z.GetType().GetProperty("State").GetValue(z);
                    Vector3 p = z.transform.position;
                    if (!anchor.TryGetValue(z, out var a)) { anchor[z] = (p, Time.time); seen++; continue; }
                    if (state == ZombieState.Attack || state == ZombieState.Stunned || (p - a.pos).sqrMagnitude > 0.3f * 0.3f) { anchor[z] = (p, Time.time); continue; }
                    float idle = Time.time - a.time;
                    if (idle > worst) { worst = idle;  var rig = z.GetType().GetProperty("Rig").GetValue(z); worstInfo = z.name + " " + state + " horda=" + z.GetType().GetProperty("InHorde").GetValue(z) + " agente=" + z.GetType().GetProperty("UsesAgent").GetValue(z) + " emerge=" + rig.GetType().GetProperty("Emerging").GetValue(rig) + " en " + p + " a " + Vector3.Distance(p, playerGo.transform.position).ToString("0.0") + " m"; }
                }
                yield return null;
            }
            Debug.Log("[Horda] zombis vistos " + seen + ", máximo quieto " + worst.ToString("0.0") + " s " + worstInfo);
            Assert.Greater(seen, 3);
            Assert.LessOrEqual(worst, 3f, "zombi quieto: " + worstInfo);
        }
    }
}
