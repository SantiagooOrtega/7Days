using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Arte procedural del menú y de las pantallas (puro, propio): fondo de pintura roja y naranja desgastada con grunge, ciudad en ruinas ardiendo abajo
    /// (edificios rotos, grúas), viñeta (la calavera roja va aparte: RedSkull); marco de panel
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

        /// <summary>
        /// Una llama (u 0..1 se repite en horizontal, v 0 abajo..1 arriba, fase 0..1 se repite en el tiempo) → RGBA 0..1. Ruido que sube con turbulencia,
        /// rampa de color de rojo oscuro a amarillo casi blanco en la base; la fase 1 es idéntica a la 0 (bucle sin salto).
        /// </summary>
        public static void Flame(float u, float v, float phase, int seed, out float r, out float g, out float b, out float a)
        {
            const int PX = 10, PY = 8;
            float sx = u * PX, sy = v * 2.2f;
            float warp = FbmP(sx * 0.5f + 3f, sy * 1.5f - phase * PY, seed + 5, PX / 2, PY, 2) - 0.5f;
            float n = FbmP(sx + warp * 2.2f, sy - phase * PY, seed, PX, PY, 5);
            float heat = C01(n * 2.1f - (float)Math.Pow(v, 0.7f) * 1.9f + 0.02f - 0.25f * v * v);
            heat = heat * heat * (3 - 2 * heat);
            if (heat < 0.5f) { float t = heat / 0.5f; r = 0.45f + 0.55f * t; g = 0.04f + 0.3f * t; b = 0.01f; }
            else if (heat < 0.8f) { float t = (heat - 0.5f) / 0.3f; r = 1f; g = 0.34f + 0.42f * t; b = 0.02f + 0.16f * t; }
            else { float t = (heat - 0.8f) / 0.2f; r = 1f; g = 0.76f + 0.2f * t; b = 0.18f + 0.5f * t; }
            a = C01(heat * 2.6f);
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
        public const float SkullAlpha = 0.66f;
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

        /// <summary>
        /// Recorte de una imagen importada: si ya trae transparencia la usa; si no, quita el fondo plano (color de las esquinas) por distancia de color.
        /// Devuelve gris (luminancia) y alfa por píxel.
        /// </summary>
        public static void Cutout(byte[] rgba, int w, int h, out float[] lum, out float[] alpha)
        {
            lum = new float[w * h]; alpha = new float[w * h];
            bool hasAlpha = false; for (int i = 0; i < w * h; i++) if (rgba[i * 4 + 3] < 250) { hasAlpha = true; break; }
            float kr = 0, kg = 0, kb = 0; int[] cs = { 0, w - 1, (h - 1) * w, h * w - 1 };
            foreach (var c in cs) { kr += rgba[c * 4] / 1020f; kg += rgba[c * 4 + 1] / 1020f; kb += rgba[c * 4 + 2] / 1020f; }
            for (int i = 0; i < w * h; i++)
            {
                float r = rgba[i * 4] / 255f, g = rgba[i * 4 + 1] / 255f, b = rgba[i * 4 + 2] / 255f;
                lum[i] = 0.299f * r + 0.587f * g + 0.114f * b;
                if (hasAlpha) alpha[i] = rgba[i * 4 + 3] / 255f;
                else { float d = (float)Math.Sqrt((r - kr) * (r - kr) + (g - kg) * (g - kg) + (b - kb) * (b - kb)); alpha[i] = Smooth(0.08f, 0.22f, d); }
            }
        }

        /// <summary>Respaldo procedural sin símbolos (n x n): cráneo en relieve en gris (cuencas negras), alfa = silueta.</summary>
        public static void ProceduralSkull(int n, out float[] lum, out float[] alpha)
        {
            lum = new float[n * n]; alpha = new float[n * n]; float e = 2f / n;
            float H(float u, float v) => Smooth(0f, -0.16f, BadgeSkullSd(u, v)) * (0.6f + 0.4f * C01(1f - (u * u + (v - 0.1f) * (v - 0.1f)) * 0.6f));
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n * 2f - 1f) / 0.86f, v = ((y + 0.5f) / n * 2f - 1f) / 0.86f + 0.05f;
                    float head = SkullHeadSdBadge(u, v), sd = BadgeSkullSd(u, v); int i = y * n + x;
                    alpha[i] = 1f - Smooth(-0.01f, 0.01f, head);
                    if (sd < 0f)
                    {
                        float h0 = H(u, v), nx = -(H(u + e, v) - h0) * 30f, ny = -(H(u, v + e) - h0) * 30f, inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
                        lum[i] = 0.05f + 0.9f * (float)Math.Pow(C01((nx * -0.5f + ny * 0.6f + 0.62f) * inv), 2.2f);
                    }
                    else lum[i] = 0.02f;
                }
        }

        /// <summary>
        /// La calavera roja del menú (w x h, RGBA32): gris → degradado rojo, relieve (realce desde arriba a la izquierda), borde de luz naranja #FF7A1F en el lado
        /// iluminado, grano y grietas multiplicados, resplandor de fuego desde abajo; alfa = silueta × SkullAlpha (60-70 %).
        /// </summary>
        public static byte[] RedSkull(float[] lum, float[] alpha, int w, int h, int seed)
        {
            var px = new byte[w * h * 4]; int d = Math.Max(2, w / 128);
            float A(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? 0f : alpha[y * w + x];
            float L(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? 0f : lum[y * w + x];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; float a = alpha[i]; if (a <= 0f) continue;
                    float u = x / (float)w, v = y / (float)h;
                    float relief = (L(x - 1, y + 1) - L(x + 1, y - 1)) * 2.5f;
                    float l = C01(lum[i] * 0.9f + relief);
                    SkullGradient(l, out float r, out float g, out float b);
                    // borde de luz naranja: dentro de la silueta con el vecino hacia la luz (arriba a la izquierda) fuera
                    float rim = C01(a - A(x - d, y + d)) * 0.9f + C01(a - A(x, y + d)) * 0.3f;
                    r += (RimOrange[0] - r) * rim; g += (RimOrange[1] - g) * rim; b += (RimOrange[2] - b) * rim;
                    // desgaste: grano y grietas por multiplicación
                    float grain = Hash(x, y, seed) > 0.82f ? 0.72f : 1f;
                    float cr = Fbm(u * 6f + 3f, v * 6f, seed + 4, 4), crack = Math.Abs(cr - 0.5f) < 0.007f ? 0.45f : 1f;
                    float wear = 0.82f + 0.25f * Fbm(u * 14f, v * 14f, seed + 8, 3);
                    float m = grain * crack * wear; r *= m; g *= m; b *= m;
                    // resplandor de fuego por abajo
                    float fire = (1f - v) * (1f - v) * (1f - v) * 0.55f;
                    r += fire * 1f; g += fire * 0.38f; b += fire * 0.06f;
                    int o = i * 4; px[o] = B(r); px[o + 1] = B(g); px[o + 2] = B(b); px[o + 3] = B(a * SkullAlpha);
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
