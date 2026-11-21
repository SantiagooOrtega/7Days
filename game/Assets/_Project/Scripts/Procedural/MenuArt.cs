using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Arte procedural del menú y de las pantallas (puro, propio): fondo de pintura roja y naranja desgastada con grunge, ciudad en ruinas ardiendo abajo
    /// (edificios rotos, grúas), calavera propia agrietada a baja opacidad en la pared (la del casco de la Calavera, estilizada), viñeta; marco de panel
    /// desgastado (esquinas rotas, rayones, óxido), resaltado de fila, grano, humo y silueta del soldado. Todo RGBA32 de abajo arriba (como Texture2D).
    /// </summary>
    public static class MenuArt
    {
        public const float SkullOpacity = 0.32f;

        private static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1442695041; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF; } }
        private static float Noise(float x, float y, int s)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
        public static float Fbm(float x, float y, int s, int oct = 4) { float v = 0, a = 0.5f, f = 1; for (int i = 0; i < oct; i++) { v += a * Noise(x * f, y * f, s + i * 31); a *= 0.5f; f *= 2f; } return v; }
        private static float C01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        private static byte B(float v) => (byte)(C01(v) * 255f + 0.5f);

        /// <summary>Distancia con signo de la calavera propia (coordenadas -1..1, y hacia arriba): casco redondo, cuencas angulosas, nariz triangular y mandíbula con placas.</summary>
        public static float SkullSd(float u, float v)
        {
            float cranium = (float)Math.Sqrt(u * u / 0.64f + (v - 0.18f) * (v - 0.18f) / 0.56f) - 1f;
            float jaw = Math.Max(Math.Abs(u) - 0.46f + (v + 0.55f) * 0.25f, Math.Abs(v + 0.52f) - 0.28f);
            float head = Math.Min(cranium * 0.6f, jaw);
            float eye(float cx) { float x = (u - cx) * (cx > 0 ? 1 : -1); return Math.Max(Math.Max(-(v + 0.02f) + x * 0.35f, v - 0.28f), Math.Abs(u - cx) - 0.2f); }
            float eyes = Math.Min(eye(0.3f), eye(-0.3f));
            float nose = Math.Max(Math.Abs(u) - (v + 0.32f) * 0.45f, Math.Max(-(v + 0.32f), v + 0.08f));
            float teeth = (v < -0.42f && v > -0.62f && Math.Abs(u) < 0.36f && Math.Abs((u + 0.5f) * 8f % 1f - 0.5f) < 0.08f) ? -1f : 1f;
            return Math.Max(head, -Math.Min(Math.Min(eyes, nose), teeth < 0 ? -0.01f : 1f));
        }

        /// <summary>Fondo del menú (w x h): pintura roja/naranja con grunge, calavera grande, ciudad ardiendo abajo y viñeta.</summary>
        public static byte[] Backdrop(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            // horizonte de la ciudad: alturas por columnas (edificios rotos y dos grúas)
            var sky = new float[w];
            var rng = new Random(seed);
            int x0 = 0;
            while (x0 < w)
            {
                int bw = 6 + rng.Next(w / 18 + 1); float bh = 0.08f + (float)rng.NextDouble() * 0.2f; bool broken = rng.NextDouble() < 0.5;
                for (int x = x0; x < Math.Min(w, x0 + bw); x++) sky[x] = bh - (broken ? (float)Math.Abs(Math.Sin((x - x0) * 0.9f)) * 0.05f * (float)rng.NextDouble() : 0f);
                x0 += bw + rng.Next(3);
            }
            foreach (float cx in new[] { 0.72f, 0.88f })
            {
                int mast = (int)(cx * w); float top = 0.46f;
                for (int x = mast - 1; x <= mast + 1 && x < w; x++) if (x >= 0) sky[x] = Math.Max(sky[x], top);
                for (int x = mast - w / 10; x < mast + w / 6 && x < w; x++) if (x >= 0 && ((x * 7) % 9 != 0)) sky[x] = Math.Max(sky[x], 0f); // el brazo se dibuja aparte
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    // pintura: rojo sangre arriba, naranja fuego abajo, manchas y desgaste
                    float paint = Fbm(u * 7f, v * 5f, seed, 5), streak = Fbm(u * 2f, v * 26f, seed + 9, 3);
                    float r = 0.42f + 0.35f * (1f - v) + 0.2f * paint, g = 0.05f + 0.22f * (1f - v) * (1f - v) + 0.08f * paint, b = 0.02f + 0.04f * paint;
                    float wear = C01((Fbm(u * 18f, v * 14f, seed + 3, 3) - 0.58f) * 4f);
                    r *= 1f - 0.55f * wear; g *= 1f - 0.55f * wear; b *= 1f - 0.4f * wear;
                    r *= 0.85f + 0.15f * streak; g *= 0.85f + 0.15f * streak;
                    // calavera grande a baja opacidad (agrietada)
                    float su = (u - 0.6f) / 0.24f, sv = (v - 0.52f) / 0.36f;
                    float sd = SkullSd(su, sv);
                    float crack = Math.Abs(Fbm(su * 3f + 5f, sv * 3f, seed + 21, 3) - 0.5f) < 0.018f ? 1f : 0f;
                    if (sd < 0f) { float k = SkullOpacity * (1f - 0.6f * crack); r = r * (1f - k) + 0.12f * k; g = g * (1f - k) + 0.02f * k; b *= 1f - k; }
                    // ciudad en ruinas ardiendo: silueta negra con ventanas de fuego y resplandor
                    float glow = (float)Math.Exp(-v * 9f) * (0.6f + 0.4f * Fbm(u * 12f, 0f, seed + 5, 2));
                    r += glow * 0.9f; g += glow * 0.45f; b += glow * 0.05f;
                    bool crane = false;
                    foreach (float cx in new[] { 0.72f, 0.88f }) { if (Math.Abs(v - 0.44f) < 0.006f && u > cx - 0.08f && u < cx + 0.14f) crane = true; if (Math.Abs(u - cx - 0.12f) < 0.0015f && v < 0.44f && v > 0.32f) crane = true; }
                    if (v < sky[x] || crane)
                    {
                        float fire = (v < sky[x] * 0.6f && Hash(x / 3, y / 3, seed) < 0.06f) ? 1f : 0f;
                        r = 0.03f + fire * 1f; g = 0.015f + fire * 0.5f; b = 0.01f + fire * 0.08f;
                    }
                    // viñeta fuerte
                    float dx = u - 0.5f, dy = v - 0.5f, vig = C01(1f - (dx * dx * 2.2f + dy * dy * 2.6f));
                    vig = 0.25f + 0.75f * vig;
                    int o = (y * w + x) * 4;
                    px[o] = B(r * vig); px[o + 1] = B(g * vig); px[o + 2] = B(b * vig); px[o + 3] = 255;
                }
            return px;
        }

        /// <summary>Marco de panel (n x n, para 9-slice con borde "border"): fondo oscuro semitransparente, línea crema fina, esquinas rotas, rayones y óxido.</summary>
        public static byte[] Frame(int n, int border, int seed)
        {
            var px = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int ex = Math.Min(x, n - 1 - x), ey = Math.Min(y, n - 1 - y), e = Math.Min(ex, ey);
                    float a = 0.78f, r = 0.05f, g = 0.045f, b = 0.04f;
                    bool line = e == 2 || e == 3;
                    bool chipped = ex + ey < 5;                                   // esquina rota
                    float rust = Fbm(x * 0.3f, y * 0.3f, seed, 3);
                    if (line && !chipped) { float worn = Hash(x, y, seed) < 0.18f ? 0.4f : 1f; r = 0.8f * worn; g = 0.74f * worn; b = 0.6f * worn; a = 0.95f; }
                    else if (e < 2 || chipped) a = 0f;
                    if (!line && e >= 2 && !chipped && e < border && rust > 0.62f) { r += 0.18f; g += 0.06f; a = Math.Max(a, 0.85f); }
                    if (!line && Hash(x * 3, y, seed + 4) < 0.012f) { r += 0.15f; g += 0.14f; b += 0.12f; }   // rayones
                    int o = (y * n + x) * 4;
                    px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a);
                }
            return px;
        }

        /// <summary>Resaltado de la fila activa: rojo a naranja de izquierda a derecha, con borde gastado.</summary>
        public static byte[] Highlight(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), n = Fbm(x * 0.2f, y * 0.4f, seed, 2);
                    float a = C01(0.95f - u * 0.55f) * (0.75f + 0.25f * n);
                    int o = (y * w + x) * 4;
                    px[o] = B(0.78f + 0.2f * u); px[o + 1] = B(0.12f + 0.3f * u); px[o + 2] = B(0.06f); px[o + 3] = B(a);
                }
            return px;
        }

        /// <summary>Grano (ruido blanco con alfa bajo) en mosaico.</summary>
        public static byte[] Grain(int n, int seed)
        {
            var px = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++) { float v = Hash(i % n, i / n, seed); int o = i * 4; px[o] = px[o + 1] = px[o + 2] = B(v); px[o + 3] = B(0.08f); }
            return px;
        }

        /// <summary>Humo en mosaico (alfa de un fbm suave, más denso abajo).</summary>
        public static byte[] Smoke(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = Fbm(u * 4f, v * 3f, seed, 4);
                float a = C01((n - 0.42f) * 2.2f) * C01(1.1f - v) * 0.55f;
                int o = (y * w + x) * 4; px[o] = B(0.12f); px[o + 1] = B(0.09f); px[o + 2] = B(0.08f); px[o + 3] = B(a);
            }
            return px;
        }

        /// <summary>Silueta del soldado con casco de calavera (de pie, arma al hombro), en crema desgastado. Para la vista previa y la pantalla de muerte.</summary>
        public static byte[] Soldier(int w, int h, float[] tint)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float head = (float)Math.Sqrt(u * u / 0.05f + (v - 0.72f) * (v - 0.72f) / 0.03f) - 1f;
                    float torso = Math.Max(Math.Abs(u) - 0.3f + (v - 0.2f) * 0.12f, Math.Abs(v - 0.28f) - 0.33f);
                    float legs = Math.Max(Math.Abs(Math.Abs(u) - 0.12f) - 0.1f, Math.Abs(v + 0.45f) - 0.42f);
                    float arms = Math.Max(Math.Abs(Math.Abs(u) - 0.36f) - 0.08f, Math.Abs(v - 0.18f) - 0.27f);
                    float gun = Math.Max(Math.Abs(u - 0.2f + (v - 0.3f) * 0.9f) - 0.05f, Math.Abs(v - 0.3f) - 0.38f);
                    float sd = Math.Min(Math.Min(Math.Min(head * 0.2f, torso), Math.Min(legs, arms)), gun);
                    float a = C01(0.5f - sd * w * 0.25f);
                    float visor = (Math.Abs(v - 0.71f) < 0.025f && Math.Abs(u) < 0.13f) ? 1f : 0f;   // ranura del casco
                    int o = (y * w + x) * 4;
                    px[o] = B(tint[0] * (1f - visor * 0.7f)); px[o + 1] = B(tint[1] * (1f - visor * 0.8f)); px[o + 2] = B(tint[2] * (1f - visor)); px[o + 3] = B(a * 0.92f);
                }
            return px;
        }

        /// <summary>Vista previa cenital de un mapa (n x n): suelo, muros del perímetro, props como bloques, casa en naranja y spawns en rojo.</summary>
        public static byte[] MapPreview(MapData m, int n)
        {
            var px = new byte[n * n * 4];
            float span = m.halfSize * 2f + 4f;
            for (int i = 0; i < n * n; i++) { int o = i * 4; float g = 0.12f + 0.05f * Fbm((i % n) * 0.1f, (i / n) * 0.1f, 3, 2); px[o] = B(g); px[o + 1] = B(g * 0.95f); px[o + 2] = B(g * 0.85f); px[o + 3] = 230; }
            void Rect(float cx, float cz, float sx, float sz, float r, float g, float b)
            {
                int x0 = (int)((cx - sx / 2f + span / 2f) / span * n), x1 = (int)((cx + sx / 2f + span / 2f) / span * n);
                int y0 = (int)((cz - sz / 2f + span / 2f) / span * n), y1 = (int)((cz + sz / 2f + span / 2f) / span * n);
                for (int y = Math.Max(0, y0); y <= Math.Min(n - 1, y1); y++) for (int x = Math.Max(0, x0); x <= Math.Min(n - 1, x1); x++) { int o = (y * n + x) * 4; px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); }
            }
            foreach (var p in m.props)
            {
                if (p.kind == "ground" || p.kind == "soil" || p.kind == "rubble_field" || p.kind == "road" || p.kind == "trail") continue;
                bool edge = p.kind == "wall" || p.kind == "boundary";
                Rect(p.pos[0], p.pos[2], Math.Max(0.6f, p.size[0]), Math.Max(0.6f, p.size[2]), edge ? 0.7f : 0.4f, edge ? 0.62f : 0.38f, edge ? 0.5f : 0.34f);
            }
            if (m.vegetation?.items != null) foreach (var v in m.vegetation.items) if (v.type.StartsWith("tree") || v.type == "pine") Rect(v.x, v.z, 0.8f, 0.8f, 0.12f, 0.24f, 0.12f);
            foreach (var h in m.houses) Rect(h.x, h.z, h.width, h.depth, 0.95f, 0.5f, 0.15f);
            foreach (var s in m.spawns) Rect(s.x, s.z, 1.2f, 1.2f, 0.9f, 0.15f, 0.1f);
            return px;
        }
    }
}
