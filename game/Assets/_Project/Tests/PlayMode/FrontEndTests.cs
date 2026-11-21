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
    }
}
