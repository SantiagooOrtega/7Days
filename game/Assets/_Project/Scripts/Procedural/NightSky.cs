using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Cielo nocturno (puro): textura de la luna con cráteres, estrellas repartidas por la cúpula, parpadeo sutil, estrellas fugaces ocasionales y cuánto
    /// atenúan la niebla, la altura sobre el horizonte y las nubes/humo. La luna va donde está la luz principal (sus sombras cuadran).
    /// </summary>
    public static class NightSky
    {
        public const int MoonSize = 128;

        /// <summary>RGBA de la luna: disco gris claro con mares oscuros, cráteres con borde claro y borde del disco suave (fuera, transparente).</summary>
        public static byte[] MoonTexture(int seed, int n = MoonSize)
        {
            var px = new byte[n * n * 4];
            var rng = new Random(seed);
            var craters = new List<float[]>();
            for (int i = 0; i < 26; i++) craters.Add(new[] { (float)(rng.NextDouble() * 1.6 - 0.8), (float)(rng.NextDouble() * 1.6 - 0.8), (float)(0.03 + Math.Pow(rng.NextDouble(), 2.2) * 0.17) });
            var seas = new List<float[]>();
            for (int i = 0; i < 4; i++) seas.Add(new[] { (float)(rng.NextDouble() - 0.5), (float)(rng.NextDouble() - 0.5), (float)(0.2 + rng.NextDouble() * 0.2) });
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, r = (float)Math.Sqrt(u * u + v * v);
                    float a = Clamp01((1f - r) * n * 0.5f);
                    float c = 0.82f;
                    foreach (var s in seas) { float d = Dist(u, v, s); if (d < s[2]) c -= 0.16f * (1f - d / s[2]); }
                    foreach (var k in craters)
                    {
                        float d = Dist(u, v, k) / k[2];
                        if (d < 0.8f) c -= 0.12f * (1f - d / 0.8f);
                        else if (d < 1.05f) c += 0.08f * (1f - Math.Abs(d - 0.92f) / 0.13f);
                    }
                    c *= 0.85f + 0.15f * (float)Math.Sqrt(Math.Max(0f, 1f - r * r));   // un poco más oscura en el borde
                    int o = (y * n + x) * 4;
                    px[o] = B(c * 0.95f); px[o + 1] = B(c * 0.97f); px[o + 2] = B(c); px[o + 3] = B(a);
                }
            return px;
        }

        /// <summary>Direcciones de estrellas (x, y, z unitarios) en la semiesfera superior, más densas lejos del horizonte, y un brillo 0..1 cada una.</summary>
        public static List<float[]> Stars(int count, int seed)
        {
            var rng = new Random(seed); var list = new List<float[]>(count);
            while (list.Count < count)
            {
                double z = rng.NextDouble(), a = rng.NextDouble() * Math.PI * 2;
                double y = 0.08 + 0.92 * Math.Sqrt(z);   // pocas pegadas al horizonte (la niebla se las come)
                double rr = Math.Sqrt(Math.Max(0, 1 - y * y));
                list.Add(new[] { (float)(Math.Cos(a) * rr), (float)y, (float)(Math.Sin(a) * rr), (float)Math.Pow(rng.NextDouble(), 2.5) });
            }
            return list;
        }

        /// <summary>Parpadeo sutil: 0.8..1 con una fase por estrella.</summary>
        public static float Twinkle(double time, float phase) => 0.9f + 0.1f * (float)Math.Sin(time * (2.1 + phase * 3.0) + phase * 40.0);

        /// <summary>¿Empieza una fugaz en este segundo? Una cada ~25 s de media, determinista por segundo.</summary>
        public static bool ShootingStarAt(int second, int seed) => new Random(second * 7919 + seed).NextDouble() < 0.04;

        /// <summary>Cuánto se ve el cielo (0..1): la niebla densa y la cercanía al horizonte lo apagan; las nubes y el humo, otro poco.</summary>
        public static float Visibility(float fogDensity, float elevationDeg, float clouds)
        {
            float fog = (float)Math.Exp(-fogDensity * 22f);          // 0.0135 -> 0.74, 0.047 -> 0.35
            float horizon = Clamp01((elevationDeg - 3f) / 22f);
            return Clamp01(fog * (0.25f + 0.75f * horizon) * (1f - 0.7f * Clamp01(clouds)));
        }

        /// <summary>Paso de nubes delante de la luna (0 despejado .. 1 tapada), lento y suave.</summary>
        public static float CloudCover(double time, float clouds) => Clamp01(clouds * (0.55f + 0.45f * (float)Math.Sin(time * 0.07) * (float)Math.Cos(time * 0.031 + 1.3)));

        private static float Dist(float u, float v, float[] c) => (float)Math.Sqrt((u - c[0]) * (u - c[0]) + (v - c[1]) * (v - c[1]));
        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        private static byte B(float v) => (byte)(Clamp01(v) * 255f + 0.5f);
    }
}
