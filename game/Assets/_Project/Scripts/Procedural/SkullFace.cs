using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Placa facial rígida con forma de cráneo estilizado (diseño PROPIO para este juego, no basado en ninguna máscara existente): cuencas hexagonales
    /// angulosas inclinadas hacia fuera, ceja marcada, nariz en triángulo invertido, pómulos en diagonal con mejillas hundidas, placa bucal con una fila de
    /// dientes grabados y línea de mandíbula. Pura: la geometría usa Relief (formas grandes) y la textura usa Relief + Detail (grabados finos) y PaintCover
    /// (pintura blanca desgastada sobre metal oscuro), así el pintado coincide exactamente con el relieve.
    /// Coordenadas: s en -1..1 (de lado a lado), t en 0..1 (del mentón a la frente). Alturas en metros, positivas hacia fuera.
    /// </summary>
    public static class SkullFace
    {
        // placa respecto al hueso de la cabeza (su origen está en la base del cráneo): radios de la cara y alturas
        public const float ChinY = 0.005f, BrowY = 0.155f, ArcDeg = 72f, Thickness = 0.007f;

        public static float Relief(float s, float t)
        {
            float a = Math.Abs(s);
            float h = 0f;
            // cuencas: hexágono inclinado hacia fuera y abajo, muy hundido con paredes marcadas
            float socket = Socket(a, t);
            h -= 0.013f * Smooth(0.2f, 0.15f, socket);
            // ceja sobre las cuencas
            h += 0.0045f * Bump(t, 0.89f, 0.05f) * (1f - Smooth(0.72f, 0.95f, a));
            // nariz: triángulo invertido
            float nose = Nose(s, t);
            h -= 0.009f * Smooth(0.0f, -0.025f, nose);
            // pómulos: aristas en diagonal hacia las sienes
            float cheek = SegDist(a, t, 0.5f, 0.6f, 0.86f, 0.44f);
            h += 0.006f * Sq(Math.Max(0f, 1f - cheek / 0.075f));
            // mejillas hundidas bajo los pómulos
            float hollow = Dist(a, t, 0.52f, 0.36f);
            h -= 0.0045f * Sq(Math.Max(0f, 1f - hollow / 0.17f));
            // placa bucal ligeramente saliente
            h += 0.0025f * Smooth(0.52f, 0.46f, a) * Bump(t, 0.26f, 0.1f);
            // borde redondeado hacia dentro
            float edge = Math.Min(Math.Min(1f - a, t), 1f - t);
            h -= 0.004f * Smooth(0.06f, 0f, edge);
            return h;
        }

        /// <summary>Relieve fino (solo para el mapa de normales): dientes grabados, separación entre arcadas, línea de mandíbula y remaches.</summary>
        public static float Detail(float s, float t)
        {
            float a = Math.Abs(s), h = 0f;
            if (a < 0.48f && t > 0.15f && t < 0.37f)
            {
                // dientes: surcos verticales cada 0.08 y la separación entre arcadas
                float col = Frac(s / 0.08f + 0.5f);
                float groove = Math.Min(col, 1f - col) * 0.08f;
                h -= 0.0024f * Smooth(0.008f, 0.002f, groove) * (1f - Smooth(0.42f, 0.48f, a));
                h -= 0.003f * Smooth(0.012f, 0.004f, Math.Abs(t - 0.26f));
                h += 0.0006f * (float)Math.Sin((t - 0.15f) / 0.22f * Math.PI);   // cada diente algo abombado
            }
            // contorno de los dientes
            float lip = Math.Min(Math.Abs(t - 0.15f), Math.Abs(t - 0.37f));
            if (a < 0.5f) h -= 0.0015f * Smooth(0.008f, 0.002f, lip);
            // línea de la mandíbula: de la sien al mentón
            float jaw = SegDist(a, t, 0.5f, 0.3f, 0.9f, 0.55f);
            h -= 0.0016f * Smooth(0.012f, 0.003f, jaw);
            // remaches en las sienes y bajo la mandíbula
            h += 0.0018f * Smooth(0.022f, 0.012f, Dist(a, t, 0.86f, 0.8f));
            h += 0.0018f * Smooth(0.022f, 0.012f, Dist(a, t, 0.7f, 0.12f));
            return h;
        }

        /// <summary>1 = pintado de blanco, 0 = metal oscuro a la vista (cuencas, nariz, surcos de los dientes).</summary>
        public static float PaintCover(float s, float t)
        {
            float a = Math.Abs(s);
            float cover = 1f;
            cover *= Smooth(0.15f, 0.19f, Socket(a, t));           // cuencas en negro
            cover *= Smooth(-0.022f, -0.004f, Nose(s, t));          // nariz en negro
            float d = Detail(s, t);
            cover *= Smooth(-0.0018f, -0.0006f, d);                // surcos de los dientes y la mandíbula en negro
            return cover;
        }

        public static float Socket(float a, float t)
        {
            float dx = a - 0.37f, dy = t - 0.71f + dx * 0.32f;     // inclinadas hacia fuera y abajo
            float hex = Math.Max(Math.Abs(dx) * 0.9f + Math.Abs(dy) * 0.45f, Math.Abs(dy) * 1.35f);
            return hex;
        }

        public static float Nose(float s, float t)
        {
            // distancia con signo al triángulo invertido (negativa dentro)
            float top = 0.63f, bottom = 0.45f;
            float w = (t - bottom) / (top - bottom) * 0.13f;
            float inside = Math.Min(Math.Min(t - bottom, top - t), w - Math.Abs(s));
            return -inside;
        }

        /// <summary>Punto de la placa (en el espacio del hueso de la cabeza). side +1 = cara exterior (con relieve), -1 = interior.</summary>
        public static V3 Surface(float s01, float t01, int side, float headRadiusX, float headRadiusZ, float centerZ)
            => Point(s01, t01, side > 0 ? Relief(s01 * 2f - 1f, t01) : -Thickness, headRadiusX, headRadiusZ, centerZ);

        /// <summary>Punto de la superficie de la cara a "offset" metros hacia fuera de la placa lisa (visor, pañuelo, velo).</summary>
        public static V3 Point(float s01, float t01, float offset, float headRadiusX, float headRadiusZ, float centerZ)
        {
            float s = s01 * 2f - 1f;
            float y = ChinY + (BrowY - ChinY) * t01;
            float taper = 0.74f + 0.26f * Smooth(0f, 0.55f, t01);   // la mandíbula se estrecha hacia el mentón
            double th = s * ArcDeg * Math.PI / 180.0;
            float rx = headRadiusX * taper + 0.012f, rz = headRadiusZ * (0.9f + 0.1f * taper) + 0.012f;
            return new V3((float)Math.Sin(th) * (rx + offset), y, centerZ + (float)Math.Cos(th) * (rz + offset));
        }

        private static float Bump(float x, float c, float w) { float d = (x - c) / w; return (float)Math.Exp(-d * d * 2f); }
        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float SegDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            float k = Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (y - ay) * dy) / l2));
            return Dist(x, y, ax + dx * k, ay + dy * k);
        }
        private static float Smooth(float e0, float e1, float x) { float k = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0))); return k * k * (3f - 2f * k); }
        private static float Sq(float x) => x * x;
        private static float Frac(float x) => x - (float)Math.Floor(x);
    }
}
