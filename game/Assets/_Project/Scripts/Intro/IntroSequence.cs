using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Intro
{
    /// <summary>
    /// Secuencia de la intro en cómic: viñetas con imagen, texto y frase de narración de VIGÍA. Puede ser un asset (Create > 7Days > Intro Sequence, guardado en
    /// Resources/IntroSequence) o, si no existe, se arma sola: imágenes Resources/intro_01, intro_02... (carpeta Intro/Resources) y texto de intro.txt (una línea
    /// por viñeta). Si faltan imágenes, se dibujan viñetas provisionales por código.
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Intro Sequence", fileName = "IntroSequence")]
    public class IntroSequence : ScriptableObject
    {
        [System.Serializable]
        public class Panel
        {
            public Texture2D image;
            [TextArea] public string text;
            public string narrationId;   // id del catálogo de VIGÍA (intro_1...); vacío = sin voz
        }

        public List<Panel> panels = new List<Panel>();

        public static IntroSequence Load()
        {
            var asset = Resources.Load<IntroSequence>("IntroSequence");
            if (asset != null && asset.panels.Count > 0) return asset;
            var s = CreateInstance<IntroSequence>();
            var ta = Resources.Load<TextAsset>("intro");
            var lines = IntroText.Parse(ta != null ? ta.text : "");
            for (int i = 0; ; i++)
            {
                var img = Resources.Load<Texture2D>("intro_" + (i + 1).ToString("00"));
                if (img == null && i >= lines.Count) break;
                s.panels.Add(new Panel { image = img != null ? img : Placeholder(i), text = i < lines.Count ? lines[i] : "", narrationId = "intro_" + (i + 1) });
            }
            return s;
        }

        /// <summary>Viñeta provisional: cielo en degradado, siluetas de edificios en ruinas, luna y grano (cambia de tono por viñeta).</summary>
        public static Texture2D Placeholder(int index)
        {
            const int W = 640, H = 360;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, name = "intro_placeholder_" + index };
            var rng = new System.Random(100 + index);
            Color top = Color.HSVToRGB((0.58f + index * 0.09f) % 1f, 0.45f, 0.35f), bottom = Color.HSVToRGB((0.05f + index * 0.07f) % 1f, 0.6f, 0.55f);
            var heights = new int[W];
            int x = 0;
            while (x < W) { int w = rng.Next(25, 70), h = rng.Next(60, 210); for (int i = x; i < Mathf.Min(W, x + w); i++) heights[i] = h - (rng.Next(10) == 0 ? rng.Next(30) : 0); x += w; }
            float mx = 120 + index * 90 % 400, my = 280;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int i = 0; i < W; i++)
                {
                    Color c = Color.Lerp(bottom, top, y / (float)H);
                    float dm = Mathf.Sqrt((i - mx) * (i - mx) + (y - my) * (y - my));
                    if (dm < 22f) c = Color.Lerp(c, new Color(0.95f, 0.92f, 0.8f), 0.9f);
                    if (y < heights[i]) c = new Color(0.04f, 0.04f, 0.05f);
                    c *= 0.92f + 0.08f * (float)rng.NextDouble();
                    px[y * W + i] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }
    }
}
