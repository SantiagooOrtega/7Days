using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>La intro se monta con las 5 viñetas (provisionales si no hay imágenes), narra con VIGÍA, captura la entrada y al saltarla lo deja todo como estaba.</summary>
    public class IntroTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [UnityTest]
        public IEnumerator Intro_Plays_AndSkipRestoresInput()
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
            Assert.AreEqual(5, panels.Count);
            yield return null; yield return null;
            Assert.IsTrue(player.GetComponent<AudioSource>().isPlaying, "narración de VIGÍA");
            pt.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null);
            yield return null;
            Assert.IsFalse((bool)pt.GetProperty("Active").GetValue(null));
            Assert.IsFalse((bool)input.GetValue(null));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue((bool)seenProp.GetValue(null), "queda guardado que se vio");
            seenProp.SetValue(null, seenBefore);
        }
    }
}
