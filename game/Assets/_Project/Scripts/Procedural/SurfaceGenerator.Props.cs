using System;

namespace SevenDays.Procedural
{
    /// <summary>Texturas de utilería: contenedor corrugado, caja de madera y caja de cartón.</summary>
    public static partial class SurfaceGenerator
    {
        private const float TwoPi = 6.2831853f;

        // ---------------------------------------------------------------- Contenedor
        // Una repetición = 2.6 m de alto (el alto real de un contenedor). Es tileable en horizontal y simétrica en vertical.
        private static void Container(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            const int ribs = 12; // chapa corrugada: ~22 cm por onda
            int s = p.Seed + 400;

            float wave = 0.5f + 0.5f * (float)Math.Sin(u * ribs * TwoPi);
            float rib = Smooth(0.12f, 0.88f, wave);

            float macro = Fbm(u, v, 5, 4, s + 1);
            Rgb paint = Mul(p.ContainerPaint, 0.82f + 0.36f * macro);
            paint = Mul(paint, 0.78f + 0.22f * rib); // los valles de la onda quedan más oscuros

            // Desgaste: la pintura se pela más hacia los bordes (arriba y abajo) y en manchas
            float edge = Math.Min(v, 1f - v);
            float edgeBias = (1f - Smooth(0f, 0.2f, edge)) * 0.28f;
            float wearField = Fbm(u, v, 6, 5, s + 2) + edgeBias + 0.1f * TileableNoise.Perlin(u * 60f, v * 60f, 60, s + 3);
            float thr = 1.0f - p.RustAmount * 0.62f;
            float wear = Smooth(thr, thr + 0.1f, wearField);
            Rgb rust = Rgb.Lerp(p.RustDark, p.Rust, Fbm(u, v, 36, 3, s + 4));
            Rgb c = Rgb.Lerp(paint, rust, wear * 0.92f);

            // Chorreras de óxido desde el borde superior
            float drip = Smooth(0.6f, 0.9f, 0.5f + 0.5f * TileableNoise.Perlin(u * 16f, v * 2f, 16, 2, s + 5));
            float topness = 1f - Smooth(0.15f, 0.55f, edge);
            c = Rgb.Lerp(c, rust, drip * topness * p.RustAmount * 0.8f);

            // Rayones horizontales que dejan ver el acero
            float scratch = Smooth(0.86f, 0.96f, 0.5f + 0.5f * TileableNoise.Perlin(u * 5f, v * 140f, 5, 140, s + 6));
            c = Rgb.Lerp(c, new Rgb(0.3f, 0.3f, 0.32f), scratch * 0.7f);

            // Golpes y suciedad
            float dent = Smooth(0.58f, 0.8f, Fbm(u, v, 3, 3, s + 7));
            float dirt = Smooth(0.5f, 0.78f, Fbm(u, v, 4, 4, s + 8)) * p.DirtAmount;
            c = Mul(c, 1f - 0.25f * dent - 0.45f * dirt);

            color = c;
            height = 0.25f + 0.5f * rib - wear * 0.12f + TileableNoise.Perlin(u * 90f, v * 90f, 90, s + 9) * 0.03f * wear;
        }

        // ---------------------------------------------------------------- Caja de madera
        // Textura de UNA cara completa (no se repite): marco, tablas, riostra diagonal, tornillos, marcas y desgaste.
        private static void CrateWood(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            int s = p.Seed + 500;
            const float b = 0.13f; // ancho del marco
            bool inFrame = u < b || u > 1f - b || v < b || v > 1f - b;

            float h;
            Rgb wood;
            float gap = 0f;
            if (inFrame)
            {
                bool horizontal = v < b || v > 1f - b;
                float grain = 0.5f + 0.5f * (horizontal
                    ? TileableNoise.Perlin(u * 3f, v * 40f, 3, 40, s + 1)
                    : TileableNoise.Perlin(u * 40f, v * 3f, 40, 3, s + 1));
                float id = TileableNoise.Hash01((int)(u * 5f), (int)(v * 5f), s + 2);
                wood = Rgb.Lerp(p.WoodA, p.WoodB, 0.35f + 0.4f * id);
                wood = Mul(wood, 0.8f + 0.3f * grain);
                h = 0.65f + 0.1f * grain;
            }
            else
            {
                float x = (u - b) / (1f - 2f * b) * 5f;
                int plank = (int)Math.Floor(x);
                float pf = x - plank;
                gap = pf < 0.05f || pf > 0.95f ? 1f : 0f;
                float grain = 0.5f + 0.5f * TileableNoise.Perlin(u * 30f, v * 3f, 30, 3, s + 3);
                float id = TileableNoise.Hash01(plank, 3, s + 4);
                wood = Rgb.Lerp(p.WoodA, p.WoodB, 0.2f + 0.6f * id);
                wood = Mul(wood, 0.75f + 0.35f * grain);
                h = 0.5f + 0.08f * grain;

                // Riostra diagonal
                float dBrace = Math.Abs(u - v) * 0.7071f;
                if (dBrace < 0.045f)
                {
                    wood = Mul(Rgb.Lerp(p.WoodA, p.WoodB, 0.6f), 0.85f + 0.3f * grain);
                    h = 0.72f;
                    if (dBrace > 0.04f) wood = Mul(wood, 0.55f); // canto sombreado
                }
                if (gap > 0f) { wood = Mul(wood, 0.35f); h = 0.2f; }
            }

            // Nudos
            TileableNoise.Voronoi(u, v, 6, s + 5, out float f1, out _, out float cid);
            if (cid > 0.65f) wood = Mul(wood, 1f - 0.5f * (1f - Smooth(0.05f, 0.2f, f1)));

            // Tornillos en esquinas y puntas de la riostra
            float bolt = BoltMask(u, v);
            wood = Rgb.Lerp(wood, new Rgb(0.2f, 0.2f, 0.21f), bolt);
            h += bolt * 0.25f;

            // Marcas estarcidas (pintura descolorida) en el panel central
            float mark = StencilMask(u, v, s) * (0.55f + 0.3f * Fbm(u, v, 24, 2, s + 6));
            wood = Rgb.Lerp(wood, new Rgb(0.72f, 0.7f, 0.62f), mark * 0.55f);

            // Bordes gastados (más claros por el roce) y manchas
            float e = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float worn = (1f - Smooth(0f, 0.035f, e)) * Smooth(0.35f, 0.7f, Fbm(u, v, 18, 3, s + 7));
            wood = Mul(wood, 1f + 0.35f * worn);
            float stain = Smooth(0.52f, 0.78f, Fbm(u, v, 3, 4, s + 8)) * p.DirtAmount;
            wood = Mul(wood, 1f - 0.5f * stain);

            color = wood;
            height = h - worn * 0.05f;
        }

        // ---------------------------------------------------------------- Caja de cartón
        private static void Cardboard(float u, float v, SurfaceParams p, out Rgb color, out float height)
        {
            int s = p.Seed + 600;
            float macro = Fbm(u, v, 4, 4, s + 1);
            float flute = 0.5f + 0.5f * TileableNoise.Perlin(u * 2f, v * 150f, 2, 150, s + 2); // ondulado del cartón
            Rgb c = Mul(p.CardboardColor, 0.82f + 0.3f * macro);
            c = Mul(c, 0.92f + 0.12f * flute);

            // Solapa central (línea oscura) y cinta adhesiva vertical
            float flap = 1f - Smooth(0f, 0.012f, Math.Abs(v - 0.5f));
            c = Mul(c, 1f - 0.45f * flap);
            float tapeD = Math.Abs(u - 0.5f);
            float tape = 1f - Smooth(0.05f, 0.058f, tapeD);
            Rgb tapeColor = Mul(new Rgb(0.56f, 0.5f, 0.32f), 0.9f + 0.2f * Fbm(u, v, 20, 2, s + 3));
            c = Rgb.Lerp(c, tapeColor, tape * 0.85f);
            float tapeEdge = Smooth(0.045f, 0.05f, tapeD) * (1f - Smooth(0.056f, 0.062f, tapeD));
            c = Mul(c, 1f - 0.25f * tapeEdge);

            // Etiqueta blanca con rayas y flechas "este lado arriba"
            if (u > 0.64f && u < 0.9f && v > 0.14f && v < 0.36f)
            {
                float line = Frac(v * 30f) < 0.35f && v > 0.2f ? 0.6f : 1f;
                c = Mul(new Rgb(0.7f, 0.68f, 0.6f), line * (0.85f + 0.2f * Fbm(u, v, 16, 2, s + 4)));
            }
            float arrow = ArrowMask(u, v);
            c = Rgb.Lerp(c, new Rgb(0.12f, 0.1f, 0.09f), arrow * 0.85f);

            // Manchas de agua, esquinas aplastadas y rasgaduras
            float water = Smooth(0.55f, 0.75f, Fbm(u, v, 3, 4, s + 5)) * p.DirtAmount;
            c = Mul(c, 1f - 0.4f * water);
            float e = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float crush = (1f - Smooth(0f, 0.06f, e)) * (0.5f + 0.5f * Fbm(u, v, 10, 3, s + 6));
            c = Mul(c, 1f - 0.3f * crush);

            color = c;
            height = 0.5f + 0.06f * flute + tape * 0.08f - flap * 0.1f - crush * 0.1f;
        }

        private static float BoltMask(float u, float v)
        {
            float m = 0f;
            const float r = 0.018f;
            float[] xs = { 0.065f, 0.935f, 0.5f };
            float[] ys = { 0.065f, 0.935f, 0.5f };
            for (int i = 0; i < xs.Length; i++)
            {
                for (int j = 0; j < ys.Length; j++)
                {
                    if (i == 2 && j == 2) continue;
                    float dx = u - xs[i], dy = v - ys[j];
                    m = Math.Max(m, 1f - Smooth(r * 0.7f, r, (float)Math.Sqrt(dx * dx + dy * dy)));
                }
            }
            return m;
        }

        private static float StencilMask(float u, float v, int seed)
        {
            // Dos "palabras" hechas con bloques, en la parte alta del panel
            if (v < 0.7f || v > 0.8f || u < 0.26f || u > 0.74f) return 0f;
            int cell = (int)(u * 26f);
            return TileableNoise.Hash01(cell, 1, seed + 9) > 0.35f && Frac(u * 26f) < 0.7f ? 1f : 0f;
        }

        private static float ArrowMask(float u, float v)
        {
            // Dos flechas hacia arriba en la zona izquierda
            float m = 0f;
            for (int i = 0; i < 2; i++)
            {
                float cx = 0.16f + i * 0.14f;
                float shaft = Math.Abs(u - cx) < 0.012f && v > 0.14f && v < 0.3f ? 1f : 0f;
                float head = v >= 0.3f && v < 0.38f && Math.Abs(u - cx) < (0.38f - v) * 0.55f ? 1f : 0f;
                m = Math.Max(m, Math.Max(shaft, head));
            }
            return m;
        }
    }
}
