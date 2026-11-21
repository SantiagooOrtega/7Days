using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Intro en cómic (puro, sin Unity): las 8 láminas traen su texto dibujado, así que solo VIGÍA narra (con subtítulos) en las láminas 6, 7 y 8.
    /// Tiempos de paso automático, fundidos, movimiento lento (zoom y paneo) y encaje con letterbox para cualquier relación de aspecto.
    /// </summary>
    public static class IntroText
    {
        public const int ComicPanels = 8;
        public const float AutoSeconds = 7f, FadeSeconds = 0.8f, LeaveSeconds = 0.45f;

        /// <summary>Frases de VIGÍA (ids del catálogo) de cada lámina, en orden; las demás láminas van sin voz.</summary>
        public static string[] ComicNarration(int panel) => panel switch
        {
            5 => new[] { "comic_6a", "comic_6b", "comic_6c" },
            6 => new[] { "comic_7" },
            7 => new[] { "comic_8" },
            _ => new string[0],
        };

        /// <summary>Segundos en pantalla: ~7 s, o lo que dure la narración más un respiro si es más larga.</summary>
        public static float HoldSeconds(float narrationSeconds, float auto = AutoSeconds) => Math.Max(auto, narrationSeconds + 1.2f);

        /// <summary>Opacidad de la lámina: entra con fundido y, al salir (leaving = segundos desde que empezó a salir; negativo = no sale), se funde a negro.</summary>
        public static float Alpha(float elapsed, float leaving) =>
            Clamp01(elapsed / FadeSeconds) * (leaving < 0f ? 1f : Clamp01(1f - leaving / LeaveSeconds));

        /// <summary>Movimiento lento de cámara sobre la lámina (t de 0 a 1 en su tiempo): zoom del 100 al 108 % y paneo suave cuya dirección cambia por lámina.
        /// Devuelve el rectángulo de UV visible (x, y, ancho, alto), siempre dentro de la imagen.</summary>
        public static float[] KenBurns(int panel, float t)
        {
            t = Clamp01(t);
            float e = t * t * (3f - 2f * t);
            float z = 1f / (1f + 0.08f * e);
            float free = 1f - z;
            float dir = panel % 2 == 0 ? e : 1f - e;
            float ox = free * (0.5f + 0.5f * (dir * 2f - 1f) * 0.8f), oy = free * (0.5f + 0.15f * (float)Math.Sin(panel * 1.3f));
            return new[] { Clamp(ox, 0f, free), Clamp(oy, 0f, free), z, z };
        }

        /// <summary>Recorrido por tramos (epílogo): de un rectángulo de UV al siguiente con transición suave; rects = {x, y, ancho, alto} por tramo.</summary>
        public static float[] Tour(float[][] rects, float t)
        {
            t = Clamp01(t);
            int n = rects.Length;
            if (n == 1) return rects[0];
            float f = t * (n - 1); int i = Math.Min(n - 2, (int)f); float k = f - i;
            // se queda un rato en cada tramo y se desplaza en el tercio central
            k = Clamp01((k - 0.3f) / 0.4f); k = k * k * (3f - 2f * k);
            var a = rects[i]; var b = rects[i + 1];
            return new[] { a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k, a[3] + (b[3] - a[3]) * k };
        }

        /// <summary>Encaje con letterbox: la imagen entera, centrada y sin deformar, en una pantalla de cualquier relación de aspecto (x, y, ancho, alto).</summary>
        public static float[] Fit(float imgW, float imgH, float scrW, float scrH)
        {
            float ia = imgW / imgH, sa = scrW / scrH;
            return ia > sa ? new[] { 0f, (scrH - scrW / ia) / 2f, scrW, scrW / ia } : new[] { (scrW - scrH * ia) / 2f, 0f, scrH * ia, scrH };
        }

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        private static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;

        public const float CharsPerSecond = 32f;

        public static List<string> Parse(string text)
        {
            var l = new List<string>();
            if (text == null) return l;
            foreach (var raw in text.Split('\n')) { var t = raw.Trim(); if (t.Length > 0 && !t.StartsWith("#")) l.Add(t); }
            return l;
        }

        /// <summary>Cuántos caracteres se ven tras "seconds" escribiendo.</summary>
        public static int Visible(string line, float seconds) => Math.Max(0, Math.Min(line.Length, (int)(seconds * CharsPerSecond)));

        /// <summary>Tiempo mínimo en pantalla de una viñeta: lo que tarda en escribirse más 2.5 s de lectura (o la narración si dura más).</summary>
        public static float PanelSeconds(string line, float narration) => Math.Max(line.Length / CharsPerSecond + 2.5f, narration + 0.8f);
    }
}
