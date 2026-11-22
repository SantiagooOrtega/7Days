using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SevenDays.Tests.Visual
{
    /// <summary>Menú de pausa (las cinco pantallas): ningún texto se sale de su panel ni pisa a otro, y ninguno baja de 24 px.</summary>
    public class PauseMenuLayoutTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static readonly Type TextT = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", true);

        [TestCase("Main"), TestCase("Options"), TestCase("Controls"), TestCase("ConfirmMenu"), TestCase("ConfirmQuit")]
        public void PauseScreen_TextsInsidePanels_AtLeast24px(string screen)
        {
            var pm = Game.GetType("SevenDays.UI.PauseMenu", true);
            var inst = (Component)pm.GetMethod("Create").Invoke(null, new object[] { null, null, null });
            try
            {
                pm.GetMethod("Show").Invoke(inst, new[] { Enum.Parse(Game.GetType("SevenDays.UI.PauseScreen", true), screen) });
                var bad = new List<string>();
                int panels = 0;
                foreach (var rt in inst.GetComponentsInChildren<RectTransform>(false)) if (rt.name.StartsWith("Panel")) { panels++; bad.AddRange(SelectScreenTests.Check(rt, null)); }
                Assert.Greater(panels, 0);
                foreach (var t in inst.GetComponentsInChildren(TextT, true))
                {
                    int size = (int)TextT.GetProperty("fontSize").GetValue(t);
                    if (size < 24) bad.Add("'" + TextT.GetProperty("text").GetValue(t) + "' a " + size + " px");
                    if ((bool)TextT.GetProperty("resizeTextForBestFit").GetValue(t) && (int)TextT.GetProperty("resizeTextMinSize").GetValue(t) < 24) bad.Add("se encoge por debajo de 24 px");
                }
                if (bad.Count > 0) Assert.Fail(screen + ": " + bad.Count + " problemas\n" + string.Join("\n", bad.GetRange(0, Math.Min(20, bad.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }
    }
}
