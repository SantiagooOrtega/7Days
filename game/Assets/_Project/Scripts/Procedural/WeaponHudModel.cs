using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Iconos de armas (puros): la silueta lateral del modelo real del arma (WeaponModel) rasterizada en una textura pequeña: relleno blanco, detalle gris
    /// (caras inclinadas y separación entre piezas) y contorno oscuro. Boca del arma a la derecha. Se hace con 2x2 muestras por píxel para que los bordes salgan
    /// suaves. RGBA con alfa recta: el HUD la tiñe con el color de rareza (el contorno oscuro se queda oscuro).
    /// </summary>
    public static class WeaponIcons
    {
        public const int Width = 256, Height = 128;

        private struct Tri { public float U0, V0, U1, V1, U2, V2, Depth, Light; public int Part; }

        public static byte[] Rasterize(WeaponModelData model, int w = Width, int h = Height)
        {
            var tris = Project(model, out float umin, out float umax, out float vmin, out float vmax);
            const int ss = 2;
            int W = w * ss, H = h * ss;
            float pad = 7f * ss;   // margen para el contorno
            float scale = Math.Min((W - 2 * pad) / Math.Max(1e-4f, umax - umin), (H - 2 * pad) / Math.Max(1e-4f, vmax - vmin));
            float ou = (W - (umax - umin) * scale) * 0.5f - umin * scale, ov = (H - (vmax - vmin) * scale) * 0.5f - vmin * scale;

            var depth = new float[W * H];
            var part = new int[W * H];
            var light = new float[W * H];
            for (int i = 0; i < depth.Length; i++) { depth[i] = float.NegativeInfinity; part[i] = -1; }
            foreach (var t in tris)
            {
                float x0 = t.U0 * scale + ou, y0 = t.V0 * scale + ov, x1 = t.U1 * scale + ou, y1 = t.V1 * scale + ov, x2 = t.U2 * scale + ou, y2 = t.V2 * scale + ov;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2)))), maxX = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2)))), maxY = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
                float den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
                if (Math.Abs(den) < 1e-9f) continue;
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float a = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / den, b = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / den, c = 1f - a - b;
                        if (a < -1e-4f || b < -1e-4f || c < -1e-4f) continue;
                        int i = y * W + x;
                        if (t.Depth <= depth[i]) continue;
                        depth[i] = t.Depth; part[i] = t.Part; light[i] = t.Light;
                    }
            }

            // color por muestra: blanco con detalle gris (caras inclinadas) y líneas de separación entre piezas
            var rgb = new float[W * H];
            var covered = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (part[i] < 0) continue;
                    covered[i] = true;
                    float v = 0.78f + 0.22f * light[i];
                    bool edge = (x + 1 < W && part[i + 1] >= 0 && part[i + 1] != part[i]) || (y + 1 < H && part[i + W] >= 0 && part[i + W] != part[i]);
                    rgb[i] = edge ? 0.5f : v;
                }

            // contorno: muestras vacías a menos de 2.5 px (de la imagen final) de una cubierta
            var outline = new bool[W * H];
            int r = (int)Math.Ceiling(2.5f * ss);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (covered[y * W + x]) continue;
                    bool near = false;
                    for (int dy = -r; dy <= r && !near; dy++)
                    {
                        int yy = y + dy; if (yy < 0 || yy >= H) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx; if (xx < 0 || xx >= W) continue;
                            if (dx * dx + dy * dy <= r * r && covered[yy * W + xx]) { near = true; break; }
                        }
                    }
                    outline[y * W + x] = near;
                }

            var img = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sa = 0f, sr = 0f;
                    for (int j = 0; j < ss; j++)
                        for (int k = 0; k < ss; k++)
                        {
                            int i = (y * ss + j) * W + x * ss + k;
                            if (covered[i]) { sa += 1f; sr += rgb[i]; }
                            else if (outline[i]) { sa += 0.9f; sr += 0.06f * 0.9f; }
                        }
                    float n = ss * ss;
                    float alpha = sa / n, color = sa > 0f ? sr / sa : 0f;
                    int o = (y * w + x) * 4;
                    byte c8 = ToByte(color);
                    img[o] = c8; img[o + 1] = c8; img[o + 2] = c8; img[o + 3] = ToByte(alpha);
                }
            return img;
        }

        /// <summary>Icono "vacío" de una ranura sin arma: un marco tenue con esquinas, sin silueta.</summary>
        public static byte[] Empty(int w = Width, int h = Height)
        {
            var img = new byte[w * h * 4];
            int inset = 22, len = 26, thick = 3;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx = Math.Min(x - inset, w - 1 - inset - x), dy = Math.Min(y - inset, h - 1 - inset - y);
                    bool corner = dx >= 0 && dy >= 0 && ((dx < thick && (y < inset + len || y > h - 1 - inset - len)) || (dy < thick && (x < inset + len || x > w - 1 - inset - len)));
                    if (!corner) continue;
                    int o = (y * w + x) * 4;
                    img[o] = img[o + 1] = img[o + 2] = 255; img[o + 3] = 255;
                }
            return img;
        }

        /// <summary>Vista lateral desde +X: u = Z del arma (la boca a la derecha), v = Y (arriba). Triángulos con su profundidad (X máximo gana), luz y pieza.</summary>
        private static List<Tri> Project(WeaponModelData model, out float umin, out float umax, out float vmin, out float vmax)
        {
            var byName = new Dictionary<string, WeaponPart>();
            foreach (var p in model.Parts) byName[p.Name] = p;
            var list = new List<Tri>();
            umin = vmin = float.MaxValue; umax = vmax = float.MinValue;
            var light = new V3(0.5f, 0.7f, -0.35f).Normalized;
            for (int pi = 0; pi < model.Parts.Count; pi++)
            {
                var part = model.Parts[pi];
                if (part.Slot == WeaponModel.SlotDot) continue;
                var chain = new List<WeaponPart>();
                for (var cur = part; ; )
                {
                    chain.Add(cur);
                    if (cur.Parent == null || !byName.TryGetValue(cur.Parent, out cur)) break;
                }
                V3 W(V3 v)
                {
                    foreach (var c in chain) v = c.Position + Mat3.Euler(c.Euler).Mul(v);   // de la pieza hacia la raíz
                    return v;
                }
                var m = part.Mesh;
                for (int t = 0; t < m.Triangles.Count; t += 3)
                {
                    V3 a = W(m.Positions[m.Triangles[t]]), b = W(m.Positions[m.Triangles[t + 1]]), c = W(m.Positions[m.Triangles[t + 2]]);
                    V3 n = Winding.FrontNormal(a, b, c).Normalized;
                    if (n.X < -0.05f) continue;   // caras que miran al otro lado (la cara visible de cada sólido ya cubre la silueta)
                    list.Add(new Tri
                    {
                        U0 = a.Z, V0 = a.Y, U1 = b.Z, V1 = b.Y, U2 = c.Z, V2 = c.Y,
                        Depth = (a.X + b.X + c.X) / 3f, Light = Math.Max(0f, Math.Min(1f, 0.3f + 0.7f * Math.Max(0f, V3.Dot(n, light)) + 0.35f * Math.Max(0f, n.X))), Part = pi,
                    });
                    foreach (var p in new[] { a, b, c }) { umin = Math.Min(umin, p.Z); umax = Math.Max(umax, p.Z); vmin = Math.Min(vmin, p.Y); vmax = Math.Max(vmax, p.Y); }
                }
            }
            if (list.Count == 0) { umin = 0f; umax = 1f; vmin = 0f; vmax = 1f; }
            return list;
        }

        private static byte ToByte(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
    }

    /// <summary>Un hueco de la columna de armas ya colocado (coordenadas de referencia 1920x1080 del Canvas): Top = distancia desde lo alto de la columna hacia abajo; Shift = cuánto se desplaza hacia la izquierda.</summary>
    public struct SlotRect
    {
        public float Top, Width, Height, Scale, Alpha, Shift;
        public float Bottom => Top + Height;
        public float CenterY => Top + Height * 0.5f;
    }

    /// <summary>Animación y colocación de la columna vertical de ranuras: la activa crece (72x36 -> 100x50 a 1080p), se desplaza a la izquierda y se resalta; las demás se quedan pequeñas y semitransparentes.</summary>
    public class WeaponBarState
    {
        public const int Slots = 5;
        public const float BaseWidth = 72f, BaseHeight = 36f, ActiveScale = 1.39f, Gap = 6f, ActiveShift = 18f, InactiveAlpha = 0.42f, EmptyAlpha = 0.16f, NameSeconds = 1.5f;
        /// <summary>Alto total de la columna con 5 ranuras (siempre hay una activa): 4 pequeñas + 1 grande + 4 separaciones.</summary>
        public const float ColumnHeight = 4 * BaseHeight + BaseHeight * ActiveScale + 4 * Gap;

        public int Active { get; private set; }
        public int Count { get; private set; }
        public float NameAlpha { get; private set; }
        /// <summary>Cuánto cuenta como "activa" cada ranura (0..1), suavizado con muelle crítico: sube la nueva y baja la anterior sin saltos.</summary>
        public float[] Highlight { get; } = new float[Slots];
        public float Pulse { get; private set; }

        private CriticalSpring[] _spring = new CriticalSpring[Slots];
        private float _nameTimer = -1f;

        public WeaponBarState() { for (int i = 0; i < Slots; i++) _spring[i] = new CriticalSpring(0f); }

        /// <summary>Fija las ranuras ocupadas y la activa. Si la activa cambia, se muestra el nombre 1.5 s y la nueva "late" una vez. "instant": sin animación (al empezar).</summary>
        public bool Set(int count, int active, bool instant = false)
        {
            Count = Math.Max(0, Math.Min(Slots, count));
            active = Math.Max(0, Math.Min(Math.Max(0, Count - 1), active));
            bool changed = active != Active;
            Active = active;
            if (instant) for (int i = 0; i < Slots; i++) { _spring[i] = new CriticalSpring(i == Active ? 1f : 0f); Highlight[i] = _spring[i].Value; }
            else if (changed) { _nameTimer = NameSeconds; Pulse = 1f; }
            return changed;
        }

        public void ShowName() => _nameTimer = NameSeconds;

        public void Tick(float dt)
        {
            for (int i = 0; i < Slots; i++) Highlight[i] = _spring[i].Step(i == Active && i < Count ? 1f : 0f, 0.09f, dt);
            if (_nameTimer >= 0f) _nameTimer -= dt;
            // el nombre se ve entero y se funde en el último cuarto de segundo
            NameAlpha = _nameTimer <= 0f ? 0f : Math.Min(1f, _nameTimer / 0.25f);
            Pulse = Math.Max(0f, Pulse - dt * 4f);
        }

        public float ScaleOf(int i) => 1f + (ActiveScale - 1f) * Clamp01(Highlight[i]);
        public float AlphaOf(int i) => i >= Count ? EmptyAlpha : InactiveAlpha + (1f - InactiveAlpha) * Clamp01(Highlight[i]);

        /// <summary>Apila las 5 ranuras de arriba abajo (la 1 arriba), alineadas a la derecha. Una ranura que crece empuja a las vecinas: se desplazan suavemente y nunca se solapan.</summary>
        public void Layout(SlotRect[] outRects)
        {
            float y = 0f;
            for (int i = 0; i < Slots; i++)
            {
                float s = ScaleOf(i);
                outRects[i] = new SlotRect { Top = y, Width = BaseWidth * s, Height = BaseHeight * s, Scale = s, Alpha = AlphaOf(i), Shift = ActiveShift * Clamp01(Highlight[i]) };
                y += BaseHeight * s + Gap;
            }
        }

        public static float TotalHeight(SlotRect[] r) { float m = 0f; foreach (var s in r) m = Math.Max(m, s.Bottom); return m; }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Rueda radial de armas (Tab): la ranura 0 arriba y en sentido horario; la elegida es la de la dirección que apunta el ratón (con zona muerta).</summary>
    public static class RadialWheel
    {
        public const float DeadZone = 55f;   // píxeles de ratón acumulado antes de elegir

        /// <summary>Índice de la ranura en esa dirección (x derecha, y arriba), o -1 dentro de la zona muerta.</summary>
        public static int IndexAt(float x, float y, int count, float deadZone = DeadZone)
        {
            if (count <= 0 || x * x + y * y < deadZone * deadZone) return -1;
            double deg = Math.Atan2(x, y) * 180.0 / Math.PI;   // 0 = arriba, positivo = horario
            if (deg < 0) deg += 360.0;
            double step = 360.0 / count;
            return (int)Math.Floor((deg + step * 0.5) / step) % count;
        }

        /// <summary>Centro de una ranura de la rueda (x derecha, y arriba) en el radio dado.</summary>
        public static V2 SlotCenter(int index, int count, float radius)
        {
            double a = index * 2.0 * Math.PI / Math.Max(1, count);
            return new V2((float)Math.Sin(a) * radius, (float)Math.Cos(a) * radius);
        }

        /// <summary>Acumula el movimiento del ratón sin salirse del radio máximo (el cursor virtual vuelve rápido si cambias de dirección).</summary>
        public static V2 Accumulate(V2 current, float dx, float dy, float maxRadius)
        {
            float x = current.X + dx, y = current.Y + dy, len = (float)Math.Sqrt(x * x + y * y);
            return len > maxRadius ? new V2(x / len * maxRadius, y / len * maxRadius) : new V2(x, y);
        }
    }

    /// <summary>Ralentización suave del tiempo mientras la rueda está abierta (sin llegar a pausar): 1 normal, 0.25 con la rueda.</summary>
    public class SlowMotion
    {
        public const float Min = 0.25f;
        public float Scale => _spring.Value;
        private CriticalSpring _spring = new CriticalSpring(1f);

        /// <summary>Devuelve la escala de tiempo (en tiempo SIN escalar, para que el muelle no dependa de sí mismo).</summary>
        public float Tick(float unscaledDt, bool slow) => Math.Max(Min, Math.Min(1f, _spring.Step(slow ? Min : 1f, 0.07f, unscaledDt)));
        public void Reset() => _spring = new CriticalSpring(1f);
    }

    /// <summary>Textos del HUD de armas (munición cargador / reserva, modo de disparo).</summary>
    public static class WeaponHudText
    {
        public static string Ammo(WeaponData d, int magazine, int reserve) => d.IsMelee ? "" : magazine + " / " + reserve;
        public static string Mode(WeaponData d, string mode) => d.IsMelee ? "" : FireModes.Label(mode) + (d.fireModes != null && d.fireModes.Length > 1 ? "  [X]" : "");
        public static string Name(WeaponData d) => d.displayName ?? d.id;
    }
}
