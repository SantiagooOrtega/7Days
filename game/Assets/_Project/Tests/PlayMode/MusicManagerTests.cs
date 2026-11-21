using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>Un solo MusicManager que sobrevive a recargas, con la pista en bucle, volumen alto en menú y ~40 % mientras VIGÍA narra la intro.</summary>
    public class MusicManagerTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [UnityTest]
        public IEnumerator SingleManager_Loops_SurvivesReload_DucksForIntroNarration()
        {
            var mt = T("SevenDays.Core.MusicManager");
            var a = (Component)mt.GetMethod("Ensure").Invoke(null, null);
            var b = (Component)mt.GetMethod("Ensure").Invoke(null, null);
            Assert.AreSame(a, b, "un solo manager");
            var dup = new GameObject("Música duplicada").AddComponent(mt);   // como si una escena recargada creara otro
            yield return null;
            Assert.IsTrue(dup == null, "el duplicado se destruye");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType(mt, FindObjectsSortMode.None).Length);
            Assert.AreEqual("DontDestroyOnLoad", a.gameObject.scene.name, "vive entre escenas (R, reintentar, M)");

            var clip = (AudioClip)mt.GetProperty("Clip").GetValue(a);
            var src = (AudioSource)mt.GetProperty("Source").GetValue(a);
            Assert.IsNotNull(clip, "musica_juego enlazada (Resources/Audio/MusicLibrary)");
            Assert.AreEqual("musica_juego", clip.name);
            Assert.IsTrue(src.loop, "en bucle");
            Assert.IsTrue(src.isPlaying, "suena desde el arranque");
            Assert.AreEqual("Menu", mt.GetProperty("Phase").GetValue(a).ToString());
            Assert.AreEqual(1f, (float)mt.GetProperty("Level").GetValue(a), 1e-3f, "alto en el menú");

            // intro: VIGÍA habla en la lámina 6 y la música baja a ~40 %
            var ip = T("SevenDays.Intro.IntroPlayer");
            var seen = ip.GetProperty("Seen"); bool seenBefore = (bool)seen.GetValue(null);
            var intro = (Component)ip.GetMethod("Play").Invoke(null, new object[] { null });
            for (int i = 0; i < 5; i++) { ip.GetMethod("Next").Invoke(intro, null); yield return new WaitForSecondsRealtime(0.6f); }
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.IsTrue((bool)ip.GetProperty("Speaking").GetValue(null), "VIGÍA narra");
            Assert.AreEqual(0.4f, (float)mt.GetProperty("Target").GetValue(a), 1e-3f, "ducking al 40 %");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.Less((float)mt.GetProperty("Level").GetValue(a), 0.7f, "baja con fundido suave");
            ip.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(intro, null);
            seen.SetValue(null, seenBefore);
            yield return null;
            Assert.IsTrue(src.isPlaying, "sin cortes al terminar la intro");
            UnityEngine.Object.Destroy(a.gameObject);
        }
    }
}
