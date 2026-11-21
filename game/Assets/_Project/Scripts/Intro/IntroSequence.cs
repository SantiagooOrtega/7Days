using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Intro
{
    /// <summary>
    /// Secuencia de láminas en cómic: imagen y frases de VIGÍA (ids del catálogo) por lámina. Puede ser un asset (Create > 7Days > Intro Sequence, en
    /// Resources/IntroSequence) o, si no existe, se arma sola con las 8 láminas Resources/intro_01 ... intro_08 (carpeta Intro/Resources). Las láminas traen su texto
    /// dibujado: no se superpone texto, solo los subtítulos de VIGÍA en las láminas 6, 7 y 8. Si falta una imagen, se usa una viñeta provisional sin fallar.
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Intro Sequence", fileName = "IntroSequence")]
    public class IntroSequence : ScriptableObject
    {
        [System.Serializable]
        public class Panel
        {
            public Texture2D image;
            public string[] narration = new string[0];   // ids del catálogo de VIGÍA, en orden; vacío = sin voz
            public float seconds;                         // 0 = paso automático normal (~7 s)
            public float[] tour = new float[0];           // recorrido por tramos (x, y, ancho, alto de UV por tramo); vacío = zoom y paneo lentos
        }

        public List<Panel> panels = new List<Panel>();

        public static IntroSequence Load()
        {
            var asset = Resources.Load<IntroSequence>("IntroSequence");
            if (asset != null && asset.panels.Count > 0) return asset;
            var s = CreateInstance<IntroSequence>();
            for (int i = 0; i < IntroText.ComicPanels; i++)
            {
                var img = Resources.Load<Texture2D>("intro_" + (i + 1).ToString("00"));
                s.panels.Add(new Panel { image = img != null ? img : Placeholder(i), narration = IntroText.ComicNarration(i) });
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
