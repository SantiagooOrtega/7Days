using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum DecalKind { Blood, Crack, Puddle, DashLine, Zebra, Arrow, Stain, Burn }

    /// <summary>
    /// Texturas RGBA con transparencia para calcomanías sobre el suelo: sangre seca, grietas y charcos oscuros.
    /// El borde siempre es totalmente transparente. Determinista (misma semilla = misma imagen), C# puro.
    /// </summary>
    public static class DecalGenerator
    {
        public static byte[] Generate(DecalKind kind, int size, int seed)
        {
            var px = new byte[size * size * 4];
            List<(float ax, float ay, float bx, float by, float w)> segs = kind == DecalKind.Crack ? CrackSegments(seed) : null;
            float streakAngle = (float)(new Random(seed).NextDouble() * Math.PI * 2);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float r, g, b, a;
                    switch (kind)
                    {
                        case DecalKind.Blood: Blood(u, v, seed, streakAngle, out r, out g, out b, out a); break;
                        case DecalKind.Crack: Crack(u, v, seed, segs, out r, out g, out b, out a); break;
                        case DecalKind.Puddle: Puddle(u, v, seed, out r, out g, out b, out a); break;
                        case DecalKind.Stain: Stain(u, v, seed, out r, out g, out b, out a); break;
                        case DecalKind.Burn: Burn(u, v, seed, out r, out g, out b, out a); break;
                        default: RoadPaint(kind, u, v, seed, out r, out g, out b, out a); break;
                    }
                    int i = (y * size + x) * 4;
                    px[i] = ToByte(r); px[i + 1] = ToByte(g); px[i + 2] = ToByte(b); px[i + 3] = ToByte(a);
                }
            }
            return px;
        }

        private static byte ToByte(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
        private static float Smooth(float a, float b, float x) => TileableNoise.Smooth(a, b, x);

        private static void Blood(float u, float v, int seed, float angle, out float r, out float g, out float b, out float a)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float warp = (TileableNoise.Fbm(u, v, 5, 4, seed + 1) - 0.5f) * 0.35f;
            float body = 1f - Smooth(0.15f, 0.25f, dist + warp);

            // Salpicaduras: gotas pequeñas en algunas celdas, más lejos del centro
            TileableNoise.Voronoi(u, v, 9, seed + 2, out float f1, out _, out float id);
            float drops = id > 0.6f ? 1f - Smooth(0.05f, 0.15f, f1) : 0f;
            drops *= Smooth(0.14f, 0.2f, dist);

            // Arrastre: franja alargada en una dirección
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle);
            float along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
            float streak = (1f - Smooth(0.012f, 0.045f, Math.Abs(across) + 0.02f * (1f - along))) * Smooth(0.0f, 0.1f, along) * (1f - Smooth(0.3f, 0.45f, along));

            float coverage = Math.Max(body, Math.Max(drops * 0.9f, streak * 0.8f));
            a = coverage * (1f - Smooth(0.42f, 0.5f, dist)) * (0.82f + 0.18f * TileableNoise.Fbm(u, v, 14, 2, seed + 3));
            float tone = 0.7f + 0.5f * TileableNoise.Fbm(u, v, 10, 3, seed + 4);
            float dark = Math.Min(1f, body * 0.6f + streak * 0.3f);
            r = (0.24f * (1f - dark) + 0.1f * dark) * tone;   // sangre seca: marrón rojizo, más oscuro en el centro
            g = (0.035f * (1f - dark) + 0.015f * dark) * tone;
            b = (0.03f * (1f - dark) + 0.012f * dark) * tone;
        }

        private static List<(float ax, float ay, float bx, float by, float w)> CrackSegments(int seed)
        {
            var rng = new Random(seed);
            var list = new List<(float, float, float, float, float)>();
            float y = 0.5f + (float)(rng.NextDouble() - 0.5) * 0.2f;
            float x = 0.04f;
            var points = new List<(float x, float y)>();
            while (x < 0.96f)
            {
                points.Add((x, y));
                float nx = x + 0.05f + (float)rng.NextDouble() * 0.04f;
                float ny = y + ((float)rng.NextDouble() - 0.5f) * 0.1f;
                list.Add((x, y, nx, ny, 0.014f));
                x = nx; y = ny;
            }
            // Ramas que salen de puntos de la grieta principal
            for (int k = 0; k < 3; k++)
            {
                var p = points[1 + rng.Next(points.Count - 2)];
                float dir = rng.NextDouble() < 0.5 ? 1f : -1f;
                float bx = p.x, by = p.y;
                int steps = 3 + rng.Next(3);
                for (int s = 0; s < steps; s++)
                {
                    float nx = bx + ((float)rng.NextDouble() - 0.3f) * 0.07f;
                    float ny = by + dir * (0.045f + (float)rng.NextDouble() * 0.03f);
                    list.Add((bx, by, nx, ny, 0.014f * (1f - s / (float)steps * 0.7f)));
                    bx = nx; by = ny;
                    if (bx < 0.03f || bx > 0.97f || by < 0.03f || by > 0.97f) break;
                }
            }
            return list;
        }

        private static void Crack(float u, float v, int seed, List<(float ax, float ay, float bx, float by, float w)> segs,
            out float r, out float g, out float b, out float a)
        {
            float best = float.MaxValue, width = 0.014f;
            foreach (var s in segs)
            {
                float d = DistToSegment(u, v, s.ax, s.ay, s.bx, s.by);
                if (d < best) { best = d; width = s.w; }
            }
            float core = 1f - Smooth(width * 0.35f, width, best);
            float broken = 0.65f + 0.35f * TileableNoise.Fbm(u, v, 12, 3, seed + 5); // no es una línea continua perfecta
            a = core * broken * (1f - Smooth(0.44f, 0.5f, Math.Max(Math.Abs(u - 0.5f), Math.Abs(v - 0.5f))));
            float rim = Smooth(width * 0.3f, width, best);
            float c = 0.02f + 0.07f * rim;
            r = c; g = c * 0.97f; b = c * 0.93f;
        }

        private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
            float len2 = vx * vx + vy * vy;
            float t = len2 < 1e-9f ? 0f : Math.Max(0f, Math.Min(1f, (wx * vx + wy * vy) / len2));
            float cx = ax + vx * t - px, cy = ay + vy * t - py;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>Marcas viales de pintura gastada: línea amarilla discontinua, paso de cebra y flecha casi borrada.</summary>
        private static void RoadPaint(DecalKind kind, float u, float v, int seed, out float r, out float g, out float b, out float a)
        {
            // bordes irregulares: se deforma un poco la posición antes de evaluar la forma
            float wu = u + 0.012f * TileableNoise.Perlin(u * 40f, v * 40f, 40, seed + 1);
            float wv = v + 0.012f * TileableNoise.Perlin(u * 40f + 9f, v * 40f, 40, seed + 2);
            float cover = 0f;
            bool yellow = false;
            float wear = 0.38f;

            if (kind == DecalKind.DashLine)
            {
                cover = (wu > 0.4f && wu < 0.6f && wv > 0.03f && wv < 0.97f) ? 1f : 0f;
                yellow = true;
            }
            else if (kind == DecalKind.Zebra)
            {
                float st = wu * 9f, fr = st - (float)Math.Floor(st);
                cover = ((int)Math.Floor(st) % 2 == 0 && fr > 0.08f && fr < 0.92f && wv > 0.06f && wv < 0.94f) ? 1f : 0f;
                wear = 0.42f;
            }
            else
            {
                bool shaft = wu > 0.41f && wu < 0.59f && wv > 0.04f && wv < 0.56f;
                bool head = wv >= 0.56f && wv < 0.96f && Math.Abs(wu - 0.5f) < (0.96f - wv) / 0.4f * 0.27f;
                cover = shaft || head ? 1f : 0f;
                wear = 0.5f; // las flechas están casi borradas
            }

            float keep = Smooth(wear - 0.05f, wear + 0.05f, TileableNoise.Fbm(u, v, 7, 4, seed + 3) + 0.08f * TileableNoise.Perlin(u * 70f, v * 70f, 70, seed + 4));
            float edge = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            a = cover * keep * 0.92f * Smooth(0.02f, 0.05f, edge); // el borde siempre transparente
            float grime = 0.65f + 0.35f * TileableNoise.Fbm(u, v, 12, 3, seed + 5);
            if (yellow) { r = 0.66f * grime; g = 0.52f * grime; b = 0.12f * grime; }
            else { r = 0.74f * grime; g = 0.73f * grime; b = 0.68f * grime; }
        }

        /// <summary>Mancha oscura y mate (aceite, hollín, grasa): irregular, con gotas alrededor.</summary>
        private static void Stain(float u, float v, int seed, out float r, out float g, out float b, out float a)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float rr = dist + (TileableNoise.Fbm(u, v, 6, 4, seed + 7) - 0.5f) * 0.5f;
            float body = 1f - Smooth(0.18f, 0.4f, rr);
            TileableNoise.Voronoi(u, v, 12, seed + 8, out float f1, out _, out float id);
            float drops = id > 0.7f ? (1f - Smooth(0.06f, 0.16f, f1)) * Smooth(0.15f, 0.25f, dist) : 0f;
            a = Math.Max(body * 0.8f, drops * 0.7f) * (1f - Smooth(0.43f, 0.5f, dist)) * Smooth(0.02f, 0.05f, Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v))); // borde siempre transparente
            float tone = 0.7f + 0.6f * TileableNoise.Fbm(u, v, 10, 3, seed + 9);
            r = 0.05f * tone; g = 0.045f * tone; b = 0.04f * tone;
        }

        /// <summary>Quemadura: centro carbonizado, borde irregular con vetas radiales y un halo de ceniza gris.</summary>
        private static void Burn(float u, float v, int seed, out float r, out float g, out float b, out float a)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float ang = (float)Math.Atan2(dy, dx);
            float streak = 0.5f + 0.5f * TileableNoise.Perlin((ang / 6.2832f + 0.5f) * 12f, dist * 10f, 12, 10, seed + 11); // vetas que salen del centro
            float rr = dist + (TileableNoise.Fbm(u, v, 5, 4, seed + 10) - 0.5f) * 0.32f + (streak - 0.5f) * 0.08f;
            float core = 1f - Smooth(0.13f, 0.27f, rr);
            float halo = 1f - Smooth(0.25f, 0.46f, rr);
            a = Math.Max(core * 0.96f, halo * 0.55f) * (1f - Smooth(0.44f, 0.5f, dist)) * Smooth(0.02f, 0.05f, Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v)));
            float t = core;
            float tone = 0.8f + 0.4f * TileableNoise.Fbm(u, v, 14, 3, seed + 12);
            r = (0.02f * t + 0.2f * (1f - t)) * tone; g = (0.018f * t + 0.19f * (1f - t)) * tone; b = (0.017f * t + 0.18f * (1f - t)) * tone;
        }

        private static void Puddle(float u, float v, int seed, out float r, out float g, out float b, out float a)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float rr = dist + (TileableNoise.Fbm(u, v, 4, 4, seed + 6) - 0.5f) * 0.42f;
            a = (1f - Smooth(0.28f, 0.42f, rr)) * 0.93f * (1f - Smooth(0.44f, 0.5f, dist));
            float rim = Smooth(0.2f, 0.34f, rr) * (1f - Smooth(0.34f, 0.42f, rr)); // borde húmedo algo más claro
            r = 0.03f + 0.045f * rim; g = 0.036f + 0.05f * rim; b = 0.045f + 0.055f * rim;
        }
    }
}
