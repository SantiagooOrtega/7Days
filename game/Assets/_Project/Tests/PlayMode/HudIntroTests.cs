using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Durante la intro y el epílogo no hay HUD encima de las láminas: la puerta del HUD está cerrada (todos los OnGUI del juego salen) y el HUD de armas (uGUI)
    /// está oculto; al saltar vuelve todo. La línea de FPS está oculta por defecto.
    /// </summary>
    public class HudIntroTests
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
            foreach (var x in UnityEngine.Object.FindObjectsByType(T("SevenDays.Intro.IntroPlayer"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)x).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            yield return null; yield return null;
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator IntroAndEpilogue_HideTheHud_ItComesBackAfter([Values("intro", "epilogo")] string which)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 60; i++) yield return null;
            var gate = T("SevenDays.Core.HudGate").GetProperty("Hidden");
            var weaponHud = (Component)UnityEngine.Object.FindFirstObjectByType(T("SevenDays.UI.WeaponHud"));
            Canvas Canvas() { var v = weaponHud.GetType().GetProperty("View").GetValue(weaponHud); return (Canvas)v.GetType().GetProperty("Canvas").GetValue(v); }
            Assert.IsFalse((bool)gate.GetValue(null)); Assert.IsTrue(Canvas().enabled, "HUD de armas en partida");
            Assert.IsFalse((bool)T("SevenDays.UI.PlayerHud").GetField("ShowFps").GetValue(null), "línea de FPS oculta por defecto (F3)");

            var ip = T("SevenDays.Intro.IntroPlayer");
            if (which == "intro") ip.GetMethod("Play").Invoke(null, new object[] { null });
            else ip.GetMethod("PlayEpilogue").Invoke(null, new object[] { null, null });
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue((bool)ip.GetProperty("Active").GetValue(null), which + " en pantalla");
            Assert.IsTrue((bool)gate.GetValue(null), which + ": la puerta del HUD está cerrada (ningún OnGUI del juego se dibuja)");
            Assert.IsFalse(Canvas().enabled, which + ": sin HUD de armas encima");

            ip.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(UnityEngine.Object.FindFirstObjectByType(ip), null);   // SALTAR
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse((bool)gate.GetValue(null), "al saltar vuelve el HUD");
            Assert.IsTrue(Canvas().enabled, "vuelve el HUD de armas");
        }
    }
}
