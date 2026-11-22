using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Fuera del editor y de una Development Build no hay atajos de depuración: con DebugKeys.ForceRelease (como la build final) pulsar N, Z, T, B, M, F3, F5
    /// o F8 no hace nada. Control: en modo editor N sí salta la fase (la pulsación simulada llega de verdad).
    /// </summary>
    public class DebugKeysTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena; private Keyboard _kb; private InputSettings.BackgroundBehavior _bg; private InputSettings.EditorInputBehaviorInPlayMode _ed;

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
            T("SevenDays.Core.DebugKeys").GetField("ForceRelease").SetValue(null, false);
            if (_kb != null) { InputSystem.RemoveDevice(_kb); InputSystem.settings.backgroundBehavior = _bg; InputSystem.settings.editorInputBehaviorInPlayMode = _ed; }
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) UnityEngine.Object.Destroy(hud);
            yield return null; yield return null;
        }

        private IEnumerator Press(Key k)
        {
            InputSystem.QueueStateEvent(_kb, new KeyboardState(k)); yield return null; yield return null;
            InputSystem.QueueStateEvent(_kb, new KeyboardState()); yield return null; yield return null;
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator ReleaseBuild_HasNoDebugShortcuts()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            Component loop = null; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f && (loop = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")) as Component) == null) yield return null;
            Assert.IsNotNull(loop);
            _bg = InputSystem.settings.backgroundBehavior; _ed = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;   // el editor en batch no tiene foco
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _kb = InputSystem.AddDevice<Keyboard>(); _kb.MakeCurrent();
            object Phase() { var c = loop.GetType().GetProperty("Cycle").GetValue(loop); return c.GetType().GetProperty("Phase").GetValue(c); }
            var mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
            int Alive() => (int)mgr.GetType().GetProperty("ActiveCount").GetValue(mgr);
            var health = GameObject.FindWithTag("Player").GetComponent(T("SevenDays.Core.Health"));
            float Hp() => (float)health.GetType().GetProperty("Current").GetValue(health);
            var dk = T("SevenDays.Core.DebugKeys");

            dk.GetField("ForceRelease").SetValue(null, true);   // como la build final
            Assert.IsFalse((bool)dk.GetProperty("Enabled").GetValue(null));
            var phase0 = Phase(); int alive0 = Alive(); float hp0 = Hp();
            foreach (var k in new[] { Key.N, Key.Z, Key.V, Key.T, Key.U, Key.L, Key.M, Key.F3, Key.F5, Key.F6, Key.F8 }) yield return Press(k);
            Assert.AreEqual(phase0, Phase(), "N no salta la fase");
            Assert.AreEqual(alive0, Alive(), "Z/V no crean zombis");
            Assert.AreEqual(hp0, Hp(), 0.01f, "T no hace daño");
            Assert.IsNotNull(GameObject.FindWithTag("Player"), "M no recarga el mapa");
            Assert.IsFalse((bool)T("SevenDays.UI.PlayerHud").GetField("ShowFps").GetValue(null), "F3 no muestra la línea de FPS");
            Assert.IsFalse((bool)T("SevenDays.Core.GameInput").GetProperty("DebugPanelPressed").GetValue(null));
            StringAssert.DoesNotContain("Pruebas:", (string)T("SevenDays.UI.PlayerHud").GetField("HelpText").GetValue(null), "la ayuda normal no lista trucos");

            // control: en el editor el mismo atajo sí funciona (la pulsación simulada llega)
            dk.GetField("ForceRelease").SetValue(null, false);
            yield return Press(Key.N);
            Assert.AreNotEqual(phase0, Phase(), "en el editor N salta la fase");
        }
    }
}
