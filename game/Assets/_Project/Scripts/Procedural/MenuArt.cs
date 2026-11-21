using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Arte procedural del menú y de las pantallas (puro, propio): fondo de pintura roja y naranja desgastada con grunge, ciudad en ruinas ardiendo abajo
    /// (edificios rotos, grúas), viñeta (la calavera roja va aparte: DetailedSkull); marco de panel
    /// desgastado (esquinas rotas, rayones, óxido), resaltado de fila, grano, humo y silueta del soldado. Todo RGBA32 de abajo arriba (como Texture2D).
    /// </summary>
    public static class MenuArt
    {
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

        // paleta del menú (valores de la descripción acordada)
        public static readonly float[] WallDark = { 0.557f, 0.106f, 0.071f }, WallBright = { 0.702f, 0.149f, 0.102f }, Maroon = { 0.557f, 0.106f, 0.071f };

        private static float Smooth(float a, float b, float x) { float t = C01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        /// <summary>El "7" estarcido en la frente de la calavera (coordenadas de la calavera): barra arriba y trazo en diagonal.</summary>
        public static bool Seven(float u, float v)
        {
            if (v > 0.5f && v < 0.6f && u > -0.17f && u < 0.17f) return true;
            float t = (0.55f - v) / 0.38f; if (t < 0f || t > 1f) return false;
            float cx = 0.15f - t * 0.2f;
            return Math.Abs(u - cx) < 0.055f;
        }

        // paleta del fondo
        public static readonly float[] SkyTop = { 0x0A / 255f, 0x08 / 255f, 0x08 / 255f }, SkyMid = { 0x3A / 255f, 0x0A / 255f, 0x07 / 255f }, SkyLow = { 0x7A / 255f, 0x14 / 255f, 0x0C / 255f };
        public static readonly float[][] CityColor = { new[] { 0x1A / 255f, 0x06 / 255f, 0x05 / 255f }, new[] { 0x10 / 255f, 0x04 / 255f, 0x03 / 255f }, new[] { 0x08 / 255f, 0x02 / 255f, 0x02 / 255f } };
        public const float GrainAmount = 0.04f;

        /// <summary>Color del cielo a la altura v (0 abajo, 1 arriba): #7A140C abajo, #3A0A07 en medio, #0A0808 arriba.</summary>
        public static void Sky(float v, out float r, out float g, out float b)
        {
            float[] lo, hi; float t;
            if (v < 0.5f) { lo = SkyLow; hi = SkyMid; t = Smooth(0f, 0.5f, v); } else { lo = SkyMid; hi = SkyTop; t = Smooth(0.5f, 1f, v); }
            r = lo[0] + (hi[0] - lo[0]) * t; g = lo[1] + (hi[1] - lo[1]) * t; b = lo[2] + (hi[2] - lo[2]) * t;
        }

        /// <summary>
        /// Fondo del menú (w x h): degradado vertical, humo denso tenue en el cielo, viñeta suave y grano del 4 %. Sin rejilla. La ciudad, las llamas, el humo
        /// animado y la calavera van en capas aparte.
        /// </summary>
        public static byte[] Backdrop(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    Sky(v, out float r, out float g, out float b);
                    // nubes de humo: más oscuras arriba, teñidas de rojo abajo
                    float sm = Fbm(u * 3.2f, v * 2.2f + 0.3f * Fbm(u * 2f, v * 2f, seed + 1, 2), seed, 5);
                    float dark = C01((sm - 0.45f) * 1.6f) * 0.45f;
                    r *= 1f - dark; g *= 1f - dark; b *= 1f - dark;
                    // resplandor naranja desde abajo
                    float glow = (float)Math.Exp(-v * 5f) * 0.35f;
                    r += glow * 1f; g += glow * 0.32f; b += glow * 0.05f;
                    // viñeta suave
                    float dx = u - 0.5f, dy = v - 0.5f, vig = 1f - 0.55f * C01((dx * dx * 1.6f + dy * dy * 2.2f) * 1.6f);
                    float gr = 1f + GrainAmount * (Hash(x, y, seed + 11) * 2f - 1f);
                    int o = (y * w + x) * 4;
                    px[o] = B(r * vig * gr); px[o + 1] = B(g * vig * gr); px[o + 2] = B(b * vig * gr); px[o + 3] = 255;
                }
            return px;
        }

        /// <summary>Perfil de la ciudad de una capa (0 lejana, 1 media, 2 cercana): altura (0..1 del alto de la textura) por columna. Edificios rotos, torres, grúas.</summary>
        public static float[] Skyline(int w, int layer, int seed)
        {
            var hgt = new float[w]; var rng = new Random(seed + layer * 101);
            float baseH = layer == 0 ? 0.55f : (layer == 1 ? 0.42f : 0.2f), var = layer == 0 ? 0.35f : (layer == 1 ? 0.4f : 0.22f);
            int x0 = 0;
            while (x0 < w)
            {
                int bw = (int)(w * (layer == 2 ? 0.04f : 0.025f)) + rng.Next((int)(w * 0.04f) + 1);
                float top = baseH + var * (float)rng.NextDouble();
                bool broken = rng.NextDouble() < 0.45, spire = rng.NextDouble() < 0.15;
                for (int x = x0; x < Math.Min(w, x0 + bw); x++)
                {
                    float t = (x - x0) / (float)bw, ht = top;
                    if (broken) ht -= 0.12f * C01((t - 0.4f) * 2f) + 0.03f * (Hash(x / 3, layer, seed) - 0.5f);   // techo derrumbado en diagonal
                    if (spire && Math.Abs(t - 0.5f) < 0.06f) ht += 0.12f;
                    hgt[x] = Math.Max(hgt[x], ht);
                }
                x0 += bw + (layer == 2 ? rng.Next(w / 30 + 1) : rng.Next(4));
            }
            // grúa en la capa media
            if (layer == 1) { int cx = w * 2 / 3; for (int x = cx; x < Math.Min(w, cx + w / 9); x++) hgt[x] = Math.Max(hgt[x], x - cx < 4 ? 0.95f : (0.9f + (Hash(x / 6, 9, seed) < 0.5f ? 0.03f : 0f))); }
            if (layer == 2) for (int x = 0; x < w; x++) hgt[x] += 0.05f * Fbm(x * 0.02f, 0.5f, seed + 5, 3);   // escombros irregulares
            return hgt;
        }

        /// <summary>Capa de ciudad (w x h): silueta del color de su capa; la cercana lleva un filo de luz naranja del fuego arriba. Alfa 1 dentro.</summary>
        public static byte[] CityLayer(int w, int h, int layer, int seed)
        {
            var px = new byte[w * h * 4]; var sk = Skyline(w, layer, seed); var c = CityColor[layer];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)h, top = sk[x];
                    if (v > top) continue;
                    float edge = C01(1f - (top - v) * h / 6f) * (layer == 2 ? 0.9f : (layer == 1 ? 0.35f : 0.15f));   // luz del fuego en el borde de arriba
                    float lift = (1f - v / Math.Max(top, 1e-3f)) * (layer == 0 ? 0.25f : 0.12f);                     // el fuego aclara la base
                    float r = c[0] + edge * 1f + lift * 0.4f, g = c[1] + edge * 0.42f + lift * 0.08f, b = c[2] + edge * 0.08f;
                    int o = (y * w + x) * 4; px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = 255;
                }
            return px;
        }

        /// <summary>Posiciones (u, v de la textura de la capa media) de "count" ventanas encendidas, siempre dentro de un edificio y por debajo de su techo.</summary>
        public static float[] CityWindows(int w, int seed, int count)
        {
            var sk = Skyline(w, 1, seed); var res = new float[count * 2]; var rng = new Random(seed + 77); int n = 0, guard = 0;
            while (n < count && guard++ < 10000)
            {
                int x = rng.Next(w); float top = sk[x];
                if (top < 0.2f || x < 3 || x > w - 4 || Math.Min(sk[x - 3], sk[x + 3]) < top - 0.02f) continue;
                float v = 0.08f + (float)rng.NextDouble() * (top - 0.16f);
                bool clash = false; for (int k = 0; k < n; k++) if (Math.Abs(res[k * 2] - x / (float)w) < 0.012f && Math.Abs(res[k * 2 + 1] - v) < 0.05f) clash = true;
                if (clash) continue;
                res[n * 2] = x / (float)w; res[n * 2 + 1] = v; n++;
            }
            return res;
        }

        /// <summary>Columna de humo negro (w x h), periódica en vertical para desplazarla hacia arriba: más ancha arriba, bordes deshilachados.</summary>
        public static byte[] SmokeColumn(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w * 2f - 1f, v = y / (float)h;
                    float n = FbmP(u * 3f + 10f, v * 4f, seed, 64, 4, 4);
                    float width = 0.55f + 0.35f * (float)Math.Sin(v * Math.PI * 2f) * 0.3f;
                    float a = C01((width - Math.Abs(u + (n - 0.5f) * 0.8f)) * 2.2f) * C01((n - 0.25f) * 2f) * 0.85f;
                    int o = (y * w + x) * 4; px[o] = B(0.03f); px[o + 1] = B(0.02f); px[o + 2] = B(0.02f); px[o + 3] = B(a);
                }
            return px;
        }

        // ruido periódico (se repite cada px x py celdas): base de las llamas animadas en bucle perfecto
        private static float NoiseP(float x, float y, int s, int px, int py)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = ((xi % px) + px) % px, x1 = (x0 + 1) % px, y0 = ((yi % py) + py) % py, y1 = (y0 + 1) % py;
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
        private static float FbmP(float x, float y, int s, int px, int py, int oct)
        { float v = 0, a = 0.5f, f = 1; for (int i = 0; i < oct; i++) { v += a * NoiseP(x * f, y * f, s + i * 31, px * (int)f, py * (int)f); a *= 0.5f; f *= 2f; } return v; }

        public const int FlameFrames = 32, FlameCols = 8, FlameW = 256, FlameH = 128;

        public const int Tongues = 9;

        /// <summary>
        /// Franja de llamas (u 0..1 se repite en horizontal, v 0 abajo..1 arriba, fase 0..1 se repite en el tiempo) → RGBA 0..1. Lenguas irregulares: cada una
        /// con su altura, ancho, ondulación y fase (y 1 o 2 latidos por vuelta), con ruido que sube para romper los bordes; degradado blanco-amarillo-naranja-rojo.
        /// La fase 1 es idéntica a la 0 (bucle sin salto).
        /// </summary>
        public static void Flame(float u, float v, float phase, int seed, out float r, out float g, out float b, out float a)
        {
            const float TwoPi = (float)(Math.PI * 2);
            const int PX = 10, PY = 8;
            float n = FbmP(u * PX, v * 2.2f - phase * PY, seed + 1, PX, PY, 4);
            float wob = (FbmP(u * PX * 2f, v * 4f - phase * PY * 2f, seed + 6, PX * 2, PY * 2, 3) - 0.5f) * (0.7f / Tongues);   // bordes rotos que suben
            float heat = 0f; int i0 = (int)Math.Floor(u * Tongues);
            for (int d = -1; d <= 1; d++)
            {
                int k = ((i0 + d) % Tongues + Tongues) % Tongues;
                float c = (k + 0.5f + 0.5f * (Hash(k, 0, seed) - 0.5f)) / Tongues;
                int m = 1 + (int)(Hash(k, 3, seed) * 2f);   // latidos por vuelta (entero: bucle perfecto)
                float ph = TwoPi * (m * phase + Hash(k, 2, seed));
                float h = (0.45f + 0.5f * Hash(k, 1, seed)) * (0.82f + 0.18f * (float)Math.Sin(ph));
                if (v >= h) continue;
                float t = v / h;
                float dx = u - c - (0.3f / Tongues) * t * (float)Math.Sin(ph * 1f + v * 7f) + wob * t;
                dx -= (float)Math.Round(dx);
                float half = (1.05f / Tongues) * (0.55f + 0.45f * Hash(k, 4, seed)) * (float)Math.Pow(1f - t, 0.75f);
                float mask = C01(1f - Math.Abs(dx) / Math.Max(half, 1e-4f));
                heat = Math.Max(heat, (float)Math.Pow(mask, 0.6f) * (float)Math.Pow(1f - t, 0.45f));
            }
            heat *= 0.4f + 0.75f * n;
            heat += C01(0.14f - v) * 2.2f * (0.5f + 0.5f * n);   // base continua de brasas
            heat = C01(heat);
            if (heat < 0.35f) { float q = heat / 0.35f; r = 0.55f + 0.45f * q; g = 0.05f + 0.35f * q; b = 0.02f; }
            else if (heat < 0.62f) { float q = (heat - 0.35f) / 0.27f; r = 1f; g = 0.4f + 0.4f * q; b = 0.03f + 0.2f * q; }
            else { float q = C01((heat - 0.62f) / 0.38f); r = 1f; g = 0.8f + 0.15f * q; b = 0.23f + 0.45f * q * q; }
            a = C01(heat * 2.8f);
        }

        /// <summary>Atlas de la animación de llamas: FlameFrames fotogramas de FlameW x FlameH en FlameCols columnas (RGBA32, de abajo arriba). Se genera una vez.</summary>
        public static byte[] FlameAtlas(int seed)
        {
            int rows = FlameFrames / FlameCols, W = FlameW * FlameCols, H = FlameH * rows;
            var px = new byte[W * H * 4];
            for (int f = 0; f < FlameFrames; f++)
            {
                int ox = (f % FlameCols) * FlameW, oy = (rows - 1 - f / FlameCols) * FlameH;   // fotograma 0 arriba a la izquierda
                float phase = f / (float)FlameFrames;
                for (int y = 0; y < FlameH; y++)
                    for (int x = 0; x < FlameW; x++)
                    {
                        Flame(x / (float)FlameW, y / (float)(FlameH - 1), phase, seed, out float r, out float g, out float b, out float a);
                        int o = ((oy + y) * W + ox + x) * 4;
                        px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a);
                    }
            }
            return px;
        }

        /// <summary>Calavera del emblema (coordenadas -1..1, y arriba): cráneo ancho, pómulos marcados, mandíbula estrecha, cuencas inclinadas amenazantes, nariz de corazón invertido y dientes separados.</summary>
        public static float BadgeSkullSd(float u, float v)
        {
            float au = Math.Abs(u);
            float cranium = (float)Math.Sqrt(u * u / 0.58f + (v - 0.2f) * (v - 0.2f) / 0.5f) - 1f;
            float cheek = (float)Math.Sqrt((au - 0.42f) * (au - 0.42f) / 0.06f + (v + 0.2f) * (v + 0.2f) / 0.05f) - 1f;
            float jaw = Math.Max(au - 0.36f + (v + 0.42f) * 0.35f, Math.Abs(v + 0.55f) - 0.24f);
            float head = SMin(SMin(cranium * 0.55f, cheek * 0.2f, 0.06f), jaw, 0.08f);
            // cuencas: elipses inclinadas hacia el centro (ceño)
            float ex = au - 0.3f, ey = v + 0.02f, cs = 0.94f, sn = 0.34f;
            float rx = ex * cs - ey * sn, ry = ex * sn + ey * cs;
            float eye = (float)Math.Sqrt(rx * rx / 0.04f + ry * ry / 0.026f) - 1f;
            eye = Math.Max(eye * 0.15f, (v - 0.04f - (au - 0.15f) * 0.45f) * 0.5f);   // ceño: el borde de arriba baja hacia la nariz
            float nose = Math.Max(au - (0.2f - (v + 0.2f)) * 0.42f, Math.Max(-(v + 0.36f), v + 0.16f));
            float row = (u + 0.5f) * 7f; float gap = Math.Abs(row - (float)Math.Floor(row) - 0.5f);
            float teeth = (v < -0.43f && v > -0.6f && au < 0.3f && gap > 0.42f) ? -1f : 1f;
            float mouth = (Math.Abs(v + 0.43f) < 0.018f && au < 0.3f) ? -1f : 1f;
            float holes = Math.Min(Math.Min(eye, nose * 0.6f), Math.Min(teeth, mouth) < 0 ? -0.01f : 1f);
            return Math.Max(head, -holes);
        }
        private static float SMin(float a, float b, float k) { float h = C01(0.5f + 0.5f * (b - a) / k); return b + (a - b) * h - k * h * (1 - h); }

        /// <summary>
        /// Emblema de la pantalla de inicio (n x n): insignia redonda de acero oscuro con remaches y bisel, campo negro con resplandor rojo, y la calavera propia
        /// en relieve (iluminada desde arriba a la izquierda, mugre y grietas), brasas en las cuencas y el "7" grabado en rojo en la frente. Fondo transparente.
        /// </summary>
        public static byte[] Emblem(int n, int seed)
        {
            var px = new byte[n * n * 4];
            const float Sk = 0.6f;   // escala de la calavera en la insignia
            float e = 2f / n;
            float Height(float su, float sv) { float d = BadgeSkullSd(su, sv); return Smooth(-0.0f, -0.16f, d) * (0.6f + 0.4f * C01(1f - (su * su + (sv - 0.1f) * (sv - 0.1f)) * 0.6f)); }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float rad = (float)Math.Sqrt(u * u + v * v), ang = (float)Math.Atan2(v, u);
                    float r, g, b, a = 1f - Smooth(0.975f, 0.995f, rad);
                    float grime = Fbm(u * 7f, v * 7f, seed + 3, 4);
                    if (rad > 0.84f)
                    {
                        // aro de acero: bisel (más claro arriba a la izquierda en el borde exterior, al revés en el interior)
                        float t = (rad - 0.84f) / 0.15f, lightDir = (-u * 0.6f + v * 0.8f) / Math.Max(rad, 1e-3f);
                        float bevel = t < 0.25f ? -lightDir * 0.5f : (t > 0.8f ? lightDir * 0.6f : 0.12f * lightDir);
                        float c = 0.3f + 0.22f * bevel + 0.12f * (grime - 0.5f) + 0.06f * (float)Math.Sin(ang * 90f + grime * 6f);
                        r = c; g = c * 0.97f; b = c * 0.95f;
                        // remaches
                        for (int k = 0; k < 12; k++)
                        {
                            float ra = k * (float)Math.PI / 6f + 0.26f, rxk = (float)Math.Cos(ra) * 0.915f, ryk = (float)Math.Sin(ra) * 0.915f;
                            float dd = (float)Math.Sqrt((u - rxk) * (u - rxk) + (v - ryk) * (v - ryk));
                            if (dd < 0.032f) { float sh = ((u - rxk) * -0.6f + (v - ryk) * 0.8f) / 0.032f; float rc = 0.42f + 0.35f * sh; r = rc; g = rc * 0.96f; b = rc * 0.92f; }
                        }
                        if (Hash(x, y, seed + 21) > 0.93f) { r *= 0.7f; g *= 0.7f; b *= 0.7f; }   // rayones
                    }
                    else if (rad > 0.8f) { r = 0.55f; g = 0.06f; b = 0.04f; float sh = 0.8f + 0.3f * (v - u) * 0.5f; r *= sh; }   // filete rojo
                    else
                    {
                        // campo negro con resplandor carmesí detrás de la calavera
                        float glow = (float)Math.Exp(-(u * u + (v - 0.05f) * (v - 0.05f)) * 2.6f);
                        r = 0.05f + 0.42f * glow; g = 0.02f + 0.04f * glow; b = 0.02f + 0.02f * glow;
                        float spk = Hash(x, y, seed + 9); if (spk > 0.85f) { r *= 0.6f; g *= 0.6f; b *= 0.6f; }
                        // sombra de la calavera desplazada abajo a la derecha
                        float sh = BadgeSkullSd((u - 0.035f) / Sk, (v + 0.045f) / Sk);
                        float shadow = (1f - Smooth(-0.02f, 0.12f, sh)) * 0.75f;
                        r *= 1f - shadow; g *= 1f - shadow; b *= 1f - shadow;
                        float su = u / Sk, sv = v / Sk;
                        float sd = BadgeSkullSd(su, sv), head = SkullHeadSdBadge(su, sv);
                        if (sd < 0f)
                        {
                            // relieve: normal a partir del gradiente de la altura
                            float h0 = Height(su, sv), hx = Height(su + e / Sk, sv) - h0, hy = Height(su, sv + e / Sk) - h0;
                            float nx = -hx * 30f, ny = -hy * 30f, nz = 1f, inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
                            nx *= inv; ny *= inv; nz *= inv;
                            float diff = C01(nx * -0.5f + ny * 0.6f + nz * 0.62f);
                            float spec = (float)Math.Pow(C01(nx * -0.3f + ny * 0.35f + nz * 0.89f), 24f) * 0.35f;
                            float bone = (0.3f + 0.85f * diff) * (0.78f + 0.3f * grime);
                            r = 0.9f * bone + spec; g = 0.84f * bone + spec; b = 0.7f * bone + spec * 0.9f;
                            // mugre en los bordes y grietas finas
                            float edge = Smooth(-0.07f, 0f, sd); r *= 1f - 0.5f * edge; g *= 1f - 0.55f * edge; b *= 1f - 0.6f * edge;
                            float cr = Fbm(su * 5f + 7f, sv * 5f, seed + 17, 4);
                            if (Math.Abs(cr - 0.5f) < 0.006f && sv > 0.15f && Fbm(su * 2f, sv * 2f, seed + 19, 2) > 0.5f) { r *= 0.35f; g *= 0.3f; b *= 0.3f; }
                            if (Seven(su * 1.05f, sv - 0.04f)) { r = 0.6f * (0.7f + 0.4f * diff); g = 0.05f; b = 0.03f; }
                        }
                        else if (head < 0f)
                        {
                            // cuencas, nariz y boca: negro profundo con brasas en los ojos
                            r = 0.03f; g = 0.015f; b = 0.01f;
                            float au = Math.Abs(su);
                            float ed = (float)Math.Sqrt((au - 0.29f) * (au - 0.29f) + (sv + 0.03f) * (sv + 0.03f));
                            float ember = (float)Math.Exp(-ed * ed * 90f) * (ed < 0.25f ? 1f : 0f);
                            r += 1f * ember; g += 0.42f * ember * ember + 0.05f * ember; b += 0.05f * ember * ember;
                        }
                    }
                    int o = (y * n + x) * 4;
                    px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a);
                }
            return px;
        }

        private static float SkullHeadSdBadge(float u, float v)
        {
            float au = Math.Abs(u);
            float cranium = (float)Math.Sqrt(u * u / 0.58f + (v - 0.2f) * (v - 0.2f) / 0.5f) - 1f;
            float cheek = (float)Math.Sqrt((au - 0.42f) * (au - 0.42f) / 0.06f + (v + 0.2f) * (v + 0.2f) / 0.05f) - 1f;
            float jaw = Math.Max(au - 0.36f + (v + 0.42f) * 0.35f, Math.Abs(v + 0.55f) - 0.24f);
            return SMin(SMin(cranium * 0.55f, cheek * 0.2f, 0.06f), jaw, 0.08f);
        }

        // ---------------------------------------------------------------- calavera roja del fondo del menú
        private static readonly float[][] SkullRamp = { new[] { 0x1A / 255f, 0x05 / 255f, 0x04 / 255f }, new[] { 0x8E / 255f, 0x1B / 255f, 0x12 / 255f }, new[] { 0xB3 / 255f, 0x26 / 255f, 0x1A / 255f }, new[] { 1f, 0x5A / 255f, 0x2A / 255f } };
        private static readonly float[] SkullRampAt = { 0f, 0.42f, 0.66f, 1f };
        public static readonly float[] RimOrange = { 1f, 0x7A / 255f, 0x1F / 255f };

        /// <summary>Mapa de degradado de la calavera: gris 0..1 → #1A0504 (sombras), #8E1B12 y #B3261A (medios), #FF5A2A (luces).</summary>
        public static void SkullGradient(float l, out float r, out float g, out float b)
        {
            l = C01(l); int i = 0; while (i < SkullRampAt.Length - 2 && l > SkullRampAt[i + 1]) i++;
            float t = (l - SkullRampAt[i]) / (SkullRampAt[i + 1] - SkullRampAt[i]);
            r = SkullRamp[i][0] + (SkullRamp[i + 1][0] - SkullRamp[i][0]) * t; g = SkullRamp[i][1] + (SkullRamp[i + 1][1] - SkullRamp[i][1]) * t; b = SkullRamp[i][2] + (SkullRamp[i + 1][2] - SkullRamp[i][2]) * t;
        }

        // ---------------------------------------------------------------- calavera detallada del menú (procedural, sin símbolos)
        public const int SkullSize = 1024;
        public const float SkullMinAlpha = 0.55f, SkullMaxAlpha = 0.65f, SkullBodyAlpha = 0.6f;

        private static float Ell(float u, float v, float cx, float cy, float rx, float ry) { float x = (u - cx) / rx, y = (v - cy) / ry; return ((float)Math.Sqrt(x * x + y * y) - 1f) * Math.Min(rx, ry); }
        private static float Gauss(float u, float v, float cx, float cy, float rx, float ry) { float x = (u - cx) / rx, y = (v - cy) / ry; return (float)Math.Exp(-(x * x + y * y)); }

        /// <summary>Silueta de la calavera (distancia con signo aprox., negativa dentro; u -1..1, v -1..1 arriba): cráneo, sienes hundidas, pómulos, maxilar y mandíbula.</summary>
        public static float SkullSilhouette(float u, float v)
        {
            float au = Math.Abs(u);
            float cranium = Ell(u, v, 0f, 0.3f, 0.7f, 0.64f);
            cranium = Math.Max(cranium, -Ell(au, v, 0.8f, -0.02f, 0.15f, 0.3f));                       // sienes
            float cheek = Ell(au, v, 0.5f, -0.12f, 0.2f, 0.1f);                                       // pómulos
            float maxW = 0.34f - C01((-v - 0.1f) / 0.42f) * 0.05f;
            float maxilla = Math.Max(au - maxW, Math.Max(v - (-0.05f), -0.515f - v));
            float t = C01((-0.53f - v) / 0.3f);                                                       // 0 arriba de la mandíbula, 1 en el mentón
            float jawW = 0.41f - 0.1f * t * t - 0.08f * t;
            float mandible = Math.Max(au - jawW, Math.Max(v + 0.525f, -0.8f - v));
            mandible = SMin(mandible, Ell(u, v, 0f, -0.8f, 0.24f, 0.075f), 0.08f);                   // mentón redondo
            float rx = 0.4f + (v + 0.62f) * 0.12f;
            float ramus = Math.Max(Math.Abs(au - rx) - 0.045f, Math.Max(v + 0.22f, -0.66f - v));     // rama inclinada hacia la articulación (detrás del pómulo)
            float face = SMin(SMin(cranium, cheek, 0.08f), maxilla, 0.06f);
            return SMin(face, SMin(mandible, ramus, 0.07f), 0.02f);
        }

        /// <summary>Profundidad de las cavidades 0..1 (cuencas, nariz, boca, separación de dientes y de la mandíbula articulada) y si es diente (1) o hueso (0).</summary>
        public static float SkullCavity(float u, float v, out float tooth)
        {
            float au = Math.Abs(u); tooth = 0f;
            // cuencas: cuadradas redondeadas, el lado de fuera más bajo
            float ex = (au - 0.27f) / 0.2f, ey = (v - 0.06f + (au - 0.27f) * 0.25f) / 0.165f;
            float se = (float)Math.Pow(Math.Pow(Math.Abs(ex), 2.6) + Math.Pow(Math.Abs(ey), 2.6), 1 / 2.6);
            float socket = C01((1f - se) * 4f) * (0.55f + 0.45f * C01(1f - se));
            // nariz: pera invertida con tabique
            float nt = C01((0.0f - v) / 0.3f), nhw = 0.03f + 0.065f * (float)Math.Sin(Math.Min(1f, nt * 1.15f) * Math.PI * 0.8f);
            float nose = (v < -0.01f && v > -0.31f) ? C01((nhw - au) / 0.025f) : 0f;
            if (au < 0.007f && v < -0.14f) nose *= 0.3f;
            // dientes de arriba y abajo, y la boca entre ellos
            float mouth = 0f;
            bool upper = v < -0.38f && v > -0.505f && au < 0.3f, lower = v < -0.525f && v > -0.63f && au < 0.27f;
            if (upper || lower)
            {
                float cols = upper ? 8f : 8f, w = upper ? 0.6f : 0.54f;
                float x = (u + w / 2f) / w * cols, f = x - (float)Math.Floor(x);
                float yy = upper ? (v + 0.505f) / 0.125f : (-0.525f - v) / 0.105f;   // 0 en el borde de mordida
                float edge = Math.Min(f, 1f - f);
                int idx = (int)Math.Floor(x); float len = 0.08f + 0.1f * Hash(idx, upper ? 1 : 2, 7);   // cada diente con su largo
                bool gap = edge < 0.06f || (yy < len && edge < 0.06f + (len - yy) * 1.6f);
                if (gap) mouth = 0.9f; else tooth = 0.4f + 0.6f * (float)Math.Sin(Math.PI * f);   // diente redondeado

            }
            else if (v <= -0.505f && v >= -0.525f && au < 0.3f) mouth = 1f;          // mordida
            else if (Math.Abs(v + 0.515f) < 0.012f && au < 0.44f) mouth = 0.7f;       // línea de la mandíbula articulada
            return Math.Max(Math.Max(socket, nose), mouth);
        }

        /// <summary>Altura del relieve (0 fuera): bisel del borde, cúpula del cráneo, arco de las cejas, pómulos, hueso nasal, sienes hundidas y cavidades.</summary>
        public static float SkullHeight(float u, float v, out float cavity, out float tooth)
        {
            float sd = SkullSilhouette(u, v);
            cavity = 0f; tooth = 0f;
            if (sd > 0f) return 0f;
            float au = Math.Abs(u);
            float bevel = Smooth(0f, 0.06f, -sd);
            float dome = (float)Math.Sqrt(C01(1f - (u * u / 0.55f + (v - 0.3f) * (v - 0.3f) / 0.5f)));
            float h = bevel * (0.45f + 0.35f * dome);
            h += 0.12f * Gauss(au, v, 0.27f, 0.25f, 0.2f, 0.05f);          // arco superciliar
            h += 0.14f * Gauss(au, v, 0.5f, -0.13f, 0.14f, 0.07f);         // pómulos
            h += 0.1f * Gauss(u, v, 0f, 0.07f, 0.05f, 0.09f);              // hueso nasal (triángulo entre las cuencas)
            h -= 0.14f * Gauss(au, v, 0.62f, 0.12f, 0.1f, 0.18f);          // sien
            h += 0.06f * Gauss(u, v, 0f, -0.84f, 0.2f, 0.06f);             // mentón
            cavity = SkullCavity(u, v, out tooth);
            h += 0.07f * tooth;
            return h - cavity * 0.4f;
        }

        // regueros de sangre: x, y de inicio, largo, ancho (bajo las cuencas, de los dientes y de la mandíbula)
        private static readonly float[] Drips =
        {
            -0.33f, -0.07f, 0.2f, 0.016f,  -0.25f, -0.09f, 0.33f, 0.02f,  -0.19f, -0.07f, 0.13f, 0.012f,
             0.21f, -0.08f, 0.26f, 0.018f,  0.29f, -0.09f, 0.4f, 0.022f,   0.36f, -0.06f, 0.15f, 0.013f,
            -0.17f, -0.5f, 0.12f, 0.014f,  -0.05f, -0.5f, 0.2f, 0.016f,   0.09f, -0.5f, 0.09f, 0.012f,  0.2f, -0.5f, 0.15f, 0.014f,
            -0.12f, -0.85f, 0.1f, 0.02f,   0.06f, -0.86f, 0.08f, 0.017f, 0.17f, -0.83f, 0.07f, 0.015f,
        };

        /// <summary>Sangre en un punto: 0..1 de cobertura y la "normal" lateral del reguero (-1..1, para el brillo húmedo).</summary>
        public static float Blood(float u, float v, out float side)
        {
            side = 0f; float best = 0f;
            for (int i = 0; i < Drips.Length; i += 4)
            {
                float x = Drips[i], y0 = Drips[i + 1], len = Drips[i + 2], w = Drips[i + 3];
                float t = (y0 - v) / len; if (t < -0.02f || t > 1.25f) continue;
                float wobble = 0.003f * (float)Math.Sin(v * 37f + i * 1.7f) + 0.002f * (float)Math.Sin(v * 91f + i);
                float ww = t > 1f ? 0f : w * (0.55f + 0.45f * (1f - t)) * 1.3f;
                float dx = u - x - wobble, cov = ww > 0f ? C01((ww - Math.Abs(dx)) / 0.004f) : 0f;
                float by = y0 - len, bulb = C01((w * 1.35f - (float)Math.Sqrt(dx * dx + (v - by) * (v - by) * 1.3f)) / 0.004f);   // gota al final
                float c = Math.Max(cov, bulb);
                if (c > best) { best = c; side = C01(dx / Math.Max(w * 1.3f, 1e-4f) * 0.5f + 0.5f) * 2f - 1f; }
            }
            return best;
        }

        /// <summary>
        /// Calavera detallada del menú (n x n, RGBA32 de abajo arriba): relieve iluminado desde arriba con resplandor de fuego desde abajo, cuencas profundas,
        /// nariz con tabique, pómulos, sienes, sutura craneal, mandíbula articulada con dientes uno a uno, grietas, manchas, ruido de hueso; teñida rojo sangre
        /// (#1A0504 → #8E1B12 / #B3261A → #FF5A2A) con borde naranja #FF7A1F, sangre que gotea con brillo húmedo. Alfa 60 %. Sin números ni símbolos.
        /// </summary>
        public static byte[] DetailedSkull(int n, int seed)
        {
            var hgt = new float[n * n]; var cav = new float[n * n]; var tth = new float[n * n];
            Func<int, float> U = x => (x + 0.5f) / n * 2f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    hgt[i] = SkullHeight(U(x) / 0.86f, U(y) / 0.97f, out cav[i], out tth[i]);
                }
            var px = new byte[n * n * 4]; float k = n / 6f;
            for (int y = 1; y < n - 1; y++)
                for (int x = 1; x < n - 1; x++)
                {
                    int i = y * n + x; float u = U(x) / 0.86f, v = U(y) / 0.97f;
                    float sd = SkullSilhouette(u, v);
                    float bl = Blood(u, v, out float side);
                    if (sd > 0.004f && bl <= 0f) continue;
                    float nx = -(hgt[i + 1] - hgt[i - 1]) * k, ny = -(hgt[i + n] - hgt[i - n]) * k, inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
                    nx *= inv; ny *= inv; float nz = inv;
                    float top = C01(nx * -0.2f + ny * 0.72f + nz * 0.66f), fire = C01(nx * 0.05f - ny * 0.75f + nz * 0.45f);
                    float c = cav[i];
                    float edgeAo = 0.45f + 0.55f * Smooth(0f, 0.12f, -sd);                                  // el borde se hunde en sombra
                    float lum = (0.04f + 1.15f * (float)Math.Pow(top, 1.6f)) * (1f - 0.95f * c) * edgeAo;
                    lum *= 1f - 0.25f * C01(Math.Abs(u) - 0.35f);                                         // los lados, más oscuros
                    if (tth[i] > 0f) lum = lum * 1.1f + 0.06f * tth[i];
                    // ruido de hueso, manchas de desgaste, sutura y grietas
                    float au = Math.Abs(u);
                    lum *= 0.9f + 0.2f * Fbm(u * 40f, v * 40f, seed, 2);
                    lum *= 1f - 0.32f * C01((Fbm(u * 5f, v * 5f, seed + 3, 4) - 0.55f) * 3.5f);
                    float tri = Math.Abs((u * 22f + 100f) % 1f - 0.5f) * 2f; float sut = 0.66f - 0.24f * u * u + 0.03f * (tri - 0.5f) * (0.4f + Hash((int)(u * 22f + 100f), 3, seed)) + 0.012f * (Fbm(u * 9f, 1f, seed + 5, 2) - 0.5f);
                    bool suture = Math.Abs(v - sut) < 0.005f && au < 0.62f;
                    float tri2 = Math.Abs((v * 22f + 100f) % 1f - 0.5f) * 2f; bool sag = v > sut && Math.Abs(u - 0.025f * (tri2 - 0.5f)) < 0.004f;
                    float cr = Fbm(u * 3.2f + 11f, v * 3.2f, seed + 9, 4), crMask = Fbm(u * 1.5f, v * 1.5f, seed + 13, 2);
                    bool crack = Math.Abs(cr - 0.5f) < 0.0045f && crMask > 0.52f && c < 0.2f;
                    if (suture || sag) lum *= 0.35f;
                    if (crack) lum *= 0.18f;
                    else if (Math.Abs(cr - 0.5f) < 0.009f && crMask > 0.52f && c < 0.2f) lum = Math.Min(1f, lum * 1.15f);   // filo claro de la grieta
                    SkullGradient(lum, out float r, out float g, out float b);
                    // resplandor de fuego desde abajo en las caras que miran hacia abajo (más abajo, más fuerte)
                    float fz = fire * (0.35f + 0.65f * C01((0.2f - v) * 0.9f)) * (1f - c) * 0.55f;
                    r += fz; g += fz * 0.36f; b += fz * 0.05f;
                    // borde de luz naranja en la silueta
                    float rim = (1f - Smooth(0f, 0.028f, -sd)) * C01(0.35f + 0.65f * C01(-nx * 0.3f + ny * 0.9f + 0.4f));
                    r += (RimOrange[0] - r) * rim; g += (RimOrange[1] - g) * rim; b += (RimOrange[2] - b) * rim;
                    float a = sd < 0f ? 1f : C01(1f - sd / 0.004f);
                    // sangre con brillo húmedo
                    if (bl > 0f)
                    {
                        float shade = 0.55f + 0.45f * (1f - Math.Abs(side));
                        float br = 0.23f + 0.33f * shade, bg = 0.012f + 0.03f * shade, bb = 0.01f + 0.02f * shade;
                        float gloss = (float)Math.Pow(C01(1f - Math.Abs(side + 0.35f) * 3f), 6f) * 0.75f;
                        br += gloss; bg += gloss * 0.55f; bb += gloss * 0.45f;
                        r += (br - r) * bl; g += (bg - g) * bl; b += (bb - b) * bl; a = Math.Max(a, bl);
                    }
                    int o = i * 4; px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a * SkullBodyAlpha);
                }
            return px;
        }

        /// <summary>Marco de panel (n x n, para 9-slice con borde "border"): banda negra al 75 % con una línea gris fina desgastada y grano leve.</summary>
        public static byte[] Frame(int n, int border, int seed)
        {
            var px = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int ex = Math.Min(x, n - 1 - x), ey = Math.Min(y, n - 1 - y), e = Math.Min(ex, ey);
                    float a = 0.75f, r = 0.02f, g = 0.018f, b = 0.018f;
                    if (e == 1) { float worn = Hash(x, y, seed) < 0.2f ? 0.45f : 1f; r = 0.416f * worn; g = 0.392f * worn; b = 0.376f * worn; a = 0.9f; }   // #6A6460
                    else if (e < 1) a = 0f;
                    if (e > 1 && Hash(x * 3, y, seed + 4) < 0.02f) { r += 0.04f; g += 0.035f; b += 0.03f; }
                    int o = (y * n + x) * 4;
                    px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a);
                }
            return px;
        }

        /// <summary>Resaltado de la fila activa: granate (#5A0A08) a transparente de izquierda a derecha, con un filo rojo arriba y abajo.</summary>
        public static byte[] Highlight(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), n = Fbm(x * 0.2f, y * 0.4f, seed, 2);
                    float a = C01(1f - u * 1.05f) * (0.85f + 0.15f * n);
                    bool rim = y == 0 || y == h - 1;
                    float r = rim ? 0.69f : Maroon[0] + 0.08f * (1f - u), g = rim ? 0.08f : Maroon[1], b = rim ? 0.05f : Maroon[2];
                    if (rim) a *= 0.8f;
                    int o = (y * w + x) * 4;
                    px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a);
                }
            return px;
        }

        /// <summary>Grano (ruido blanco con alfa bajo) en mosaico.</summary>
        public static byte[] Grain(int n, int seed)
        {
            var px = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++) { float v = Hash(i % n, i / n, seed); int o = i * 4; px[o] = px[o + 1] = px[o + 2] = B(v); px[o + 3] = B(GrainAmount); }
            return px;
        }

        /// <summary>Humo en mosaico (alfa de un fbm suave, más denso abajo).</summary>
        public static byte[] Smoke(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = FbmP(u * 4f, v * 3f, seed, 4, 3, 4);   // periódico: se desplaza sin costuras
                float a = C01((n - 0.42f) * 2.2f) * 0.55f;
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
