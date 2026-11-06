using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Ruido determinista y "tileable" (el borde derecho encaja con el izquierdo), para que las texturas
    /// se repitan sin costuras. C# puro, sin UnityEngine: se puede probar y ejecutar en otros hilos.
    /// </summary>
    public static class TileableNoise
    {
        private static readonly float[] GradX = { 1f, -1f, 1f, -1f, 1f, -1f, 0f, 0f };
        private static readonly float[] GradY = { 1f, 1f, -1f, -1f, 0f, 0f, 1f, -1f };

        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u + 3266489917u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>Número pseudoaleatorio estable en [0,1) para una celda entera.</summary>
        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFFu) / 16777216f;

        public static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Math.Min(1f, Math.Max(0f, (x - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Corner(int ix, int iy, int seed, float dx, float dy)
        {
            int g = (int)(Hash(ix, iy, seed) & 7u);
            return GradX[g] * dx + GradY[g] * dy;
        }

        /// <summary>
        /// Ruido de gradiente en [-1,1]. (x,y) está en unidades de la rejilla; se repite cada
        /// periodX/periodY celdas.
        /// </summary>
        public static float Perlin(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            int xa = Mod(x0, periodX), xb = Mod(x0 + 1, periodX);
            int ya = Mod(y0, periodY), yb = Mod(y0 + 1, periodY);
            float n00 = Corner(xa, ya, seed, fx, fy);
            float n10 = Corner(xb, ya, seed, fx - 1f, fy);
            float n01 = Corner(xa, yb, seed, fx, fy - 1f);
            float n11 = Corner(xb, yb, seed, fx - 1f, fy - 1f);
            float u = Fade(fx), v = Fade(fy);
            float r = Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v);
            return Math.Max(-1f, Math.Min(1f, r));
        }

        public static float Perlin(float x, float y, int period, int seed) => Perlin(x, y, period, period, seed);

        /// <summary>Suma de octavas en [0,1] (centrada en 0.5). u,v son coordenadas de la textura (0..1 = una repetición).</summary>
        public static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int freq = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += Perlin(u * freq, v * freq, freq, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2;
            }
            float r = 0.5f + 0.5f * (sum / norm) * 1.6f; // ×1.6: el ruido rara vez llega a ±1
            return Math.Max(0f, Math.Min(1f, r));
        }

        /// <summary>
        /// Ruido celular tileable. f1/f2 = distancia al punto más cercano / segundo más cercano (en celdas);
        /// f2-f1 es casi 0 en los bordes entre celdas, lo que sirve para dibujar grietas. id = valor estable de la celda más cercana.
        /// </summary>
        public static void Voronoi(float u, float v, int cells, int seed, out float f1, out float f2, out float id)
        {
            float x = u * cells, y = v * cells;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            f1 = float.MaxValue;
            f2 = float.MaxValue;
            id = 0f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    int wx = Mod(nx, cells), wy = Mod(ny, cells);
                    float px = nx + Hash01(wx, wy, seed);
                    float py = ny + Hash01(wx, wy, seed + 77);
                    float ddx = px - x, ddy = py - y;
                    float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = Hash01(wx, wy, seed + 151);
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
        }
    }
}
