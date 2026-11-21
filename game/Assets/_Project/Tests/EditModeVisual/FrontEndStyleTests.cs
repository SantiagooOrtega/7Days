using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// Coherencia de todas las pantallas (inicio, menú, login, selección, opciones, ranking, perfil, créditos, muerte, victoria y resumen del día), construidas
    /// de verdad: ningún texto por debajo de 24 px a 1080p (ni al encogerse), contraste de al menos 4.5:1 contra lo que tiene detrás (paneles y filas
    /// compuestos sobre el cielo o, en la franja de las llamas, sobre la llama más brillante), y Canvas que escala con la pantalla (1280x720, 1920x1080, 2560x1440).
    /// </summary>
    public class FrontEndStyleTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static readonly Type TextT = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", true);
        private static readonly Type GraphicT = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI", true);
        private static readonly Type ImageT = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
        private static readonly Type ScalerT = Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI", true);
        private static Type T(string n) => Game.GetType(n, true);
        private static object P(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
        public const float MinContrast = 4.5f, FlameTop = -540f + 470f;
        private static readonly Color WorstFlame = new Color(1f, 0.85f, 0.45f);

        public static readonly string[] Screens = { "Title", "Menu", "Login", "Select", "Options", "Ranking", "Profile", "Credits" };

        private static Component Open(string screen)
        {
            if (screen.StartsWith("Stats"))
            {
                var mode = (StatsScreenMode)Enum.Parse(typeof(StatsScreenMode), screen.Substring(5));
                var days = new List<DayStats> { new DayStats { day = 1, kills = 30, headshotKills = 9, damageTaken = 40f, waveSeconds = 120f, zombiesTotal = 30 },
                                                 new DayStats { day = 2, kills = 41, headshotKills = 15, damageTaken = 25f, waveSeconds = 150f, zombiesTotal = 40 } };
                return (Component)T("SevenDays.UI.StatsScreen").GetMethod("Show").Invoke(null, new object[] { mode, 2, days, new List<string> { "más zombis", "más rápidos" } });
            }
            return (Component)T("SevenDays.UI.FrontEnd").GetMethod("Show").Invoke(null, new[] { Enum.Parse(T("SevenDays.UI.FrontScreen"), screen) });
        }

        private static IEnumerable<string> All() { foreach (var s in Screens) yield return s; yield return "StatsDefeat"; yield return "StatsVictory"; yield return "StatsDaySummary"; }

        [TestCaseSource(nameof(All))]
        public void NoTextBelow24px_AndAllReadable_4p5to1(string screen)
        {
            var inst = Open(screen);
            try
            {
                var canvas = (RectTransform)inst.transform.GetChild(0);
                var bad = new List<string>();
                foreach (var t in canvas.GetComponentsInChildren(TextT, true))
                {
                    string s = (string)P(t, "text");
                    int size = (int)P(t, "fontSize");
                    bool fit = (bool)P(t, "resizeTextForBestFit");
                    if (size < 24) bad.Add("'" + Short(s) + "' a " + size + " px");
                    if (fit && (int)P(t, "resizeTextMinSize") < 24) bad.Add("'" + Short(s) + "' puede encogerse a " + P(t, "resizeTextMinSize") + " px");
                }
                // contraste de los textos visibles
                var graphics = new List<Component>(canvas.GetComponentsInChildren(GraphicT, false));
                for (int k = 0; k < graphics.Count; k++)
                {
                    var t = graphics[k]; if (!TextT.IsInstanceOfType(t)) continue;
                    string s = (string)P(t, "text"); if (string.IsNullOrWhiteSpace(s) || Regex.Replace(s, "<[^>]+>|[\\s\\u2009]", "").Length == 0) continue;
                    if (((Behaviour)t).enabled == false) continue;
                    Vector2 p = canvas.InverseTransformPoint(((RectTransform)t.transform).position);
                    var bg = Behind(graphics, k, canvas, p);
                    var fg = (Color)P(t, "color");
                    var colors = new List<Color> { fg };
                    foreach (Match m in Regex.Matches(s, "<color=(#[0-9A-Fa-f]{6})")) { ColorUtility.TryParseHtmlString(m.Groups[1].Value, out var c); colors.Add(c); }
                    foreach (var c in colors)
                    {
                        float cr = Contrast(c, bg);
                        if (cr < MinContrast) bad.Add("'" + Short(s) + "' contraste " + cr.ToString("0.00") + ":1 (texto " + ColorUtility.ToHtmlStringRGB(c) + " sobre " + ColorUtility.ToHtmlStringRGB(bg) + ")");
                    }
                }
                if (bad.Count > 0) Assert.Fail(screen + ": " + bad.Count + " problemas\n" + string.Join("\n", bad.GetRange(0, Math.Min(25, bad.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }

        [TestCase("StatsDefeat"), TestCase("StatsVictory"), TestCase("StatsDaySummary")]
        public void StatsScreens_NoTextOutsideItsPanel(string screen)
        {
            var inst = Open(screen);
            try
            {
                var canvas = (RectTransform)inst.transform.GetChild(0);
                var bad = new List<string>();
                foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(false)) if (rt.name.StartsWith("Panel") || rt.name == "Tabla") bad.AddRange(SelectScreenTests.Check(rt.name == "Panel" ? (RectTransform)rt.parent : rt, null));
                if (bad.Count > 0) Assert.Fail(screen + ": " + bad.Count + " problemas\n" + string.Join("\n", bad.GetRange(0, Math.Min(25, bad.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(2560, 1440)]
        public void Canvas_ScalesWithScreen_TextsStayInside(int w, int h)
        {
            var inst = Open("Menu");
            try
            {
                var canvas = inst.transform.GetChild(0);
                var sc = canvas.GetComponent(ScalerT);
                Assert.AreEqual("ScaleWithScreenSize", P(sc, "uiScaleMode").ToString());
                var refRes = (Vector2)P(sc, "referenceResolution"); Assert.AreEqual(new Vector2(1920, 1080), refRes);
                float match = (float)P(sc, "matchWidthOrHeight");
                // fórmula de CanvasScaler (modo "Match"): media logarítmica de los cocientes de ancho y alto
                float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(w / refRes.x, 2f), Mathf.Log(h / refRes.y, 2f), match));
                Assert.AreEqual(h / 1080f, scale, 1e-4f, "16:9: todo escala igual, el diseño de 1920x1080 cabe entero");
                foreach (var t in canvas.GetComponentsInChildren(TextT, false))
                {
                    Vector2 p = ((RectTransform)canvas).InverseTransformPoint(((RectTransform)t.transform).position);
                    Assert.That(Mathf.Abs(p.x) <= 960f && Mathf.Abs(p.y) <= 540f, "dentro de la pantalla: " + Short((string)P(t, "text")));
                    Assert.GreaterOrEqual((int)P(t, "fontSize") * scale, 24f * h / 1080f - 1e-3f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }

        [Test]
        public void TrackedText_SeparatesWords_NoGluedWords()
        {
            var s = SelectContent.Tracked("SOBREVIVE SIETE DÍAS");
            StringAssert.Contains("E" + SelectContent.WordGap + "S", s, "hueco claro entre palabras");
            Assert.AreEqual(3, s.Split(new[] { SelectContent.WordGap }, StringSplitOptions.None).Length);
            Assert.AreEqual("SOBREVIVESIETEDÍAS", Regex.Replace(s, "[\\s\\u2009]", ""));
        }

        // ------------------------------------------------------------ composición de fondo
        private static Color Behind(List<Component> gs, int k, RectTransform canvas, Vector2 p)
        {
            Color bg;
            if (p.y < FlameTop) bg = WorstFlame;
            else { MenuArt.Sky((p.y + 540f) / 1080f, out float r, out float g, out float b); float glow = Mathf.Exp(-(p.y + 540f) / 1080f * 5f) * 0.35f; bg = new Color(r + glow, g + glow * 0.32f, b + glow * 0.05f); }
            for (int i = 0; i < k; i++)
            {
                var g = gs[i];
                if (TextT.IsInstanceOfType(g) || !((Behaviour)g).isActiveAndEnabled) continue;
                if (InBackdrop(g.transform)) continue;
                var rt = (RectTransform)g.transform;
                Vector2 lp = rt.InverseTransformPoint(canvas.TransformPoint(p));
                if (!rt.rect.Contains(lp)) continue;
                var c = (Color)P(g, "color"); c.a *= GroupAlpha(g.transform);
                if (ImageT.IsInstanceOfType(g))
                {
                    var sp = (Sprite)P(g, "sprite");
                    if (sp != null)
                    {
                        string n = sp.texture.name;
                        if (n == "Marco metal") c = new Color(0f, 0f, 0f, MenuArt.PanelFill * c.a);
                        else if (n == "Fila activa") c = new Color(0xB3 / 255f, 0x26 / 255f, 0x1A / 255f, c.a);
                        else if (n == "Resaltado") c = new Color(MenuArt.Maroon[0], MenuArt.Maroon[1], MenuArt.Maroon[2], 0.8f * c.a);
                        else continue;
                    }
                }
                else continue;   // RawImage: decoración (arañazos, calavera, fondo)
                bg = Mix(bg, new Color(c.r, c.g, c.b), c.a);
            }
            return bg;
        }

        // el proyecto está en espacio de color lineal: Unity mezcla la UI en lineal (un negro al 60 % oscurece menos de lo que parece en sRGB)
        private static Color Mix(Color bg, Color c, float a) { float M(float x, float y) => Mathf.Pow(Mathf.Lerp(Lin(x), Lin(y), a), 1f / 2.2f); return new Color(M(bg.r, c.r), M(bg.g, c.g), M(bg.b, c.b)); }

        private static float GroupAlpha(Transform t) { float a = 1f; for (; t != null; t = t.parent) { var cg = t.GetComponent<CanvasGroup>(); if (cg != null && cg.alpha > 0f) a *= cg.alpha; } return a; }
        private static bool InBackdrop(Transform t) { for (; t != null; t = t.parent) if (t.name == "Fondo en llamas") return true; return false; }

        private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        private static float Lum(Color c) => 0.2126f * Lin(Mathf.Clamp01(c.r)) + 0.7152f * Lin(Mathf.Clamp01(c.g)) + 0.0722f * Lin(Mathf.Clamp01(c.b));
        public static float Contrast(Color a, Color b) { float x = Lum(a), y = Lum(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }
        private static string Short(string s) { s = Regex.Replace(s ?? "", "<[^>]+>", ""); return s.Length > 28 ? s.Substring(0, 28) : s; }
    }
}
