using System;

namespace SevenDays.Procedural
{
    public enum SurfaceKind { Asphalt, Wall, Concrete, Metal, Container, CrateWood, Cardboard }

    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        public Rgb Scale(float k) => new Rgb(R * k, G * k, B * k);
        public float Luma => 0.299f * R + 0.587f * G + 0.114f * B;
    }

    /// <summary>Parámetros del estilo "ruinas industriales": paleta oscura y desgastada. Los colores están en espacio sRGB (0..1).</summary>
    public class SurfaceParams
    {
        public int Size = 512;
        public int Seed = 1337;

        public Rgb Asphalt = new Rgb(0.17f, 0.17f, 0.18f);
        public Rgb ConcreteA = new Rgb(0.30f, 0.29f, 0.27f);
        public Rgb ConcreteB = new Rgb(0.46f, 0.44f, 0.40f);
        public Rgb BrickA = new Rgb(0.22f, 0.10f, 0.08f);
        public Rgb BrickB = new Rgb(0.36f, 0.19f, 0.13f);
        public Rgb Mortar = new Rgb(0.30f, 0.28f, 0.26f);
        public Rgb Plaster = new Rgb(0.40f, 0.37f, 0.32f);
        public Rgb MetalPaint = new Rgb(0.32f, 0.34f, 0.35f);
        public Rgb Rust = new Rgb(0.45f, 0.21f, 0.09f);
        public Rgb RustDark = new Rgb(0.22f, 0.09f, 0.05f);
        /// <summary>Pintura base (neutra) del contenedor; el color final lo da el tinte de cada contenedor.</summary>
        public Rgb ContainerPaint = new Rgb(0.42f, 0.42f, 0.43f);
        public Rgb WoodA = new Rgb(0.34f, 0.23f, 0.14f);
        public Rgb WoodB = new Rgb(0.50f, 0.36f, 0.22f);
        public Rgb CardboardColor = new Rgb(0.58f, 0.46f, 0.30f);

        /// <summary>Multiplicador de brillo de toda la paleta (1 = valores base). No borra la suciedad: solo sube/baja el conjunto.</summary>
        public float Brightness = 1.3f;

        /// <summary>0..1: cuánto metal está oxidado.</summary>
        public float RustAmount = 0.5f;
        /// <summary>0..1: cantidad de grietas.</summary>
        public float CrackAmount = 0.5f;
        /// <summary>0..1: manchas y chorreras de suciedad.</summary>
        public float DirtAmount = 0.5f;
        public float NormalStrength = 2.5f;

        public SurfaceParams Clone() => (SurfaceParams)MemberwiseClone();
    }

    public class SurfaceResult
    {
        public int Size;
        /// <summary>RGBA32, fila 0 abajo (convención de Unity), alfa 255.</summary>
        public byte[] Diffuse;
        /// <summary>Mapa de normales RGBA32 (R,G = XY, convención OpenGL/Unity), alfa 255.</summary>
        public byte[] Normal;
        public float[] Height;
    }

    /// <summary>Genera mapa de color y de normales de una superficie. Es determinista (misma semilla = mismo resultado) y no usa UnityEngine.</summary>
    public static partial class SurfaceGenerator
    {
        private delegate void PixelFn(float u, float v, SurfaceParams p, out Rgb color, out float height);

        public static SurfaceResult Generate(SurfaceKind kind, SurfaceParams p)
        {
            int n = p.Size;
            PixelFn fn = kind switch
            {
                SurfaceKind.Asphalt => Asphalt,
                SurfaceKind.Wall => Wall,
                SurfaceKind.Concrete => Concrete,
                SurfaceKind.Container => Container,
                SurfaceKind.CrateWood => CrateWood,
                SurfaceKind.Cardboard => Cardboard,
                _ => Metal,
            };

            var height = new float[n * n];
            var diffuse = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    fn((x + 0.5f) / n, (y + 0.5f) / n, p, out Rgb c, out float h);
                    c = c.Scale(p.Brightness);
                    int i = y * n + x;
                    height[i] = h;
                    diffuse[i * 4] = ToByte(c.R);
                    diffuse[i * 4 + 1] = ToByte(c.G);
                    diffuse[i * 4 + 2] = ToByte(c.B);
                    diffuse[i * 4 + 3] = 255;
                }
            }
            return new SurfaceResult { Size = n, Diffuse = diffuse, Height = height, Normal = NormalFromHeight(height, n, p.NormalStrength) };
        }

        public static byte[] NormalFromHeight(float[] h, int n, float strength)
        {
            var normal = new byte[n * n * 4];
            float k = strength * n / 64f; // 1.0 = pendiente de 45° para un relieve de altura 1 repartido en 64 píxeles
            for (int y = 0; y < n; y++)
            {
                int ym = TileableNoise.Mod(y - 1, n), yp = TileableNoise.Mod(y + 1, n);
                for (int x = 0; x < n; x++)
                {
                    int xm = TileableNoise.Mod(x - 1, n), xp = TileableNoise.Mod(x + 1, n);
                    float dx = (h[y * n + xp] - h[y * n + xm]) * 0.5f * k;
                    float dy = (h[yp * n + x] - h[ym * n + x]) * 0.5f * k;
                    float nx = -dx, ny = -dy, nz = 1f;
                    float inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    int i = (y * n + x) * 4;
                    normal[i] = ToByte(nx * inv * 0.5f + 0.5f);
                    normal[i + 1] = ToByte(ny * inv * 0.5f + 0.5f);
                    normal[i + 2] = ToByte(nz * inv * 0.5f + 0.5f);
                    normal[i + 3] = 255;
                }
            }
            return normal;
        }

        private static byte ToByte(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
        private static float Smooth(float a, float b, float x) => TileableNoise.Smooth(a, b, x);
        private static float Fbm(float u, float v, int f, int o, int s) => TileableNoise.Fbm(u, v, f, o, s);
        private static Rgb Mul(Rgb c, float k) => c.Scale(k);

        /// <summary>Grietas: bordes de un Voronoi deformado, solo en las zonas donde un ruido lento lo permite. 0..1.</summary>
        public static float Cracks(float u, float v, float amount, int seed)
        {
            if (amount <= 0.001f) return 0f;
            float wu = u + 0.025f * TileableNoise.Perlin(u * 9f, v * 9f, 9, seed + 1);
            float wv = v + 0.025f * TileableNoise.Perlin(u * 9f + 40f, v * 9f + 40f, 9, seed + 2);
            TileableNoise.Voronoi(wu, wv, 6, seed + 3, out float f1, out float f2, out _);
            float line = 1f - Smooth(0.0f, 0.07f, f2 - f1);
            float zone = Fbm(u, v, 3, 3, seed + 4);
            float thr = 0.78f - amount * 0.38f;
            return line * Smooth(thr, thr + 0.07f, zone);
        }

        // ---------------------------------------------------------------- Concreto
        private static void Concrete(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            int s = p.Seed;
            float macro = Fbm(u, v, 4, 4, s + 1);
            float mid = Fbm(u, v, 16, 3, s + 2);
            float grain = 0.5f + 0.5f * TileableNoise.Perlin(u * 128f, v * 128f, 128, s + 3);

            Rgb c = Rgb.Lerp(p.ConcreteA, p.ConcreteB, Smooth(0.3f, 0.7f, macro * 0.7f + mid * 0.3f));
            c = Mul(c, 0.82f + 0.36f * grain);

            // Poros: pequeñas manchas oscuras en algunas celdas
            TileableNoise.Voronoi(u, v, 40, s + 5, out float f1, out _, out float id);
            float pit = id > 0.72f ? 1f - Smooth(0.04f, 0.2f, f1) : 0f;
            c = Mul(c, 1f - 0.55f * pit);

            // Manchas de suciedad y chorreras verticales
            float stain = Smooth(0.52f - 0.1f * p.DirtAmount, 0.74f - 0.1f * p.DirtAmount, Fbm(u, v, 3, 4, s + 6)) * p.DirtAmount;
            float streak = Smooth(0.55f, 0.85f, 0.5f + 0.5f * TileableNoise.Perlin(u * 22f, v * 3f, 22, 3, s + 7)) * p.DirtAmount;
            c = Mul(c, 1f - 0.55f * stain - 0.25f * streak);

            float crack = Cracks(u, v, p.CrackAmount, s + 8);
            c = Mul(c, 1f - 0.85f * crack);

            color = c;
            height = macro * 0.35f + mid * 0.15f + grain * 0.12f - pit * 0.3f - crack * 0.8f;
        }

        // ---------------------------------------------------------------- Asfalto
        private static void Asphalt(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            int s = p.Seed + 100;
            float macro = Fbm(u, v, 4, 4, s + 1);
            float fine = 0.5f + 0.5f * TileableNoise.Perlin(u * 256f, v * 256f, 256, s + 2);
            float aggregate = Smooth(0.62f, 0.9f, 0.5f + 0.5f * TileableNoise.Perlin(u * 160f + 13f, v * 160f, 160, s + 3));

            Rgb c = Mul(p.Asphalt, 0.8f + 0.4f * macro);
            c = Mul(c, 0.85f + 0.3f * fine);
            Rgb speck = Mul(p.Asphalt, 3.2f);
            c = Rgb.Lerp(c, speck, aggregate * 0.6f);

            // Manchas de aceite / agua sucia
            float oil = Smooth(0.55f - 0.08f * p.DirtAmount, 0.75f, Fbm(u, v, 3, 4, s + 4)) * p.DirtAmount;
            c = Mul(c, 1f - 0.5f * oil);

            float crack = Cracks(u, v, p.CrackAmount * 1.2f, s + 5);
            c = Mul(c, 1f - 0.9f * crack);

            color = c;
            height = fine * 0.2f + aggregate * 0.25f + macro * 0.15f - crack * 0.8f;
        }

        // ---------------------------------------------------------------- Muro de ladrillo y revoque
        private static void Wall(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            const int cols = 4, rows = 8; // ladrillo 2:1; filas pares => encaja al repetir la textura
            int s = p.Seed + 200;

            float fy = v * rows;
            int row = (int)Math.Floor(fy);
            float ry = fy - row;
            float fx = u * cols + ((row & 1) == 1 ? 0.5f : 0f);
            int col = (int)Math.Floor(fx);
            float rx = fx - col;

            float ex = Math.Min(rx, 1f - rx), ey = Math.Min(ry, 1f - ry);
            float brick = Smooth(0.025f, 0.07f, ex) * Smooth(0.05f, 0.14f, ey); // 0 = mortero, 1 = ladrillo

            float id = TileableNoise.Hash01(TileableNoise.Mod(col, cols), TileableNoise.Mod(row, rows), s + 1);
            float tex = Fbm(u, v, 32, 3, s + 2);
            Rgb brickColor = Rgb.Lerp(p.BrickA, p.BrickB, id * 0.8f + tex * 0.2f);
            brickColor = Mul(brickColor, 0.85f + 0.3f * tex);
            Rgb mortarColor = Mul(p.Mortar, 0.8f + 0.4f * tex);
            Rgb baseColor = Rgb.Lerp(mortarColor, brickColor, brick);
            float baseHeight = 0.35f + 0.4f * brick + 0.1f * tex;

            // Revoque: parches que cubren el ladrillo, con bordes irregulares y desprendidos
            float cover = Fbm(u, v, 3, 4, s + 3) + 0.1f * TileableNoise.Perlin(u * 48f, v * 48f, 48, s + 4);
            float plaster = Smooth(0.5f, 0.55f, cover);
            Rgb plasterColor = Mul(p.Plaster, 0.82f + 0.36f * Fbm(u, v, 12, 3, s + 5));
            Rgb c = Rgb.Lerp(baseColor, plasterColor, plaster);
            float h = baseHeight + (0.62f - baseHeight) * plaster;

            float stain = Smooth(0.5f, 0.75f, Fbm(u, v, 3, 4, s + 6)) * p.DirtAmount;
            float streak = Smooth(0.55f, 0.85f, 0.5f + 0.5f * TileableNoise.Perlin(u * 20f, v * 2f, 20, 2, s + 7)) * p.DirtAmount;
            c = Mul(c, 1f - 0.5f * stain - 0.3f * streak);

            float crack = Cracks(u, v, p.CrackAmount * (0.4f + 0.8f * plaster), s + 8);
            c = Mul(c, 1f - 0.85f * crack);

            color = c;
            height = h - crack * 0.6f;
        }

        // ---------------------------------------------------------------- Metal oxidado
        private static void Metal(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            int s = p.Seed + 300;

            // Paneles 2x2 con costuras y remaches
            float du = Math.Min(Frac(u * 2f), 1f - Frac(u * 2f)) * 0.5f; // distancia a la costura vertical más cercana
            float dv = Math.Min(Frac(v * 2f), 1f - Frac(v * 2f)) * 0.5f;
            float seam = Math.Min(du, dv);
            float groove = 1f - Smooth(0.0f, 0.012f, seam);
            float rivet = Rivets(u, v, du, dv);

            float macro = Fbm(u, v, 8, 3, s + 1);
            Rgb paint = Mul(p.MetalPaint, 0.8f + 0.4f * macro);

            // Rayones finos (ruido muy estirado)
            float scratch = Smooth(0.82f, 0.95f, 0.5f + 0.5f * TileableNoise.Perlin(u * 3f, v * 150f, 3, 150, s + 2));
            paint = Mul(paint, 1f + 0.35f * scratch);

            // Óxido: manchas grandes + detalle, más intenso cerca de costuras y con chorreras verticales
            float field = Fbm(u, v, 5, 5, s + 3) * 0.7f + Fbm(u, v, 24, 3, s + 4) * 0.3f;
            float drip = 0.5f + 0.5f * TileableNoise.Perlin(u * 14f, v * 2f, 14, 2, s + 5);
            float seamBias = (1f - Smooth(0.0f, 0.05f, seam)) * 0.22f;
            float thr = 1.02f - p.RustAmount * 0.75f;
            float rustMask = Smooth(thr, thr + 0.14f, field + 0.18f * drip + seamBias);
            Rgb rustColor = Rgb.Lerp(p.RustDark, p.Rust, Fbm(u, v, 40, 3, s + 6));
            Rgb c = Rgb.Lerp(paint, rustColor, rustMask * 0.95f);

            float dirt = Smooth(0.55f, 0.8f, Fbm(u, v, 4, 4, s + 7)) * p.DirtAmount;
            c = Mul(c, 1f - 0.45f * dirt - 0.5f * groove);

            color = c;
            float pit = Fbm(u, v, 64, 2, s + 8);
            height = 0.6f - groove * 0.35f + rivet * 0.2f + rustMask * (pit - 0.5f) * 0.4f - rustMask * 0.05f;
        }

        private static float Frac(float x) => x - (float)Math.Floor(x);

        private static float Rivets(float u, float v, float du, float dv)
        {
            // Remaches cada 1/16 de textura a ambos lados de las costuras
            const float spacing = 1f / 16f, offset = 0.022f;
            float cu = Frac(u * 16f) - 0.5f, cv = Frac(v * 16f) - 0.5f;
            float a = Hypot(cu * spacing, dv - offset);  // junto a la costura horizontal
            float b = Hypot(du - offset, cv * spacing);  // junto a la costura vertical
            float d = Math.Min(a, b);
            return 1f - Smooth(0.0f, 0.009f, d);
        }

        private static float Hypot(float a, float b) => (float)Math.Sqrt(a * a + b * b);
    }
}
