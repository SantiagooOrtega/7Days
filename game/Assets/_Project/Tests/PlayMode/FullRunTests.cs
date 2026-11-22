using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Partida de punta a punta en cada mapa: EMPEZAR desde la selección muestra la intro (se salta), arranca el Día 1, se mata la oleada entera, ningún
    /// cadáver queda con collider ni agente, y al morir el jugador no sigue sonando ningún zombi ni arma.
    /// </summary>
    public class FullRunTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena; private Component _fe;

        [UnitySetUp]
        public IEnumerator CleanStart()
        {
            // pruebas anteriores pueden dejar arena o jugador vivos: mataríamos al jugador equivocado
            foreach (var a in UnityEngine.Object.FindObjectsByType(T("SevenDays.World.TestArena"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)a).gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            var st = T("SevenDays.UI.StatsScreen"); st.GetMethod("Hide").Invoke(null, null);
            var sti = st.GetProperty("Instance").GetValue(null) as Component; if (sti != null) UnityEngine.Object.Destroy(sti.gameObject);
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            if (_fe != null) UnityEngine.Object.Destroy(_fe.gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            T("SevenDays.Intro.IntroPlayer").GetField("NewGameRequested").SetValue(null, false);
            T("SevenDays.Core.GameAudio").GetMethod("Restore").Invoke(null, null);
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        private static void Kill(Component c, float amount)
        {
            var h = c.GetComponent(T("SevenDays.Core.Health"));
            var di = Activator.CreateInstance(T("SevenDays.Core.DamageInfo")); di.GetType().GetField("Amount").SetValue(di, amount);
            h.GetType().GetMethod("TakeDamage").Invoke(h, new[] { di });
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Empezar_Intro_KillWave_NoBlockingCorpses_SilenceOnDeath([Values(0, 1)] int mapIndex)
        {
            var fe = T("SevenDays.UI.FrontEnd"); var ip = T("SevenDays.Intro.IntroPlayer");
            _fe = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(T("SevenDays.UI.FrontScreen"), "Select") });
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            yield return null;
            fe.GetMethod("Choose").Invoke(_fe, new object[] { 0, mapIndex });
            var list = fe.GetProperty("List").GetValue(_fe);
            list.GetType().GetMethod("Select").Invoke(list, new object[] { 2, false });
            list.GetType().GetMethod("Confirm").Invoke(list, null);   // EMPEZAR
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 10f && !(bool)ip.GetProperty("Active").GetValue(null)) yield return null;
            Assert.IsTrue((bool)ip.GetProperty("Active").GetValue(null), "la intro aparece al pulsar EMPEZAR");
            ip.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(UnityEngine.Object.FindFirstObjectByType(ip), null);   // SALTAR

            // Día 1
            Component loop = null; t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f && (loop = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")) as Component) == null) yield return null;
            Assert.IsNotNull(loop, "arranca el Día 1");
            string mapId = (string)T("SevenDays.World.MapIO").GetField("SelectedId").GetValue(null);
            Assert.AreEqual(((string[])T("SevenDays.World.MapIO").GetField("PlayableIds").GetValue(null))[mapIndex], mapId);
            object Phase() { var c = loop.GetType().GetProperty("Cycle").GetValue(loop); return c.GetType().GetProperty("Phase").GetValue(c); }

            // mata la oleada entera (más deprisa)
            Time.timeScale = 3f;
            var mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
            var all = (IEnumerable)mgr.GetType().GetProperty("All").GetValue(mgr);
            int kills = 0, corpses = 0; t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 240f && Phase().ToString() != "Summary" && Phase().ToString() != "Victory")
            {
                foreach (Component z in all)
                {
                    bool inUse = (bool)z.GetType().GetProperty("InUse").GetValue(z);
                    var h = z.GetComponent(T("SevenDays.Core.Health"));
                    if (inUse && !(bool)h.GetType().GetProperty("IsDead").GetValue(h)) { Kill(z, 1e6f); kills++; }
                }
                for (int i = 0; i < 10; i++) yield return null;
                // ningún cadáver bloquea: sin colliders activos, sin agente, sin obstáculo
                foreach (Component z in all)
                {
                    if (!(bool)z.GetType().GetProperty("IsCorpse").GetValue(z)) continue;
                    corpses++;
                    foreach (var c in z.GetComponentsInChildren<Collider>(false)) Assert.IsFalse(c.enabled, mapId + ": collider activo en un cadáver: " + c.name);
                    var ag = z.GetComponent<NavMeshAgent>(); Assert.IsTrue(ag == null || !ag.enabled, mapId + ": cadáver con agente");
                    Assert.IsNull(z.GetComponentInChildren<NavMeshObstacle>(), mapId + ": cadáver con obstáculo");
                }
            }
            Time.timeScale = 1f;
            Assert.AreEqual("Summary", Phase().ToString(), mapId + ": oleada superada (" + kills + " bajas)");
            Assert.Greater(kills, 5);
            Assert.Greater(corpses, 0, "se revisaron cadáveres");

            // muere el jugador: silencio del juego
            Kill(GameObject.FindWithTag("Player").GetComponent(T("SevenDays.Core.Health")), 1e6f);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.AreEqual("Defeat", Phase().ToString(), mapId + ": derrota");
            Assert.IsTrue((bool)T("SevenDays.Core.AudioMix").GetProperty("GameplayMuted").GetValue(null), mapId + ": juego silenciado");
            Assert.AreEqual(0, (int)T("SevenDays.Combat.ZombieAudio").GetProperty("ActiveVoices").GetValue(null), mapId + ": voces de zombi calladas");
            var keep = T("SevenDays.Core.GameAudio").GetMethod("Keep");
            foreach (var s in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && !(bool)keep.Invoke(null, new object[] { s })) Assert.Fail(mapId + ": sigue sonando " + s.gameObject.name + " (" + (s.clip != null ? s.clip.name : "sin clip") + ", bucle " + s.loop + ")");
        }
    }
}
