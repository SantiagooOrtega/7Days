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
    /// Audio de zombis en Unity: el banco convierte todas las muestras sintetizadas en AudioClip, el pool de 12 voces da prioridad al más cercano y descarta lo que
    /// pasa de 40 m, los nombres de clips propios se reconocen, y con 20 zombis en la arena real nunca suenan más de 12 voces a la vez.
    /// </summary>
    public class ZombieAudioTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _cam, _arena;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            if (_cam != null) UnityEngine.Object.Destroy(_cam);
            yield return null; yield return null;
        }

        private static object Call(Type t, string name, params object[] args) => t.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        private static int Voices => (int)T("SevenDays.Combat.ZombieAudio").GetProperty("ActiveVoices").GetValue(null);

        [UnityTest]
        public IEnumerator Bank_HasEveryEvent_AndPool_PrioritizesTheNearest()
        {
            _cam = new GameObject("Cam") { tag = "MainCamera" }; _cam.AddComponent<Camera>(); _cam.AddComponent<AudioListener>();
            var bank = T("SevenDays.Combat.ZombieSoundBank"); var audio = T("SevenDays.Combat.ZombieAudio");
            foreach (string t in ZombieAnatomies.Types) Call(bank, "Prewarm", t);
            Call(bank, "WaitAll");
            AudioClip sample = null;
            var pick = bank.GetMethod("Pick");
            foreach (string t in ZombieAnatomies.Types)
                foreach (var ev in ZombieSoundMap.EventsFor(t))
                    for (int tb = 0; tb < 2; tb++)
                    {
                        var args = new object[] { t, ev, tb, -1 };
                        var clip = (AudioClip)pick.Invoke(null, args);
                        Assert.IsNotNull(clip, t + " " + ev + " garganta " + tb);
                        Assert.Greater(clip.length, 0.1f);
                        sample = clip;
                    }
            long mb = (long)bank.GetProperty("Bytes").GetValue(null) / 1048576;
            Debug.Log("[ZombieAudio] clips " + bank.GetProperty("ClipCount").GetValue(null) + ", memoria " + mb + " MB");
            Assert.Less(mb, 40, "memoria de los clips");

            var play = audio.GetMethod("Play");
            object Play(float dist) => play.Invoke(null, new object[] { 1, null, new Vector3(0f, 0f, dist), sample, 1f, 1f, 8000f, false, 1.5f });
            for (int i = 0; i < 12; i++) Assert.IsTrue((bool)Play(30f), "voz libre " + i);
            Assert.IsFalse((bool)Play(35f), "más lejos que todas: no entra");
            Assert.IsTrue((bool)Play(2f), "más cerca: le quita la voz al más lejano");
            Assert.IsFalse((bool)Play(45f), "más allá de 40 m no suena");
            yield return null;
            Assert.LessOrEqual(Voices, 12);
        }

        [Test]
        public void CustomClipNames_AreRecognised()
        {
            var m = T("SevenDays.Combat.ZombieSoundBank").GetMethod("ParseOverrideName");
            bool Parse(string n, out string type, out ZombieSound ev)
            {
                var a = new object[] { n, null, null };
                bool ok = (bool)m.Invoke(null, a);
                type = (string)a[1]; ev = a[2] is ZombieSound s ? s : ZombieSound.Idle;
                return ok;
            }
            Assert.IsTrue(Parse("common_idle_1", out var t, out var e)); Assert.AreEqual("common", t); Assert.AreEqual(ZombieSound.Idle, e);
            Assert.IsTrue(Parse("Tank_Roar_2", out t, out e)); Assert.AreEqual("tank", t); Assert.AreEqual(ZombieSound.Roar, e);
            Assert.IsTrue(Parse("any_headshot", out t, out e)); Assert.AreEqual("any", t); Assert.AreEqual(ZombieSound.Headshot, e);
            Assert.IsTrue(Parse("boss_brute_roar_1", out t, out e)); Assert.AreEqual("boss_brute", t);
            Assert.IsFalse(Parse("dragon_idle_1", out _, out _));
            Assert.IsFalse(Parse("common_bailar_1", out _, out _));
            Assert.IsFalse(Parse("common_3_1", out _, out _));
        }

        [UnityTest]
        public IEnumerator TwentyZombies_NeverMoreThanTwelveVoices()
        {
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 120; i++) yield return null;
            var player = GameObject.FindWithTag("Player"); Assert.IsNotNull(player);
            var mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
            var spawn = Array.Find(mgr.GetType().GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            Call(T("SevenDays.Combat.ZombieSoundBank"), "WaitAll");
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                spawn.Invoke(mgr, new object[] { ZombieAnatomies.Types[i % 6], player.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (6f + i % 4), 1f, 1f, 1f, true });
            }
            int max = 0;
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until) { yield return null; max = Mathf.Max(max, Voices); }
            Debug.Log("[ZombieAudio] voces máximas con 20 zombis: " + max + ", descartados " + T("SevenDays.Combat.ZombieAudio").GetProperty("Dropped").GetValue(null));
            Assert.Greater(max, 0, "los zombis hacen ruido");
            Assert.LessOrEqual(max, 12);
        }
    }
}
