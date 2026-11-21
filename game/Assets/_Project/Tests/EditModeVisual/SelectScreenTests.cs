using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// La pantalla de selección REAL (uGUI, en EditMode): para cada personaje y cada mapa, ningún texto se sale de su caja ni de su panel y ninguno se
    /// superpone con otro ni con la imagen (modelo 3D o miniatura). Mide el texto renderizado (preferredWidth/Height) y su alineación.
    /// </summary>
    public class SelectScreenTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static readonly Type TextT = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", true);
        private static Type T(string n) => Game.GetType(n, true);

        private static float F(Component c, string p) => Convert.ToSingle(TextT.GetProperty(p).GetValue(c));
        private static object G(Component c, string p) => TextT.GetProperty(p).GetValue(c);

        /// <summary>Caja del texto realmente dibujado, en el espacio del panel (x0, y0, x1, y1).</summary>
        private static Rect Ink(Component text, RectTransform panel, out string problem)
        {
            problem = null;
            var rt = (RectTransform)text.transform; var r = rt.rect;
            bool wrap = G(text, "horizontalOverflow").ToString() == "Wrap", best = (bool)G(text, "resizeTextForBestFit");
            float pw = Mathf.Min(F(text, "preferredWidth"), wrap ? r.width : float.MaxValue), ph = F(text, "preferredHeight");
            if (!best)
            {
                if (!wrap && pw > r.width + 1f) problem = "ancho " + pw.ToString("0") + " > " + r.width.ToString("0");
                if (ph > r.height + 1f) problem = "alto " + ph.ToString("0") + " > " + r.height.ToString("0");
            }
            else { pw = Mathf.Min(pw, r.width); ph = Mathf.Min(ph, r.height); }
            var a = (TextAnchor)G(text, "alignment"); int ax = (int)a % 3, ay = (int)a / 3;
            float x0 = ax == 0 ? r.xMin : ax == 1 ? r.center.x - pw / 2f : r.xMax - pw;
            float y1 = ay == 0 ? r.yMax : ay == 1 ? r.center.y + ph / 2f : r.yMin + ph;
            Vector3 p0 = panel.InverseTransformPoint(rt.TransformPoint(new Vector3(x0, y1 - ph))), p1 = panel.InverseTransformPoint(rt.TransformPoint(new Vector3(x0 + pw, y1)));
            return Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
        }

        private static Rect Box(RectTransform rt, RectTransform panel)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector3 a = panel.InverseTransformPoint(c[0]), b = panel.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static List<string> Check(RectTransform panel, RectTransform image)
        {
            var bad = new List<string>();
            var inner = panel.rect; inner.xMin += 6f; inner.xMax -= 6f; inner.yMin += 6f; inner.yMax -= 6f;
            var inks = new List<(string name, Rect r)>();
            foreach (var t in panel.GetComponentsInChildren(TextT, false))
            {
                string s = (string)G(t, "text"); if (string.IsNullOrEmpty(s)) continue;
                var ink = Ink(t, panel, out string prob);
                string name = s.Length > 24 ? s.Substring(0, 24) : s;
                if (prob != null) bad.Add("'" + name + "' no cabe en su caja: " + prob);
                if (ink.xMin < inner.xMin || ink.xMax > inner.xMax || ink.yMin < inner.yMin || ink.yMax > inner.yMax) bad.Add("'" + name + "' se sale del panel " + panel.name + " " + ink);
                foreach (var o in inks) if (ink.Overlaps(o.r)) bad.Add("'" + name + "' pisa a '" + o.name + "'");
                if (image != null && image.gameObject.activeInHierarchy && ink.Overlaps(Box(image, panel))) bad.Add("'" + name + "' pisa la imagen " + image.name);
                inks.Add((name, ink));
            }
            return bad;
        }

        [Test]
        public void SelectScreen_NoTextOverflowsOrOverlaps_EveryCharacterAndMap()
        {
            var fe = T("SevenDays.UI.FrontEnd"); var scr = T("SevenDays.UI.FrontScreen");
            var inst = (Component)fe.GetMethod("Show").Invoke(null, new[] { Enum.Parse(scr, "Select") });
            try
            {
                var pp = (RectTransform)fe.GetProperty("PreviewPanel").GetValue(inst);
                var lp = (RectTransform)fe.GetProperty("ListPanel").GetValue(inst);
                var variants = (System.Collections.IList)T("SevenDays.Animation.CharacterVariantLibrary").GetProperty("All").GetValue(null);
                var maps = (string[])T("SevenDays.World.MapIO").GetField("PlayableIds").GetValue(null);
                var bad = new List<string>();
                var model = pp.Find("Modelo 3D") as RectTransform; var thumb = pp.Find("Miniatura mapa") as RectTransform;
                Assert.IsNotNull(model); Assert.IsNotNull(thumb);
                for (int v = 0; v < variants.Count; v++)
                    for (int m = 0; m < maps.Length; m++)
                        foreach (int row in new[] { 0, 1 })
                        {
                            fe.GetMethod("Choose").Invoke(inst, new object[] { v, m });
                            fe.GetMethod("ShowPreview").Invoke(inst, new object[] { row });
                            foreach (var b in Check(pp, row == 1 ? thumb : model)) bad.Add(variants[v].GetType().GetField("id").GetValue(variants[v]) + "/" + maps[m] + "/fila " + row + ": " + b);
                            foreach (var b in Check(lp, null)) bad.Add("lista: " + b);
                        }
                // el modelo 3D es el personaje real vestido con su variante
                var prev = fe.GetProperty("Preview3D").GetValue(inst);
                Assert.IsNotNull(prev, "vista previa 3D");
                Assert.IsNotNull(prev.GetType().GetProperty("Rig").GetValue(prev), "modelo real del personaje");
                Assert.IsNotNull(prev.GetType().GetProperty("Texture").GetValue(prev));
                if (bad.Count > 0) Assert.Fail(bad.Count + " problemas:\n" + string.Join("\n", bad.GetRange(0, Math.Min(25, bad.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst.gameObject); }
        }
    }
}
