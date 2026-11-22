using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Al morir el jugador no sigue sonando nada del juego: ningún AudioSource de zombi, arma, fuego o ambiente reproduciéndose, el pool de voces vacío y
    /// los grupos del juego a 0; la música sube a volumen de menú. Al reintentar (recarga de escena) el sonido vuelve.
    /// </summary>
    public class EndAudioTests
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
            var st = T("SevenDays.UI.StatsScreen"); st.GetMethod("Hide").Invoke(null, null);
            var inst = st.GetProperty("Instance").GetValue(null) as Component; if (inst != null) UnityEngine.Object.Destroy(inst.gameObject);
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            T("SevenDays.Core.GameAudio").GetMethod("Restore").Invoke(null, null);
            var mm = T("SevenDays.Core.MusicManager").GetProperty("Instance").GetValue(null) as Component; if (mm != null) UnityEngine.Object.Destroy(mm.gameObject);   // vive entre escenas: que la siguiente prueba lo cree de cero
            yield return null; yield return null;
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator AfterDeath_NoZombieOrWeaponSoundPlays_MusicAtMenuLevel()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            var music = T("SevenDays.Core.MusicManager").GetMethod("Ensure").Invoke(null, null);
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var player = GameObject.FindWithTag("Player");
            // zombis cerca, gruñendo y persiguiendo
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            for (int i = 0; i < 6; i++)
            {
                var z = (Component)spawn.Invoke(mgr, new object[] { "common", player.transform.position + new Vector3(4f + i, 0.2f, 3f), 1f, 1f, 1f, false });
                z.GetType().GetMethod("JoinHorde").Invoke(z, new object[] { 0f, 1f });
            }
            for (int i = 0; i < 120; i++) yield return null;
            // muere el jugador
            var h = player.GetComponent(T("SevenDays.Core.Health"));
            var di = Activator.CreateInstance(T("SevenDays.Core.DamageInfo")); di.GetType().GetField("Amount").SetValue(di, 100000f);
            h.GetType().GetMethod("TakeDamage").Invoke(h, new[] { di });
            for (int i = 0; i < 180; i++) yield return null;   // 3 s: los zombis siguen vivos y cerca

            Assert.IsTrue((bool)T("SevenDays.Core.AudioMix").GetProperty("GameplayMuted").GetValue(null), "grupos del juego a 0");
            Assert.AreEqual(0, (int)T("SevenDays.Combat.ZombieAudio").GetProperty("ActiveVoices").GetValue(null), "pool de voces de zombi vacío");
            var keep = T("SevenDays.Core.GameAudio").GetMethod("Keep");
            foreach (var s in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && !(bool)keep.Invoke(null, new object[] { s })) Assert.Fail("sigue sonando " + s.gameObject.name + " (" + (s.clip != null ? s.clip.name : "-") + ")");
            Assert.AreEqual(1f, (float)music.GetType().GetProperty("Target").GetValue(music), 0.61f, "música a volumen de menú (o con VIGÍA hablando)");
            Assert.AreEqual("Dead", music.GetType().GetProperty("Phase").GetValue(music).ToString());

            // reintentar: la escena se recarga y el sonido vuelve
            T("SevenDays.Core.SceneStatics").GetMethod("ResetAll").Invoke(null, null);
            Assert.IsFalse((bool)T("SevenDays.Core.AudioMix").GetProperty("GameplayMuted").GetValue(null), "al reintentar vuelve el sonido");
        }
    }
}
