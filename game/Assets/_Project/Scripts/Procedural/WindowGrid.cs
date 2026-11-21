using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Ventanas de los edificios lejanos (puro): una rejilla de Cols x Rows ventanas en una textura de 2 píxeles por ventana (ventana + hueco de pared).
    /// La mayoría apagadas; ~12 % encendidas en amarillo cálido, naranja o azul pálido. Flicker() cambia unas pocas a la vez manteniendo esa proporción
    /// (alguna se apaga, otra se enciende): da vida sin luces reales.
    /// </summary>
    public static class WindowGrid
    {
        public const int Cols = 64, Rows = 64, Size = Cols * 2;
        public const float LitFraction = 0.12f;
        /// <summary>Metros por ventana en horizontal y por planta en vertical.</summary>
        public const float WindowMeters = 1.8f, FloorMeters = 3.2f;

        public static readonly byte[][] Palette =
        {
            new byte[] { 255, 200, 110 },   // amarillo cálido
            new byte[] { 255, 135, 50 },    // naranja
            new byte[] { 160, 200, 255 },   // azul pálido
        };
        public static readonly double[] PaletteWeights = { 0.6, 0.25, 0.15 };
        public static readonly byte[] Glass = { 14, 16, 20 }, Wall = { 62, 64, 68 };

        public struct Cell { public bool Lit; public int Color; }

        public static Cell[] Generate(int seed, float lit = LitFraction)
        {
            var rng = new Random(seed);
            var c = new Cell[Cols * Rows];
            for (int i = 0; i < c.Length; i++) c[i] = new Cell { Lit = rng.NextDouble() < lit, Color = PickColor(rng) };
            return c;
        }

        public static int PickColor(Random rng)
        {
            double r = rng.NextDouble(), acc = 0;
            for (int i = 0; i < PaletteWeights.Length; i++) { acc += PaletteWeights[i]; if (r < acc) return i; }
            return 0;
        }

        public static float LitRatio(Cell[] c) { int n = 0; foreach (var x in c) if (x.Lit) n++; return n / (float)c.Length; }

        /// <summary>RGBA32 de la textura (Size x Size): ventanas en píxeles pares, pared en los impares.</summary>
        public static byte[] Pixels(Cell[] cells)
        {
            var p = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    byte[] rgb = (x % 2 == 0 && y % 2 == 0) ? ColorOf(cells[(y / 2) * Cols + x / 2]) : Wall;
                    int o = (y * Size + x) * 4;
                    p[o] = rgb[0]; p[o + 1] = rgb[1]; p[o + 2] = rgb[2]; p[o + 3] = 255;
                }
            return p;
        }

        public static byte[] ColorOf(Cell c) => c.Lit ? Palette[c.Color] : Glass;

        /// <summary>Cambia "changes" ventanas: si hay más encendidas que la proporción, apaga una; si no, enciende otra. Devuelve los índices tocados.</summary>
        public static int[] Flicker(Cell[] cells, Random rng, int changes = 2, float lit = LitFraction)
        {
            var touched = new int[changes];
            for (int k = 0; k < changes; k++)
            {
                bool turnOff = LitRatio(cells) > lit;
                int i = rng.Next(cells.Length);
                for (int tries = 0; tries < cells.Length && cells[i].Lit != turnOff; tries++) i = (i + 7919) % cells.Length;
                cells[i].Lit = !turnOff;
                if (!turnOff) cells[i].Color = PickColor(rng);
                touched[k] = i;
            }
            return touched;
        }

        /// <summary>Trozo de la textura para una fachada de "width" x "height" metros (tiling y offset para _BaseMap_ST), a partir de una semilla.</summary>
        public static float[] FacadeST(float width, float height, int seed)
        {
            int c = Math.Max(1, Math.Min(Cols, (int)Math.Round(width / WindowMeters)));
            int r = Math.Max(1, Math.Min(Rows, (int)Math.Round(height / FloorMeters)));
            var rng = new Random(seed);
            int ox = rng.Next(Cols - c + 1) * 2, oy = rng.Next(Rows - r + 1) * 2;
            return new[] { c * 2f / Size, r * 2f / Size, ox / (float)Size, oy / (float)Size };
        }

        /// <summary>Baliza roja de azotea: encendida 0.5 s de cada 2.2 s, con la fase de cada edificio.</summary>
        public static bool BeaconOn(double time, float phase) { double t = (time + phase) % 2.2; return t < 0.5; }
    }
}
