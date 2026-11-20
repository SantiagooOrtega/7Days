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
    }
}
