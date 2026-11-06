using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public class SignParams
    {
        public int Size = 256;
        public int Seed = 1;
        /// <summary>0..1: cuánta pintura se ha descascarado.</summary>
        public float Wear = 0.5f;
        /// <summary>0..1: óxido en bordes y zonas peladas.</summary>
        public float Rust = 0.5f;
        /// <summary>0..1: suciedad y chorreras.</summary>
        public float Dirt = 0.5f;
        public int BulletHoles = 2;
        public int Dents = 3;
        public float Brightness = 1.0f;
    }

    /// <summary>
    /// Textura de una señal (color + normales) generada por código y desgastada: pintura descascarada que deja ver el acero
    /// galvanizado, óxido en los bordes y alrededor de los tornillos, abolladuras (relieve en el mapa de normales),
    /// suciedad, rayones y agujeros de bala. Símbolos genéricos: sin logos ni textos protegidos (el "pare" usa bloques abstractos).
    /// Las coordenadas están en metros y coinciden con SignGeometry.
    /// </summary>
    public static class SignGenerator
    {
        /// <summary>Cuántas variantes de desgaste existen por tipo (semilla y nivel de desgaste distintos).</summary>
        public const int Variants = 3;

        /// <summary>Parámetros de la variante v: desde "poco usada" hasta "destrozada".</summary>
        public static SignParams VariantParams(int variant, int size, int seed)
        {
            variant = Math.Abs(variant) % Variants;
            return new SignParams
            {
                Size = size,
                Seed = seed * 17 + variant * 101 + 7,
                Wear = new[] { 0.32f, 0.55f, 0.8f }[variant],
                Rust = new[] { 0.3f, 0.55f, 0.8f }[variant],
                Dirt = new[] { 0.35f, 0.5f, 0.7f }[variant],
                BulletHoles = new[] { 0, 2, 5 }[variant],
                Dents = new[] { 1, 3, 5 }[variant],
            };
        }

        private static readonly Rgb Red = new Rgb(0.62f, 0.07f, 0.07f);
        private static readonly Rgb White = new Rgb(0.72f, 0.71f, 0.66f);
        private static readonly Rgb Yellow = new Rgb(0.8f, 0.62f, 0.1f);
        private static readonly Rgb Blue = new Rgb(0.06f, 0.24f, 0.5f);
        private static readonly Rgb Black = new Rgb(0.04f, 0.04f, 0.045f);
        private static readonly Rgb Steel = new Rgb(0.44f, 0.46f, 0.47f);
        private static readonly Rgb RustColor = new Rgb(0.46f, 0.21f, 0.08f);
        private static readonly Rgb RustDark = new Rgb(0.22f, 0.09f, 0.05f);

        public static SurfaceResult Generate(SignType type, SignParams p)
        {
            int n = p.Size;
            var poly = SignGeometry.Outline(type);
            var (W, H) = SignGeometry.Dimensions(type);
            var rng = new Random(p.Seed);
            var dents = Blobs(rng, p.Dents, poly, 0.07f, 0.15f);
            var holes = Blobs(rng, p.BulletHoles, poly, 0.016f, 0.024f);
            var bolts = Bolts(type);

            var height = new float[n * n];
            var diffuse = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float mx = (u - 0.5f) * W, my = (v - 0.5f) * H;
                    float d = SignGeometry.SignedDistance(poly, mx, my);
                    Rgb paint = Paint(type, mx, my, d);

                    // --- desgaste de la pintura
                    float edge = 1f - Smooth(0f, 0.09f, -d);                       // 1 en el borde de la placa
                    float peelField = Fbm(u, v, 6, 4, p.Seed + 1) + edge * 0.28f + 0.07f * TileableNoise.Perlin(u * 90f, v * 90f, 90, p.Seed + 2);
                    float thr = 1f - p.Wear * 0.72f;
                    float peeled = Smooth(thr, thr + 0.07f, peelField);
                    Rgb metal = Steel.Scale(0.82f + 0.3f * Fbm(u, v, 12, 3, p.Seed + 3));
                    float rustField = Fbm(u, v, 9, 4, p.Seed + 4) + 0.3f * edge;
                    float rthr = 1f - p.Rust * 0.8f;
                    float rustMask = Smooth(rthr, rthr + 0.12f, rustField);
                    Rgb under = Rgb.Lerp(metal, Rgb.Lerp(RustDark, RustColor, Fbm(u, v, 30, 3, p.Seed + 5)), rustMask);
                    Rgb c = Rgb.Lerp(paint, under, peeled);

                    // óxido que sangra desde el borde aunque la pintura siga
                    c = Rgb.Lerp(c, RustColor, edge * edge * p.Rust * 0.55f * (0.5f + 0.5f * Fbm(u, v, 14, 2, p.Seed + 6)));

                    // --- suciedad: manchas, chorreras verticales y más mugre abajo
                    float grime = Smooth(0.5f, 0.8f, Fbm(u, v, 4, 4, p.Seed + 7)) * p.Dirt;
                    float drip = Smooth(0.6f, 0.9f, 0.5f + 0.5f * TileableNoise.Perlin(u * 20f, v * 2f, 20, 2, p.Seed + 8)) * p.Dirt * (1f - v);
                    c = c.Scale(1f - 0.45f * grime - 0.3f * drip - 0.25f * p.Dirt * (1f - v));

                    // --- rayones
                    float scratch = Smooth(0.86f, 0.96f, 0.5f + 0.5f * TileableNoise.Perlin(u * 50f, v * 4f, 50, 4, p.Seed + 9));
                    c = Rgb.Lerp(c, Steel, scratch * 0.45f);

                    // --- relieve base y abolladuras
                    float h = 0.5f - peeled * 0.04f + 0.012f * TileableNoise.Perlin(u * 120f, v * 120f, 120, p.Seed + 10);
                    float dent = 0f;
                    foreach (var b in dents) dent += Dent(mx, my, b);
                    h += dent;
                    c = c.Scale(1f - Math.Min(0.25f, Math.Abs(dent) * 1.2f)); // la abolladura también se ve en el color

                    // --- tornillos
                    foreach (var b in bolts)
                    {
                        float bd = Dist(mx, my, b.x, b.y);
                        if (bd < 0.03f)
                        {
                            float core = 1f - Smooth(0.012f, 0.018f, bd);
                            Rgb ring = Rgb.Lerp(RustColor, Steel.Scale(0.7f), 0.4f);
                            c = Rgb.Lerp(c, Rgb.Lerp(ring, Black.Scale(2f), core), 1f - Smooth(0.026f, 0.03f, bd));
                            h += (1f - Smooth(0.0f, 0.03f, bd)) * 0.05f;
                        }
                    }

                    // --- agujeros de bala: centro oscuro, aro de metal pelado y óxido alrededor, borde levantado
                    foreach (var b in holes)
                    {
                        float hd = Dist(mx, my, b.x, b.y);
                        float rr = b.r;
                        if (hd < rr * 3.2f)
                        {
                            float hole = 1f - Smooth(rr * 0.7f, rr, hd);
                            float rim = Smooth(rr * 0.9f, rr * 1.4f, hd) * (1f - Smooth(rr * 1.6f, rr * 2.4f, hd));
                            float halo = 1f - Smooth(rr * 1.4f, rr * 3.2f, hd);
                            c = Rgb.Lerp(c, Rgb.Lerp(Steel, RustColor, 0.5f), halo * 0.6f);
                            c = Rgb.Lerp(c, Black, hole);
                            h += rim * 0.05f - hole * 0.3f;
                        }
                    }

                    c = c.Scale(p.Brightness);
                    int i = y * n + x;
                    height[i] = h;
                    diffuse[i * 4] = B(c.R); diffuse[i * 4 + 1] = B(c.G); diffuse[i * 4 + 2] = B(c.B); diffuse[i * 4 + 3] = 255;
                }
            }
            return new SurfaceResult { Size = n, Diffuse = diffuse, Height = height, Normal = SurfaceGenerator.NormalFromHeight(height, n, 3.2f) };
        }

        // ------------------------------------------------------------------ pintura por tipo
        private static Rgb Paint(SignType type, float x, float y, float d)
        {
            switch (type)
            {
                case SignType.Stop:
                {
                    if (d > -0.03f) return White;                          // borde blanco fino
                    if (d > -0.055f) return Red;
                    // 4 bloques blancos que sugieren una palabra (sin letras reales)
                    for (int i = 0; i < 4; i++)
                    {
                        float cx = -0.225f + i * 0.15f;
                        if (Math.Abs(x - cx) < 0.05f && Math.Abs(y) < 0.075f && !(i % 2 == 1 && y > 0.01f && Math.Abs(x - cx) < 0.02f)) return White;
                    }
                    return Red;
                }
                case SignType.Yield:
                {
                    if (d > -0.075f) return Red;                           // borde rojo ancho
                    return White;
                }
                case SignType.Speed:
                {
                    if (d > -0.025f) return White;
                    if (d > -0.115f) return Red;
                    return Digits(x, y, 4, 0) ? Black : White;             // "40"
                }
                case SignType.NoTurn:
                {
                    if (d > -0.025f) return White;
                    if (d > -0.115f) return Red;
                    if (Slash(x, y)) return Red;
                    return Arrow(x, y) ? Black : White;
                }
                case SignType.Hazard:
                {
                    if (d > -0.03f) return Yellow;
                    if (d > -0.075f) return Black;                         // borde negro
                    bool bar = Math.Abs(x) < 0.03f && y > -0.12f && y < 0.14f;
                    bool dot = Dist(x, y, 0f, -0.2f) < 0.04f;
                    return bar || dot ? Black : Yellow;
                }
                default:
                {
                    if (d > -0.03f) return White;
                    if (d > -0.05f) return Blue;
                    // flecha blanca hacia la derecha
                    bool shaft = x > -0.3f && x < 0.12f && Math.Abs(y) < 0.05f;
                    bool head = x >= 0.12f && x < 0.32f && Math.Abs(y) < (0.32f - x) * 0.85f;
                    return shaft || head ? White : Blue;
                }
            }
        }

        private static bool Slash(float x, float y)
        {
            // barra diagonal roja de arriba-izquierda a abajo-derecha
            float dist = Math.Abs(x + y) / 1.4142f;
            return dist < 0.025f && Math.Abs(x) < 0.23f;
        }

        private static bool Arrow(float x, float y)
        {
            // giro a la izquierda: tallo vertical, tramo horizontal y punta
            bool stem = Math.Abs(x - 0.07f) < 0.025f && y > -0.17f && y < 0.06f;
            bool bend = y > 0.035f && y < 0.085f && x > -0.1f && x < 0.095f;
            bool head = x >= -0.2f && x < -0.1f && Math.Abs(y - 0.06f) < (x + 0.2f) * 0.8f;
            return stem || bend || head;
        }

        private static bool Digits(float x, float y, int left, int right)
        {
            // "40" con dígitos de siete segmentos
            return Seven(x + 0.09f, y, left) || Seven(x - 0.09f, y, right);
        }

        private static readonly int[] SegMask = { 0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011, 0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011 };

        private static bool Seven(float x, float y, int digit)
        {
            const float w = 0.055f, hgt = 0.13f, t = 0.022f;
            int m = SegMask[digit];
            bool Seg(int bit, float cx, float cy, float hw, float hh) => (m & (1 << (6 - bit))) != 0 && Math.Abs(x - cx) < hw && Math.Abs(y - cy) < hh;
            return Seg(0, 0, hgt, w, t) || Seg(1, w, hgt / 2f, t, hgt / 2f) || Seg(2, w, -hgt / 2f, t, hgt / 2f) ||
                   Seg(3, 0, -hgt, w, t) || Seg(4, -w, -hgt / 2f, t, hgt / 2f) || Seg(5, -w, hgt / 2f, t, hgt / 2f) || Seg(6, 0, 0, w, t);
        }

        // ------------------------------------------------------------------ elementos del desgaste
        private struct Blob { public float x, y, r, sign; }

        private static List<Blob> Blobs(Random rng, int count, IList<V2> poly, float rMin, float rMax)
        {
            var list = new List<Blob>();
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float x = ((float)rng.NextDouble() - 0.5f) * 0.8f, y = ((float)rng.NextDouble() - 0.5f) * 0.7f;
                    float r = rMin + (float)rng.NextDouble() * (rMax - rMin);
                    if (SignGeometry.SignedDistance(poly, x, y) > -r * 1.6f) continue; // que quede dentro de la placa
                    list.Add(new Blob { x = x, y = y, r = r, sign = rng.NextDouble() < 0.7 ? -1f : 1f });
                    break;
                }
            }
            return list;
        }

        private static float Dent(float x, float y, Blob b)
        {
            float d = Dist(x, y, b.x, b.y) / b.r;
            return b.sign * 0.12f * (float)Math.Exp(-d * d * 2.2f);
        }

        private static List<(float x, float y)> Bolts(SignType t)
        {
            switch (t)
            {
                case SignType.Stop: return new List<(float, float)> { (0f, 0.31f), (0f, -0.31f) };
                case SignType.Yield: return new List<(float, float)> { (-0.2f, 0.3f), (0.2f, 0.3f) };
                case SignType.Hazard: return new List<(float, float)> { (0f, 0.26f), (-0.28f, -0.3f), (0.28f, -0.3f) };
                case SignType.Generic: return new List<(float, float)> { (-0.42f, 0.26f), (0.42f, 0.26f), (-0.42f, -0.26f), (0.42f, -0.26f) };
                default: return new List<(float, float)> { (0f, 0.26f), (0f, -0.26f) };
            }
        }

        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float Smooth(float a, float b, float x) => TileableNoise.Smooth(a, b, x);
        private static float Fbm(float u, float v, int f, int o, int s) => TileableNoise.Fbm(u, v, f, o, s);
        private static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
    }
}
