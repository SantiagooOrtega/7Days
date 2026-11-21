using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>Menú y pantallas: el menú principal tiene sus 7 opciones, se navega, cada pantalla se construye, Selección -> Empezar suelta la arena, y la arena espera al menú.</summary>
    public class FrontEndTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [UnityTest]
        public IEnumerator Menu_Navigates_AllScreensBuild_StartReleasesTheArena()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var inst = fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Menu") });
            yield return null;
            Assert.IsTrue((bool)fe.GetProperty("Blocking").GetValue(null));
            var list = fe.GetProperty("List").GetValue(inst);
            Assert.AreEqual(7, (int)list.GetType().GetProperty("Count").GetValue(list), "Jugar, Continuar, Ranking, Perfil, Opciones, Créditos, Salir");
            list.GetType().GetMethod("Select").Invoke(list, new object[] { 2, false });
            Assert.AreEqual(2, (int)list.GetType().GetProperty("Index").GetValue(list));
            var scaler = ((Component)inst).GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            foreach (var s in new[] { "Login", "Ranking", "Profile", "Options", "Credits", "Select" })
            {
                fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, s) });
                yield return null;
                Assert.AreEqual(s, fe.GetProperty("Current").GetValue(inst).ToString());
            }
            // Selección: "Empezar" (fila 2) arranca la partida
            list = fe.GetProperty("List").GetValue(inst);
            list.GetType().GetMethod("Select").Invoke(list, new object[] { 2, false });
            list.GetType().GetMethod("Confirm").Invoke(list, null);
            yield return null;
            Assert.IsFalse((bool)fe.GetProperty("Blocking").GetValue(null), "Empezar suelta la arena");
            var ip = T("SevenDays.Intro.IntroPlayer").GetField("NewGameRequested");
            bool requested = (bool)ip.GetValue(null);
            ip.SetValue(null, false);   // aquí no hay arena que la consuma
            Assert.IsTrue(requested, "Empezar pide la intro de la partida nueva");
            UnityEngine.Object.Destroy((Component)inst);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Arena_WaitsForTheMenu()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Menu") });
            var arena = new GameObject("Arena"); arena.AddComponent(T("SevenDays.World.TestArena"));
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsNull(GameObject.FindWithTag("Player"), "sin jugador mientras el menú está abierto");
            fe.GetMethod("StartGame").Invoke(inst, null);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            Assert.IsNotNull(GameObject.FindWithTag("Player"), "tras Jugar se construye la partida");
            UnityEngine.Object.Destroy(arena); UnityEngine.Object.Destroy(inst.gameObject);
            foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Empezar_ShowsTheEightPanelsInOrder_ThenDayOne_RetryDoesNot()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen"); var ip = T("SevenDays.Intro.IntroPlayer");
            PlayerPrefs.SetInt("intro.seen", 1);   // la marca vieja ya no la oculta
            bool skipBefore = (bool)ip.GetProperty("SkipAlways").GetValue(null);
            ip.GetProperty("SkipAlways").SetValue(null, false);
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Select") });
            var arena = new GameObject("Arena"); arena.AddComponent(T("SevenDays.World.TestArena"));
            try
            {
                yield return null;
                var list = fe.GetProperty("List").GetValue(inst);
                list.GetType().GetMethod("Select").Invoke(list, new object[] { 2, false });
                list.GetType().GetMethod("Confirm").Invoke(list, null);   // EMPEZAR
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 10f && !(bool)ip.GetProperty("Active").GetValue(null)) yield return null;
                Assert.IsTrue((bool)ip.GetProperty("Active").GetValue(null), "tras EMPEZAR sale la intro");
                var player = (Component)UnityEngine.Object.FindFirstObjectByType(ip);
                var seq = ip.GetField("_seq", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(player);
                var panels = (System.Collections.IList)seq.GetType().GetField("panels").GetValue(seq);
                Assert.AreEqual(8, panels.Count);
                for (int i = 0; i < 8; i++)
                {
                    Assert.AreEqual(i, (int)ip.GetProperty("Index").GetValue(player), "lámina " + (i + 1) + " en orden");
                    var img = (Texture2D)panels[i].GetType().GetField("image").GetValue(panels[i]);
                    Assert.AreEqual("intro_" + (i + 1).ToString("00"), img.name, "carga de Resources");
                    Assert.IsNull(UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")), "el Día 1 espera a la intro");
                    ip.GetMethod("Next").Invoke(player, null);   // clic o Espacio
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                Assert.IsFalse((bool)ip.GetProperty("Active").GetValue(null), "termina tras la lámina 8");
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 60f && UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")) == null) yield return null;
                var loop = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop"));
                Assert.IsNotNull(loop, "arranca el Día 1");
                var cycle = loop.GetType().GetProperty("Cycle").GetValue(loop);
                Assert.AreEqual(1, (int)cycle.GetType().GetProperty("Day").GetValue(cycle));
                Assert.IsFalse((bool)ip.GetField("NewGameRequested").GetValue(null), "reintentar (recarga de escena) no la repite");
            }
            finally
            {
                ip.GetField("NewGameRequested").SetValue(null, false);
                ip.GetProperty("SkipAlways").SetValue(null, skipBefore);
                UnityEngine.Object.Destroy(arena); UnityEngine.Object.Destroy(inst.gameObject);
                foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
            }
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator TitleScreen_PressEnter_GoesToMenuOrLogin()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var testMode = fe.GetField("TestMode"); bool prev = (bool)testMode.GetValue(null);
            testMode.SetValue(null, true);
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Title") });
            yield return null;
            Assert.AreEqual("Title", fe.GetProperty("Current").GetValue(inst).ToString(), "pantalla de inicio");
            var ps = fe.GetProperty("PressStartScreen").GetValue(inst);
            ps.GetType().GetMethod("Press").Invoke(ps, null);   // como pulsar Enter
            yield return null;
            Assert.AreEqual("Menu", fe.GetProperty("Current").GetValue(inst).ToString(), "con sesión o en modo prueba: menú principal");
            var list = fe.GetProperty("List").GetValue(inst);
            Assert.AreEqual(7, (int)list.GetType().GetProperty("Count").GetValue(list));
            testMode.SetValue(null, false);
            fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Title") });
            yield return null;
            ps = fe.GetProperty("PressStartScreen").GetValue(inst);
            ps.GetType().GetMethod("Press").Invoke(ps, null);
            yield return null;
            Assert.AreEqual("Login", fe.GetProperty("Current").GetValue(inst).ToString(), "sin sesión: login");
            testMode.SetValue(null, prev);
            UnityEngine.Object.Destroy(inst.gameObject);
            yield return null;
        }
    }
}
