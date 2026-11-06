using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum GroundLayer { Earth, Gravel, Ash, Mud, Grass, Concrete }

    /// <summary>Parámetros del piso fuera de la autopista. Los pesos Earth..Concrete son abundancia relativa (0 = nada, 1 = normal, 2 = el doble).</summary>
    public class GroundParams
    {
        public int Size = 1024;
        public int Seed = 21;
        /// <summary>El mapa cubre [-WorldHalf, WorldHalf] en X y Z (metros).</summary>
        public float WorldHalf = 30f;
        /// <summary>Mitad del ancho de la autopista (no se toca: el asfalto va encima).</summary>
        public float RoadHalfWidth = 4f;
        /// <summary>Ancho de la franja junto a la vía donde aumentan el escombro y la arena.</summary>
        public float ShoulderWidth = 7f;
        /// <summary>1 = zonas de ~15 m. Más pequeño = zonas más grandes.</summary>
        public float ZoneScale = 1f;

        public float Earth = 1f, Gravel = 1f, Ash = 1f, Mud = 1f, DryGrass = 1f, Concrete = 1f;
        public float RoadDebris = 1f;
        public float Cracks = 1f, Pebbles = 1f, Ruts = 1f, Footprints = 1f;
        public float NormalStrength = 1.6f;
        public float Brightness = 1f;

        public Rgb EarthA = new Rgb(0.30f, 0.23f, 0.16f), EarthB = new Rgb(0.46f, 0.37f, 0.26f);
        public Rgb Sand = new Rgb(0.58f, 0.50f, 0.36f);
        public Rgb GravelA = new Rgb(0.22f, 0.21f, 0.20f), GravelB = new Rgb(0.52f, 0.49f, 0.43f);
        public Rgb AshA = new Rgb(0.34f, 0.34f, 0.33f), AshB = new Rgb(0.52f, 0.51f, 0.49f);
        public Rgb MudColor = new Rgb(0.17f, 0.125f, 0.085f);
        public Rgb GrassA = new Rgb(0.42f, 0.38f, 0.20f), GrassB = new Rgb(0.56f, 0.50f, 0.29f);
        public Rgb ConcreteA = new Rgb(0.36f, 0.35f, 0.33f), ConcreteB = new Rgb(0.55f, 0.54f, 0.50f);

        public GroundParams Clone() => (GroundParams)MemberwiseClone();
    }

    /// <summary>Búferes compartidos para generar el piso en tiras (cada hilo escribe filas distintas).</summary>
    public class GroundBuffers
    {
        public readonly int Size;
        public readonly byte[] Diffuse;   // RGBA: alfa = suavidad (barro mojado brilla)
        public readonly float[] Height;
        public GroundBuffers(int size) { Size = size; Diffuse = new byte[size * size * 4]; Height = new float[size * size]; }
    }

    /// <summary>
    /// Piso por capas mezcladas con un mapa grande que NO se repite: tierra seca agrietada, grava, ceniza, charcos de barro, parches de hierba
    /// seca y restos de concreto roto. La mezcla va por ruido a gran escala (zonas de ~15 m) y por cercanía a la autopista (más escombro y arena
    /// junto al borde de la vía). Incluye relieve para el mapa de normales: grietas, piedras sueltas, rodadas y huellas. C# puro y determinista.
    /// </summary>
    public static class GroundGenerator
    {
        public const int LayerCount = 6;

        // ------------------------------------------------------------------ zonas
        /// <summary>Peso de cada capa en el punto (x, z) del mundo; suman 1. Las zonas son grandes y el borde de la vía favorece escombro y grava.</summary>
        public static float[] Weights(GroundParams p, float x, float z)
        {
            var w = new float[LayerCount];
            Weights(p, x, z, w);
            return w;
        }

        public static float RoadBand(GroundParams p, float x)
        {
            float d = Math.Abs(x) - p.RoadHalfWidth;
            return 1f - TileableNoise.Smooth(0f, p.ShoulderWidth, Math.Max(0f, d));
        }

        private static int ZoneFreq(GroundParams p) => Math.Max(1, (int)Math.Round(4f / Math.Max(0.1f, p.ZoneScale)));

        private static void Weights(GroundParams p, float x, float z, float[] w)
        {
            float u = (x + p.WorldHalf) / (2f * p.WorldHalf), v = (z + p.WorldHalf) / (2f * p.WorldHalf);
            // los bordes de las zonas se deforman un poco para que no sean curvas "de ruido uniforme"
            float wu = u + 0.035f * TileableNoise.Perlin(u * 6f, v * 6f, 6, p.Seed + 90);
            float wv = v + 0.035f * TileableNoise.Perlin(u * 6f + 5f, v * 6f, 6, p.Seed + 91);
            int f = ZoneFreq(p);
            float band = RoadBand(p, x) * p.RoadDebris;

            float s0 = (TileableNoise.Fbm(wu, wv, f, 4, p.Seed + 11) * p.Earth) + 0.15f;
            float s1 = TileableNoise.Fbm(wu, wv, f, 4, p.Seed + 12) * p.Gravel * 0.8f + 0.85f * band;
            float s2 = TileableNoise.Fbm(wu, wv, f, 4, p.Seed + 13) * p.Ash * 0.85f;
            float s3 = TileableNoise.Smooth(0.55f, 0.72f, TileableNoise.Fbm(wu, wv, f + 1, 4, p.Seed + 14)) * p.Mud * 1.6f * (1f - 0.7f * band);
            float s4 = TileableNoise.Smooth(0.5f, 0.68f, TileableNoise.Fbm(wu, wv, f, 4, p.Seed + 15)) * p.DryGrass * 1.4f * (1f - 0.85f * band);
            float s5 = TileableNoise.Smooth(0.6f, 0.78f, TileableNoise.Fbm(wu, wv, f + 1, 4, p.Seed + 16)) * p.Concrete * 1.2f + 0.55f * band * p.Concrete;

            w[0] = s0; w[1] = s1; w[2] = s2; w[3] = s3; w[4] = s4; w[5] = s5;
            float sum = 0f;
            for (int i = 0; i < LayerCount; i++)
            {
                float t = Math.Max(0f, w[i]);
                t *= t; t *= t; // potencia 4: zonas definidas en vez de mezcla uniforme
                w[i] = t; sum += t;
            }
            if (sum < 1e-6f) { for (int i = 0; i < LayerCount; i++) w[i] = 0f; w[0] = 1f; return; }
            for (int i = 0; i < LayerCount; i++) w[i] /= sum;
        }

        // ------------------------------------------------------------------ generación
        public static SurfaceResult Generate(GroundParams p)
        {
            var b = new GroundBuffers(p.Size);
            FillRows(p, b, 0, p.Size);
            return Finish(p, b);
        }

        /// <summary>Rellena las filas [y0, y1): color (con suavidad en el alfa) y altura. Se puede llamar desde varios hilos con filas distintas.</summary>
        public static void FillRows(GroundParams p, GroundBuffers b, int y0, int y1)
        {
            int n = p.Size;
            var w = new float[LayerCount];
            float span = 2f * p.WorldHalf;
            for (int y = y0; y < y1; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float wx = u * span - p.WorldHalf, wz = v * span - p.WorldHalf;
                    Weights(p, wx, wz, w);
                    float band = RoadBand(p, wx) * p.RoadDebris;

                    float r = 0, g = 0, bl = 0, h = 0, smooth = 0;
                    for (int l = 0; l < LayerCount; l++)
                    {
                        if (w[l] < 0.02f) continue;
                        Layer((GroundLayer)l, p, u, v, band, out Rgb c, out float lh, out float ls);
                        r += c.R * w[l]; g += c.G * w[l]; bl += c.B * w[l]; h += lh * w[l]; smooth += ls * w[l];
                    }

                    // rodadas de vehículos a los lados de la autopista (huellas largas y poco profundas)
                    float rut = Ruts(p, wx, wz);
                    h -= rut * 0.5f;
                    float dark = 1f - 0.22f * rut;

                    int i = y * n + x;
                    b.Height[i] = h;
                    b.Diffuse[i * 4] = B(r * dark); b.Diffuse[i * 4 + 1] = B(g * dark); b.Diffuse[i * 4 + 2] = B(bl * dark);
                    b.Diffuse[i * 4 + 3] = B(smooth);
                }
            }
        }

        /// <summary>Estampa las huellas, aplica el brillo y calcula el mapa de normales.</summary>
        public static SurfaceResult Finish(GroundParams p, GroundBuffers b)
        {
            int n = p.Size;
            StampFootprints(p, b);
            for (int i = 0; i < n * n; i++)
            {
                b.Diffuse[i * 4] = B(b.Diffuse[i * 4] / 255f * p.Brightness);
                b.Diffuse[i * 4 + 1] = B(b.Diffuse[i * 4 + 1] / 255f * p.Brightness);
                b.Diffuse[i * 4 + 2] = B(b.Diffuse[i * 4 + 2] / 255f * p.Brightness);
            }
            return new SurfaceResult { Size = n, Diffuse = b.Diffuse, Height = b.Height, Normal = SurfaceGenerator.NormalFromHeight(b.Height, n, p.NormalStrength) };
        }

        // ------------------------------------------------------------------ capas
        private static void Layer(GroundLayer layer, GroundParams p, float u, float v, float band, out Rgb color, out float height, out float smooth)
        {
            int s = p.Seed;
            switch (layer)
            {
                case GroundLayer.Earth:
                {
                    float tone = TileableNoise.Fbm(u, v, 24, 3, s + 21);
                    Rgb c = Rgb.Lerp(p.EarthA, p.EarthB, tone);
                    c = Rgb.Lerp(c, p.Sand, band * 0.5f * TileableNoise.Smooth(0.3f, 0.7f, TileableNoise.Fbm(u, v, 12, 3, s + 22))); // arena junto a la vía
                    TileableNoise.Voronoi(u, v, 36, s + 23, out float f1, out float f2, out _);
                    float zone = TileableNoise.Smooth(0.35f, 0.6f, TileableNoise.Fbm(u, v, 5, 3, s + 24));
                    float crack = (1f - TileableNoise.Smooth(0f, 0.09f, f2 - f1)) * zone * Math.Min(1.5f, p.Cracks); // tierra seca agrietada
                    float grain = 0.5f + 0.5f * TileableNoise.Perlin(u * 400f, v * 400f, 400, s + 25);
                    color = c.Scale((0.85f + 0.3f * grain) * (1f - 0.42f * crack));
                    height = 0.45f + 0.15f * tone + 0.05f * grain - 0.55f * crack;
                    smooth = 0.06f;
                    return;
                }
                case GroundLayer.Gravel:
                {
                    TileableNoise.Voronoi(u, v, 700, s + 31, out float f1, out _, out float id);
                    float dome = 1f - TileableNoise.Smooth(0.15f, 0.55f, f1);
                    float pebbles = Math.Min(1.5f, p.Pebbles);
                    Rgb stone = Rgb.Lerp(p.GravelA, p.GravelB, id);
                    Rgb dirt = p.GravelA.Scale(0.8f);
                    color = Rgb.Lerp(dirt, stone, dome).Scale(0.9f + 0.2f * TileableNoise.Fbm(u, v, 30, 2, s + 32));
                    height = 0.35f + 0.5f * dome * pebbles * (0.6f + 0.4f * id);
                    smooth = 0.14f;
                    return;
                }
                case GroundLayer.Ash:
                {
                    float soft = TileableNoise.Fbm(u, v, 14, 4, s + 41);
                    float streak = 0.5f + 0.5f * TileableNoise.Perlin(u * 8f, v * 90f, 8, 90, s + 42);
                    color = Rgb.Lerp(p.AshA, p.AshB, soft).Scale(0.9f + 0.2f * streak);
                    height = 0.4f + 0.12f * soft + 0.05f * streak;
                    smooth = 0.04f;
                    return;
                }
                case GroundLayer.Mud:
                {
                    float rip = TileableNoise.Fbm(u, v, 20, 3, s + 51);
                    color = p.MudColor.Scale(0.8f + 0.5f * rip);
                    height = 0.22f + 0.05f * rip;                              // hundido y casi liso
                    smooth = 0.78f;                                            // húmedo: brilla con las luces
                    return;
                }
                case GroundLayer.Grass:
                {
                    float tone = TileableNoise.Fbm(u, v, 18, 3, s + 61);
                    Rgb c = Rgb.Lerp(p.GrassA, p.GrassB, tone);
                    float blades = TileableNoise.Smooth(0.55f, 0.85f, 0.5f + 0.5f * TileableNoise.Perlin(u * 700f, v * 260f, 700, 260, s + 62));
                    color = Rgb.Lerp(c.Scale(0.8f), c.Scale(1.15f), blades);
                    height = 0.45f + 0.18f * blades;
                    smooth = 0.05f;
                    return;
                }
                default: // concreto roto: losas desiguales con grietas
                {
                    TileableNoise.Voronoi(u, v, 22, s + 71, out float f1, out float f2, out float id);
                    float edge = 1f - TileableNoise.Smooth(0f, 0.07f, f2 - f1);
                    Rgb c = Rgb.Lerp(p.ConcreteA, p.ConcreteB, id * 0.8f + 0.2f * TileableNoise.Fbm(u, v, 40, 3, s + 72));
                    float grit = 0.5f + 0.5f * TileableNoise.Perlin(u * 500f, v * 500f, 500, s + 73);
                    color = c.Scale((0.88f + 0.24f * grit) * (1f - 0.7f * edge));
                    height = 0.5f + 0.35f * id - 0.6f * edge;                  // cada losa a una altura distinta
                    smooth = 0.18f;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ rodadas y huellas
        private static float Ruts(GroundParams p, float x, float z)
        {
            if (p.Ruts <= 0f) return 0f;
            float best = 0f;
            float[] offsets = { 1.5f, 3.3f };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < offsets.Length; k++)
                {
                    float xr = side * (p.RoadHalfWidth + offsets[k]) + 0.45f * (float)Math.Sin(z * 0.17f + k * 2f + side);
                    float d = Math.Abs(x - xr);
                    float profile = 1f - TileableNoise.Smooth(0.08f, 0.32f, d);
                    float modulate = 0.55f + 0.45f * (float)Math.Sin(z * 0.43f + k * 1.7f);
                    best = Math.Max(best, profile * modulate);
                }
            }
            return best * Math.Min(1.5f, p.Ruts);
        }

        private static void StampFootprints(GroundParams p, GroundBuffers b)
        {
            if (p.Footprints <= 0f) return;
            int n = p.Size;
            var rng = new Random(p.Seed + 500);
            int trails = Math.Max(0, (int)Math.Round(3 * p.Footprints));
            float span = 2f * p.WorldHalf, ppm = n / span; // píxeles por metro
            for (int t = 0; t < trails; t++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (p.WorldHalf - 6f), z = -p.WorldHalf + 2f;
                double heading = Math.PI / 2 + (rng.NextDouble() - 0.5) * 0.9; // avanza hacia +Z con curvas suaves
                int steps = 40 + rng.Next(30);
                for (int s = 0; s < steps; s++)
                {
                    heading += (rng.NextDouble() - 0.5) * 0.18;
                    x += (float)Math.Cos(heading) * 0.62f; z += (float)Math.Sin(heading) * 0.62f;
                    if (Math.Abs(x) < p.RoadHalfWidth + 0.3f || Math.Abs(x) > p.WorldHalf - 1f || Math.Abs(z) > p.WorldHalf - 1f) continue;
                    float side = (s % 2 == 0) ? 0.13f : -0.13f;
                    float fx = x + (float)(-Math.Sin(heading)) * side, fz = z + (float)Math.Cos(heading) * side;
                    StampEllipse(b, p, fx, fz, (float)heading, 0.075f, 0.17f, 0.22f, ppm);
                }
            }
        }

        private static void StampEllipse(GroundBuffers b, GroundParams p, float wx, float wz, float heading, float rx, float rz, float depth, float ppm)
        {
            int n = p.Size;
            float cx = (wx + p.WorldHalf) * ppm, cy = (wz + p.WorldHalf) * ppm;
            int r = (int)Math.Ceiling(Math.Max(rx, rz) * ppm) + 1;
            float ca = (float)Math.Cos(heading), sa = (float)Math.Sin(heading);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
            {
                if (y < 0 || y >= n) continue;
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    if (x < 0 || x >= n) continue;
                    float dx = (x + 0.5f - cx) / ppm, dy = (y + 0.5f - cy) / ppm;
                    float along = dx * ca + dy * sa, across = -dx * sa + dy * ca; // la huella es alargada en la dirección de avance
                    float d = (float)Math.Sqrt((across / rx) * (across / rx) + (along / rz) * (along / rz));
                    if (d >= 1f) continue;
                    float k = 1f - TileableNoise.Smooth(0.5f, 1f, d);
                    b.Height[y * n + x] -= depth * k;
                    int i = (y * n + x) * 4;
                    float dark = 1f - 0.18f * k;
                    b.Diffuse[i] = (byte)(b.Diffuse[i] * dark); b.Diffuse[i + 1] = (byte)(b.Diffuse[i + 1] * dark); b.Diffuse[i + 2] = (byte)(b.Diffuse[i + 2] * dark);
                }
            }
        }

        private static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
    }

    /// <summary>Textura de detalle tileable (gris medio ≈ 0.5, para el modo "Detail x2" de URP Lit) y su mapa de normales: grano fino de tierra y piedrecitas.</summary>
    public static class GroundDetail
    {
        public static SurfaceResult Generate(int size, int seed)
        {
            int n = size;
            var height = new float[n * n];
            var diffuse = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float soft = TileableNoise.Fbm(u, v, 6, 3, seed + 1);
                    float grain = 0.5f + 0.5f * TileableNoise.Perlin(u * 96f, v * 96f, 96, seed + 2);
                    TileableNoise.Voronoi(u, v, 14, seed + 3, out float f1, out _, out _);
                    float stone = 1f - TileableNoise.Smooth(0.1f, 0.45f, f1);
                    float val = 0.5f + 0.22f * (soft - 0.5f) + 0.16f * (grain - 0.5f) + 0.12f * stone;
                    int i = y * n + x;
                    height[i] = 0.4f * soft + 0.25f * grain + 0.5f * stone;
                    byte c = (byte)(Math.Max(0f, Math.Min(1f, val)) * 255f + 0.5f);
                    diffuse[i * 4] = c; diffuse[i * 4 + 1] = c; diffuse[i * 4 + 2] = c; diffuse[i * 4 + 3] = 255;
                }
            }
            return new SurfaceResult { Size = n, Diffuse = diffuse, Height = height, Normal = SurfaceGenerator.NormalFromHeight(height, n, 2.2f) };
        }
    }
}
