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
    /// Menú de pausa en partida: Esc lo abre (tiempo detenido, audio en pausa, ratón libre, HUD oculto) y lo cierra (todo restablecido); Salir y Volver al menú
    /// piden confirmación y ejecutan su acción; durante la intro, Esc la salta pero no abre la pausa.
    /// </summary>
    public class PauseMenuTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena; private Keyboard _kb; private InputSettings.BackgroundBehavior _bg; private InputSettings.EditorInputBehaviorInPlayMode _ed;

        [UnitySetUp]
        public IEnumerator CleanStart()
        {
            foreach (var a in UnityEngine.Object.FindObjectsByType(T("SevenDays.World.TestArena"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)a).gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
            foreach (var n in new[] { "SevenDays.World.DayLoop", "SevenDays.UI.WeaponHud", "SevenDays.UI.PauseMenu" })
                foreach (var x in UnityEngine.Object.FindObjectsByType(T(n), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)x).gameObject);
            var oldHud = GameObject.Find("HUD"); if (oldHud != null) UnityEngine.Object.Destroy(oldHud);
            yield return null;
            // estado global que pueden dejar otras pruebas (entrada capturada, Esc bloqueado, pantalla final, intro)
            T("SevenDays.Core.GameInput").GetField("InputCaptured").SetValue(null, false);
            T("SevenDays.Core.GameInput").GetField("EscapeBlocked").SetValue(null, false);
            T("SevenDays.UI.StatsScreen").GetMethod("Hide").Invoke(null, null);
            foreach (var x in UnityEngine.Object.FindObjectsByType(T("SevenDays.Intro.IntroPlayer"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)x).gameObject);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            var pm = T("SevenDays.UI.PauseMenu");
            pm.GetField("QuitAction").SetValue(null, (Action)Application.Quit);
            pm.GetField("ToMenuAction").SetValue(null, Delegate.CreateDelegate(typeof(Action), T("SevenDays.World.DayLoop").GetMethod("ToMenu")));
            T("SevenDays.Core.GameInput").GetField("InputCaptured").SetValue(null, false);
            foreach (var x in UnityEngine.Object.FindObjectsByType(T("SevenDays.Intro.IntroPlayer"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)x).gameObject);
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
        public IEnumerator Esc_OpensAndCloses_PausesTime_QuitAndMenuConfirm_NotDuringIntro()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f && UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")) == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            _bg = InputSystem.settings.backgroundBehavior; _ed = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _kb = InputSystem.AddDevice<Keyboard>(); _kb.MakeCurrent();
            var pm = T("SevenDays.UI.PauseMenu"); var isOpen = pm.GetProperty("IsOpen");
            var inst = pm.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(inst, "la partida crea el menú de pausa");

            T("SevenDays.Core.GameInput").GetField("InputCaptured").SetValue(null, false);
            yield return Press(Key.Escape);
            Assert.IsTrue((bool)isOpen.GetValue(null), "Esc abre la pausa (capturada=" + T("SevenDays.Core.GameInput").GetField("InputCaptured").GetValue(null) + ", hud oculto=" + T("SevenDays.Core.HudGate").GetProperty("Hidden").GetValue(null)
                + ", stats=" + T("SevenDays.UI.StatsScreen").GetProperty("Blocking").GetValue(null) + ", menú=" + T("SevenDays.UI.FrontEnd").GetProperty("Blocking").GetValue(null) + ")");
            Assert.AreEqual(0f, Time.timeScale, "tiempo detenido");
            Assert.IsTrue(AudioListener.pause, "audio del juego en pausa");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "ratón libre");
            Assert.IsTrue((bool)T("SevenDays.Core.HudGate").GetProperty("Hidden").GetValue(null), "sin HUD encima");
            yield return Press(Key.Escape);
            Assert.IsFalse((bool)isOpen.GetValue(null), "Esc cierra y reanuda");
            Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse((bool)T("SevenDays.Core.GameInput").GetField("InputCaptured").GetValue(null), "vuelve la entrada del juego");

            // Salir y Volver al menú: confirmación y acción
            bool quit = false, menu = false;
            pm.GetField("QuitAction").SetValue(null, (Action)(() => quit = true));
            pm.GetField("ToMenuAction").SetValue(null, (Action)(() => menu = true));
            object List() => pm.GetProperty("List").GetValue(inst);
            void Choose(int i) { var l = List(); l.GetType().GetMethod("Select").Invoke(l, new object[] { i, false }); l.GetType().GetMethod("Confirm").Invoke(l, null); }
            string Screen() => pm.GetProperty("Current").GetValue(inst).ToString();
            yield return Press(Key.Escape);
            Choose(4); Assert.AreEqual("ConfirmQuit", Screen(), "Salir pide confirmación");
            Choose(0); Assert.AreEqual("Main", Screen(), "No vuelve atrás"); Assert.IsFalse(quit);
            Choose(4); Choose(1); Assert.IsTrue(quit, "Sí sale del juego (Application.Quit)");
            Assert.IsFalse((bool)isOpen.GetValue(null)); Assert.AreEqual(1f, Time.timeScale);
            yield return Press(Key.Escape);
            Choose(3); Assert.AreEqual("ConfirmMenu", Screen()); Choose(1); Assert.IsTrue(menu, "Sí vuelve al menú principal");
            yield return Press(Key.Escape);
            Choose(1); Assert.AreEqual("Options", Screen(), "Opciones");
            yield return Press(Key.Escape); Assert.AreEqual("Main", Screen(), "Esc vuelve de Opciones");
            yield return Press(Key.Escape); Assert.IsFalse((bool)isOpen.GetValue(null));

            // durante la intro Esc la salta, pero no abre la pausa
            var ip = T("SevenDays.Intro.IntroPlayer"); ip.GetMethod("Play").Invoke(null, new object[] { null });
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue((bool)ip.GetProperty("Active").GetValue(null));
            yield return Press(Key.Escape);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse((bool)ip.GetProperty("Active").GetValue(null), "Esc salta la intro");
            Assert.IsFalse((bool)isOpen.GetValue(null), "y no abre la pausa");
        }
    }
}
