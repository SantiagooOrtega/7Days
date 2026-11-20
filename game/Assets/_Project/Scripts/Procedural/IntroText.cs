using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Texto de la intro (puro): una línea por viñeta en intro.txt; efecto máquina de escribir.</summary>
    public static class IntroText
    {
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
