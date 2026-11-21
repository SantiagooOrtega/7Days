using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>La intro en cómic: 8 láminas, VIGÍA solo habla desde la 6, avanzar y retroceder, y al saltarla lo deja todo como estaba.</summary>
    public class IntroTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [UnityTest]
        public IEnumerator Intro_EightPanels_NextBack_VigiaOn6_SkipRestoresInput()
        {
            var pt = T("SevenDays.Intro.IntroPlayer");
            var seenProp = pt.GetProperty("Seen");
            bool seenBefore = (bool)seenProp.GetValue(null);
            var player = (Component)pt.GetMethod("Play").Invoke(null, new object[] { null });
            Assert.IsNotNull(player);
            Assert.IsTrue((bool)pt.GetProperty("Active").GetValue(null));
            var input = T("SevenDays.Core.GameInput").GetField("InputCaptured");
            Assert.IsTrue((bool)input.GetValue(null), "el jugador no se mueve durante la intro");
            var seq = pt.GetField("_seq", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
            var panels = (IList)seq.GetType().GetField("panels").GetValue(seq);
            Assert.AreEqual(8, panels.Count);
            foreach (var p in panels) Assert.IsNotNull(p.GetType().GetField("image").GetValue(p), "imagen (o provisional)");
            var index = pt.GetProperty("Index"); var sub = pt.GetProperty("Subtitle");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("", (string)sub.GetValue(player), "sin texto superpuesto en la lámina 1");
            pt.GetMethod("Next").Invoke(player, null);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(1, (int)index.GetValue(player), "clic o Espacio avanza");
            pt.GetMethod("Back").Invoke(player, null);
            yield return null;
            Assert.AreEqual(0, (int)index.GetValue(player), "flecha izquierda retrocede");
            for (int i = 0; i < 5; i++) { pt.GetMethod("Next").Invoke(player, null); yield return new WaitForSecondsRealtime(0.6f); }
            Assert.AreEqual(5, (int)index.GetValue(player), "lámina 6");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("Señal vital detectada.", (string)sub.GetValue(player), "VIGÍA se presenta con subtítulos");
            pt.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null);
            yield return null;
            Assert.IsFalse((bool)pt.GetProperty("Active").GetValue(null));
            Assert.IsFalse((bool)input.GetValue(null));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue((bool)seenProp.GetValue(null), "queda guardado que se vio");
            seenProp.SetValue(null, seenBefore);
        }

        [UnityTest]
        public IEnumerator Epilogue_PlaysFinalPage_VigiaNarrates_ThenVictory()
        {
            var pt = T("SevenDays.Intro.IntroPlayer");
            var seenProp = pt.GetProperty("Seen"); bool seenBefore = (bool)seenProp.GetValue(null);
            seenProp.SetValue(null, false);
            bool done = false;
            var player = (Component)pt.GetMethod("PlayEpilogue").Invoke(null, new object[] { null, (Action)(() => done = true) });
            Assert.IsNotNull(player);
            var seq = pt.GetField("_seq", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
            var panels = (IList)seq.GetType().GetField("panels").GetValue(seq);
            Assert.AreEqual(1, panels.Count);
            var img = (Texture2D)panels[0].GetType().GetField("image").GetValue(panels[0]);
            Assert.AreEqual("final_01", img.name, "la lámina final");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.AreEqual("Transmisión enviada. Todo el mundo sabrá qué hicieron.", (string)pt.GetProperty("Subtitle").GetValue(player));
            Assert.IsFalse(done, "sigue en el epílogo");
            pt.GetMethod("Next").Invoke(player, null);   // clic o Espacio: continuar
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsTrue(done, "después, la pantalla de victoria");
            Assert.IsFalse((bool)pt.GetProperty("Active").GetValue(null));
            Assert.IsFalse((bool)seenProp.GetValue(null), "el epílogo no marca la intro como vista");
            seenProp.SetValue(null, seenBefore);
        }
    }
}
