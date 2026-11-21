using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// Todas las pantallas del menú con el estilo nuevo (inicio, menú, opciones, ranking, perfil, créditos, selección), construidas de verdad en EditMode:
    /// ningún texto se sale de su caja ni de su panel, ninguno pisa a otro, y los que no van en un panel quedan dentro de la pantalla de 1920x1080.
    /// </summary>
    public class FrontEndScreensTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static readonly Type TextT = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", true);
        private static Type T(string n) => Game.GetType(n, true);

        [TestCase("Title"), TestCase("Menu"), TestCase("Options"), TestCase("Ranking"), TestCase("Profile"), TestCase("Credits"), TestCase("Select")]
        public void Screen_NoTextOutsideItsPanel_NoOverlaps(string screen)
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, screen) });
            try
            {
                var bad = new List<string>();
                var root = (RectTransform)inst.transform.GetChild(0);   // el Canvas
                var panels = new List<RectTransform>();
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(false)) if (rt.name.StartsWith("Panel")) panels.Add(rt);
                Assert.Greater(panels.Count, 0, screen + ": paneles");
                foreach (var p in panels) bad.AddRange(SelectScreenTests.Check(p, null));
                // textos fuera de paneles: dentro de 1920x1080 (centrado) y sin pisarse
                var free = new List<(string, Rect)>();
                foreach (var t in root.GetComponentsInChildren(TextT, false))
                {
                    bool inPanel = false;
                    for (var x = t.transform.parent; x != null; x = x.parent) if (x.name.StartsWith("Panel")) { inPanel = true; break; }
                    string s = (string)TextT.GetProperty("text").GetValue(t);
                    if (inPanel || string.IsNullOrEmpty(s)) continue;
                    var ink = SelectScreenTests.Ink(t, root, out string prob);
                    string name = s.Length > 24 ? s.Substring(0, 24) : s;
                    if (prob != null) bad.Add("'" + name + "' no cabe en su caja: " + prob);
                    if (ink.xMin < -960f || ink.xMax > 960f || ink.yMin < -540f || ink.yMax > 540f) bad.Add("'" + name + "' se sale de la pantalla " + ink);
                    foreach (var (n, r) in free) if (ink.Overlaps(r)) bad.Add("'" + name + "' pisa a '" + n + "'");
                    free.Add((name, ink));
                }
                if (bad.Count > 0) Assert.Fail(screen + ": " + bad.Count + " problemas\n" + string.Join("\n", bad.GetRange(0, Math.Min(20, bad.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }

        [Test]
        public void TitleScreen_HasEmblem_7DAYS_AndPressEnter()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Title") });
            try
            {
                var texts = new List<string>();
                foreach (var t in inst.GetComponentsInChildren(TextT, true)) texts.Add(System.Text.RegularExpressions.Regex.Replace((string)TextT.GetProperty("text").GetValue(t), "\\s+", ""));
                CollectionAssert.Contains(texts, "7DAYS", "título del juego");
                CollectionAssert.Contains(texts, "PULSAENTER");
                Assert.IsNotNull(inst.transform.Find("Canvas menú/Pantalla Title/Emblema"), "emblema propio");
                Assert.IsNotNull(fe.GetProperty("PressStartScreen").GetValue(inst));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }
    }
}
