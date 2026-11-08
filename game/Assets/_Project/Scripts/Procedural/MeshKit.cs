using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Celda de un atlas de texturas (fracciones de la textura): cada pieza de un personaje o un arma proyecta sus UV dentro de su celda, así un mismo material
    /// lleva tela, correas, cuero, metal... y cada pieza su propio desgaste (barro en las botas, pintura en la calavera). "Inset" deja margen para los mipmaps.
    /// </summary>
    public readonly struct AtlasCell
    {
        public readonly float U0, V0, Size, Inset;
        public AtlasCell(float u0, float v0, float size, float inset) { U0 = u0; V0 = v0; Size = size; Inset = inset; }

        /// <summary>Celda "index" de una rejilla de perRow x perRow (fila 0 abajo), con un margen de padFrac de su tamaño.</summary>
        public static AtlasCell Grid(int index, int perRow, float padFrac = 0.02f)
        {
            float s = 1f / perRow;
            return new AtlasCell((index % perRow) * s, (index / perRow) * s, s, s * padFrac);
        }

        /// <summary>Subcelda de esta celda (para atlas de dos niveles).</summary>
        public AtlasCell Sub(int index, int perRow, float padFrac = 0.02f)
        {
            float s = Size / perRow;
            return new AtlasCell(U0 + (index % perRow) * s, V0 + (index / perRow) * s, s, s * padFrac);
        }

        public V2 Map(float u01, float v01) => new V2(U0 + Inset + Clamp01(u01) * (Size - 2f * Inset), V0 + Inset + Clamp01(v01) * (Size - 2f * Inset));

        /// <summary>¿Este punto (u, v de toda la textura) cae en la celda? Devuelve las coordenadas locales 0..1.</summary>
        public bool Contains(float u, float v, out float lu, out float lv)
        {
            lu = (u - U0) / Size; lv = (v - V0) / Size;
            return lu >= 0f && lu < 1f && lv >= 0f && lv < 1f;
        }

        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
    }

    /// <summary>Rotación 3x3 (pura) con la convención de Euler de Unity: primero Z, luego X, luego Y (q = Y * X * Z).</summary>
    public readonly struct Mat3
    {
        public readonly float A, B, C, D, E, F, G, H, I;   // filas: (A B C) (D E F) (G H I)
        public Mat3(float a, float b, float c, float d, float e, float f, float g, float h, float i) { A = a; B = b; C = c; D = d; E = e; F = f; G = g; H = h; I = i; }

        public static readonly Mat3 Identity = new Mat3(1, 0, 0, 0, 1, 0, 0, 0, 1);

        public static Mat3 Euler(V3 deg)
        {
            double x = deg.X * Math.PI / 180.0, y = deg.Y * Math.PI / 180.0, z = deg.Z * Math.PI / 180.0;
            float cx = (float)Math.Cos(x), sx = (float)Math.Sin(x), cy = (float)Math.Cos(y), sy = (float)Math.Sin(y), cz = (float)Math.Cos(z), sz = (float)Math.Sin(z);
            var rx = new Mat3(1, 0, 0, 0, cx, -sx, 0, sx, cx);
            var ry = new Mat3(cy, 0, sy, 0, 1, 0, -sy, 0, cy);
            var rz = new Mat3(cz, -sz, 0, sz, cz, 0, 0, 0, 1);
            return ry * rx * rz;
        }

        public static Mat3 operator *(Mat3 m, Mat3 n) => new Mat3(
            m.A * n.A + m.B * n.D + m.C * n.G, m.A * n.B + m.B * n.E + m.C * n.H, m.A * n.C + m.B * n.F + m.C * n.I,
            m.D * n.A + m.E * n.D + m.F * n.G, m.D * n.B + m.E * n.E + m.F * n.H, m.D * n.C + m.E * n.F + m.F * n.I,
            m.G * n.A + m.H * n.D + m.I * n.G, m.G * n.B + m.H * n.E + m.I * n.H, m.G * n.C + m.H * n.F + m.I * n.I);

        public V3 Mul(V3 v) => new V3(A * v.X + B * v.Y + C * v.Z, D * v.X + E * v.Y + F * v.Z, G * v.X + H * v.Y + I * v.Z);
    }

    /// <summary>
    /// Junta piezas en una malla (pura): cajas con bisel, tornos (cilindros, conos, tubos con perfil), cúpulas y láminas con relieve y grosor. Cada pieza se
    /// coloca con posición y giro, conserva normales y tangentes (signo por vértice) y proyecta sus UV en su celda del atlas manteniendo la densidad
    /// (cellMeters = metros que representa la celda; si la pieza es más grande, se encoge para caber). Convención de caras: Winding (todo cerrado, hacia fuera).
    /// </summary>
    public class MeshBuilder
    {
        public readonly MeshData Data = new MeshData();
        public int TriangleCount => Data.Triangles.Count / 3;

        /// <summary>
        /// Añade una pieza. UV: por defecto en metros (densidad igual en todas las piezas); "exactUv" usa los UV de la pieza tal cual (0..1); "fit" estira los UV de la
        /// pieza para llenar TODA su celda (una banda de pliegues o una rótula con su textura propia).
        /// </summary>
        public void Append(MeshData part, V3 pos, Mat3 rot, AtlasCell cell, float cellMeters, bool exactUv = false, bool fit = false)
        {
            float umin = float.MaxValue, vmin = float.MaxValue, umax = float.MinValue, vmax = float.MinValue;
            foreach (var uv in part.Uvs) { umin = Math.Min(umin, uv.X); vmin = Math.Min(vmin, uv.Y); umax = Math.Max(umax, uv.X); vmax = Math.Max(vmax, uv.Y); }
            float span = Math.Max(1e-5f, Math.Max(umax - umin, vmax - vmin));
            float scale = cellMeters > 0f ? Math.Min(1f / cellMeters, 1f / span) : 1f / span;
            int b = Data.Positions.Count;
            for (int i = 0; i < part.Positions.Count; i++)
            {
                Data.Positions.Add(pos + rot.Mul(part.Positions[i]));
                Data.Normals.Add(rot.Mul(part.Normals[i]).Normalized);
                Data.Tangents.Add(rot.Mul(part.Tangents[i]).Normalized);
                Data.TangentW.Add(part.WOf(i));
                var uv = part.Uvs[i];
                Data.Uvs.Add(exactUv ? cell.Map(uv.X, uv.Y)
                    : fit ? cell.Map((uv.X - umin) / Math.Max(1e-5f, umax - umin), (uv.Y - vmin) / Math.Max(1e-5f, vmax - vmin))
                    : cell.Map((uv.X - umin) * scale, (uv.Y - vmin) * scale));
            }
            foreach (int t in part.Triangles) Data.Triangles.Add(b + t);
        }

        public void Append(MeshBuilder other, V3 pos, Mat3 rot)
        {
            int b = Data.Positions.Count;
            var d = other.Data;
            for (int i = 0; i < d.Positions.Count; i++)
            {
                Data.Positions.Add(pos + rot.Mul(d.Positions[i])); Data.Normals.Add(rot.Mul(d.Normals[i])); Data.Tangents.Add(rot.Mul(d.Tangents[i]));
                Data.TangentW.Add(d.WOf(i)); Data.Uvs.Add(d.Uvs[i]);
            }
            foreach (int t in d.Triangles) Data.Triangles.Add(b + t);
        }

        /// <summary>Caja con bisel centrada en "center", girada "euler" (grados).</summary>
        public void Box(V3 center, V3 size, V3 euler, float bevel, AtlasCell cell, float cellMeters)
            => Append(BoxGeometry.Build(size, 1f, new V2(0, 0), bevel), center, Mat3.Euler(euler), cell, cellMeters);

        /// <summary>
        /// Torno a lo largo del eje Y local de la pieza: "profile" = pares (y, radio) de abajo arriba; "depth" estira Z (secciones ovaladas); tapas opcionales.
        /// UV: u = metros alrededor (la costura queda en -Z, detrás), v = metros a lo largo.
        /// </summary>
        public void Lathe(V3 center, V3 euler, float[] profile, int sides, float depth, bool capBottom, bool capTop, AtlasCell cell, float cellMeters, bool fit = false)
            => Append(LatheData(profile, sides, depth, capBottom, capTop), center, Mat3.Euler(euler), cell, cellMeters, false, fit);

        /// <summary>
        /// Loft de anillos elípticos a lo largo de Y: "rings" = grupos de 5 (y, radio X, radio Z, desplazamiento X, desplazamiento Z) de abajo arriba. Con el centro de
        /// cada anillo desplazado salen bultos asimétricos (bíceps y pectoral hacia delante, gemelo y glúteo hacia atrás, cuádriceps) que un torno no da.
        /// </summary>
        public void Loft(V3 center, V3 euler, float[] rings, int sides, bool capBottom, bool capTop, AtlasCell cell, float cellMeters, bool fit = false)
            => Append(LoftData(rings, sides, capBottom, capTop), center, Mat3.Euler(euler), cell, cellMeters, false, fit);

        public const int LoftStride = 5;

        public static MeshData LoftData(float[] rings, int sides, bool capBottom, bool capTop)
        {
            var m = new MeshData();
            int n = rings.Length / LoftStride, row = sides + 1;
            float circ = 0f;
            for (int r = 0; r < n; r++) circ += (float)(2.0 * Math.PI * 0.5 * (rings[r * LoftStride + 1] + rings[r * LoftStride + 2])) / n;
            float along = 0f;
            for (int r = 0; r < n; r++)
            {
                int o = r * LoftStride;
                if (r > 0) along += (float)Math.Sqrt(Sq(rings[o] - rings[o - LoftStride]) + Sq(rings[o + 1] - rings[o + 1 - LoftStride]));
                for (int s = 0; s <= sides; s++)
                {
                    double a = Math.PI + s * 2.0 * Math.PI / sides;   // la costura queda detrás (-Z), como en los tornos
                    m.Positions.Add(new V3(rings[o + 3] + (float)Math.Sin(a) * rings[o + 1], rings[o], rings[o + 4] + (float)Math.Cos(a) * rings[o + 2]));
                    m.Uvs.Add(new V2(s / (float)sides * circ, along));
                }
            }
            for (int r = 0; r < n; r++)
                for (int s = 0; s <= sides; s++)
                {
                    int i = r * row + s;
                    int sp = r * row + (s + 1) % sides + (s + 1 >= sides ? 0 : 0), sm = r * row + (s - 1 + sides) % sides;
                    if (s == sides) { sp = r * row + 1; sm = r * row + sides - 1; }
                    int rp = Math.Min(n - 1, r + 1), rm = Math.Max(0, r - 1);
                    V3 dRing = m.Positions[sp] - m.Positions[sm];
                    V3 dAlong = m.Positions[rp * row + s] - m.Positions[rm * row + s];
                    V3 nrm = V3.Cross(dAlong, dRing).Normalized;
                    V3 center = new V3(rings[r * LoftStride + 3], rings[r * LoftStride], rings[r * LoftStride + 4]);
                    if (V3.Dot(nrm, m.Positions[i] - center) < 0f) nrm = -nrm;
                    if (nrm.Length < 0.5f) nrm = (m.Positions[i] - center).Normalized;
                    m.Normals.Add(nrm);
                    m.Tangents.Add(dRing.Length > 1e-9f ? dRing.Normalized : new V3(1f, 0f, 0f));
                }
            for (int r = 0; r < n - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = r * row + s, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Tri(m, a0, b0, a1, Outward(m, a0, a1, b0));
                    Tri(m, a1, b0, b1, Outward(m, a1, b0, b1));
                }
            if (capBottom) LoftCap(m, 0, sides, new V3(0f, -1f, 0f), circ);
            if (capTop) LoftCap(m, (n - 1) * row, sides, new V3(0f, 1f, 0f), circ);
            return m;
        }

        private static void LoftCap(MeshData m, int ringStart, int sides, V3 normal, float circ)
        {
            V3 c = new V3(0f, 0f, 0f);
            for (int s = 0; s < sides; s++) c = c + m.Positions[ringStart + s];
            c = c * (1f / sides);
            int center = m.Positions.Count;
            m.Positions.Add(c); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(circ * 0.5f, circ * 0.5f));
            int first = m.Positions.Count;
            for (int s = 0; s <= sides; s++)
            {
                V3 p = m.Positions[ringStart + s];
                m.Positions.Add(p); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(circ * 0.5f + (p.X - c.X), circ * 0.5f + (p.Z - c.Z)));
            }
            for (int s = 0; s < sides; s++) Tri(m, center, first + s, first + s + 1, normal);
        }

        /// <summary>Cilindro (o cono si r1 != r0) a lo largo del eje Y local, centrado en "center".</summary>
        public void Cylinder(V3 center, V3 euler, float r0, float r1, float length, int sides, AtlasCell cell, float cellMeters, bool caps = true)
            => Lathe(center, euler, new[] { -length * 0.5f, r0, length * 0.5f, r1 }, sides, 1f, caps, caps, cell, cellMeters);

        public static MeshData LatheData(float[] profile, int sides, float depth, bool capBottom, bool capTop)
        {
            var m = new MeshData();
            int rings = profile.Length / 2;
            float avgR = 0f;
            for (int r = 0; r < rings; r++) avgR += profile[r * 2 + 1];
            avgR /= Math.Max(1, rings);
            float circ = (float)(2.0 * Math.PI * avgR * (1f + depth) * 0.5f);
            float along = 0f;
            for (int r = 0; r < rings; r++)
            {
                float y = profile[r * 2], rad = profile[r * 2 + 1];
                if (r > 0) along += (float)Math.Sqrt(Sq(y - profile[r * 2 - 2]) + Sq(rad - profile[r * 2 - 1]));
                float dr = r < rings - 1 ? profile[r * 2 + 3] - profile[r * 2 + 1] : rad - profile[r * 2 - 1];
                float dy = r < rings - 1 ? profile[r * 2 + 2] - y : y - profile[r * 2 - 2];
                for (int s = 0; s <= sides; s++)
                {
                    double a = Math.PI + s * 2.0 * Math.PI / sides;   // empieza en -Z: la costura queda detrás
                    float sn = (float)Math.Sin(a), cs = (float)Math.Cos(a);
                    m.Positions.Add(new V3(sn * rad, y, cs * rad * depth));
                    // normal de la superficie de revolución (inclinada por la pendiente del perfil)
                    V3 radial = new V3(sn * depth, 0f, cs).Normalized;
                    float len = (float)Math.Sqrt(dr * dr + dy * dy);
                    V3 n = len > 1e-6f ? (radial * (dy / len) + new V3(0f, -dr / len, 0f)).Normalized : radial;
                    m.Normals.Add(n);
                    m.Tangents.Add(new V3(cs, 0f, -sn * depth).Normalized);
                    m.Uvs.Add(new V2(s / (float)sides * circ, along));
                }
            }
            int row = sides + 1;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = r * row + s, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Tri(m, a0, b0, a1, Outward(m, a0, a1, b0));
                    Tri(m, a1, b0, b1, Outward(m, a1, b0, b1));
                }
            if (capBottom) Cap(m, 0, sides, new V3(0f, -1f, 0f), circ);
            if (capTop) Cap(m, (rings - 1) * row, sides, new V3(0f, 1f, 0f), circ);
            return m;
        }

        /// <summary>
        /// Cúpula elipsoidal (casco, rodillera, hombrera) de radios "radii": de la latitud mínima (que puede variar con la longitud: cortes del casco) al polo,
        /// con grosor real (cara exterior, interior y canto). lon0..lon1 en grados (0 = +Z delante) para cúpulas parciales.
        /// </summary>
        public void Dome(V3 center, V3 euler, V3 radii, Func<float, float> minLatDeg, float thickness, int segLon, int segLat, AtlasCell cell, float cellMeters,
            float lon0 = -180f, float lon1 = 180f)
            => Append(DomeData(radii, minLatDeg, thickness, segLon, segLat, lon0, lon1), center, Mat3.Euler(euler), cell, cellMeters);

        public static MeshData DomeData(V3 radii, Func<float, float> minLatDeg, float thickness, int segLon, int segLat, float lon0 = -180f, float lon1 = 180f)
        {
            bool full = lon1 - lon0 >= 359.9f;
            V3 inner = new V3(Math.Max(0.002f, radii.X - thickness), Math.Max(0.002f, radii.Y - thickness), Math.Max(0.002f, radii.Z - thickness));
            return Sheet((s, t, side) =>
            {
                double lon = (lon0 + (lon1 - lon0) * s) * Math.PI / 180.0;
                float latMin = minLatDeg((float)(lon * 180.0 / Math.PI));
                double lat = (latMin + (89.5f - latMin) * t) * Math.PI / 180.0;
                V3 r = side > 0 ? radii : inner;
                return new V3((float)(Math.Cos(lat) * Math.Sin(lon)) * r.X, (float)Math.Sin(lat) * r.Y, (float)(Math.Cos(lat) * Math.Cos(lon)) * r.Z);
            }, segLon, segLat, thickness > 0f, full, closeTop: true);
        }

        /// <summary>
        /// Lámina paramétrica (s, t en 0..1) con grosor: surface(s, t, +1) es la cara exterior y surface(s, t, -1) la interior; los bordes se cierran con un canto.
        /// UV = (s, t) exactos (para que la textura coincida con el relieve, como la placa calavera). Normales por diferencias finitas, orientadas hacia fuera.
        /// "wrapS": la lámina da la vuelta completa en s (no hay canto en s = 0/1). "closeTop": t = 1 se cierra en un punto (polo).
        /// </summary>
        public static MeshData Sheet(Func<float, float, int, V3> surface, int ns, int nt, bool thick, bool wrapS = false, bool closeTop = false)
        {
            var m = new MeshData();
            int row = ns + 1;
            int Grid(int side)
            {
                int start = m.Positions.Count;
                for (int j = 0; j <= nt; j++)
                    for (int i = 0; i <= ns; i++)
                    {
                        float s = i / (float)ns, t = closeTop ? Math.Min(j / (float)nt, 0.999f) : j / (float)nt;
                        V3 p = surface(s, t, side);
                        const float e = 1e-3f;
                        V3 ds = surface(Math.Min(1f, s + e), t, side) - surface(Math.Max(0f, s - e), t, side);
                        V3 dt = surface(s, Math.Min(closeTop ? 0.999f : 1f, t + e), side) - surface(s, Math.Max(0f, t - e), side);
                        V3 n = V3.Cross(ds, dt).Normalized;   // s hacia +X y t hacia arriba en la cara exterior: sale hacia fuera
                        if (n.Length < 0.5f) n = p.Normalized;
                        m.Positions.Add(p);
                        m.Normals.Add(side > 0 ? n : -n);
                        m.Tangents.Add(ds.Length > 1e-9f ? ds.Normalized : new V3(1f, 0f, 0f));
                        m.Uvs.Add(new V2(s, t));
                    }
                return start;
            }
            int front = Grid(1);
            for (int j = 0; j < nt; j++)
                for (int i = 0; i < ns; i++)
                {
                    int a0 = front + j * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Tri(m, a0, b0, a1, Avg(m, a0, b0, a1)); Tri(m, a1, b0, b1, Avg(m, a1, b0, b1));
                }
            if (!thick) return m;
            int back = Grid(-1);
            for (int j = 0; j < nt; j++)
                for (int i = 0; i < ns; i++)
                {
                    int a0 = back + j * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Tri(m, a0, b0, a1, Avg(m, a0, b0, a1)); Tri(m, a1, b0, b1, Avg(m, a1, b0, b1));
                }
            // cantos: borde inferior (t = 0) siempre; laterales (s = 0, s = 1) si no da la vuelta; superior si no se cierra en un polo
            Rim(m, front, back, row, nt, 0, true, ns);
            if (!closeTop) Rim(m, front, back, row, nt, nt, true, ns);
            if (!wrapS) { Rim(m, front, back, row, nt, 0, false, nt); Rim(m, front, back, row, nt, ns, false, nt); }
            return m;
        }

        /// <summary>Canto entre la cara exterior y la interior a lo largo de una fila (t fija) o una columna (s fija).</summary>
        private static void Rim(MeshData m, int front, int back, int row, int nt, int fixedIndex, bool alongS, int count)
        {
            int start = m.Positions.Count;
            for (int k = 0; k <= count; k++)
            {
                int idx = alongS ? fixedIndex * row + k : k * row + fixedIndex;
                V3 pf = m.Positions[front + idx], pb = m.Positions[back + idx];
                int nb = alongS ? (fixedIndex == 0 ? idx + row : idx - row) : (fixedIndex == 0 ? idx + 1 : idx - 1);
                V3 inward = m.Positions[front + nb] - pf;
                V3 outward = (-inward).Normalized;
                m.Positions.Add(pf); m.Normals.Add(outward); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(k / (float)count, 0f));
                m.Positions.Add(pb); m.Normals.Add(outward); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(k / (float)count, 0.02f));
            }
            for (int k = 0; k < count; k++)
            {
                int a0 = start + k * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                V3 o = (m.Normals[a0] + m.Normals[b0]).Normalized;
                Tri(m, a0, a1, b0, o); Tri(m, b0, a1, b1, o);
            }
        }

        private static void Cap(MeshData m, int ringStart, int sides, V3 normal, float circ)
        {
            V3 c = new V3(0f, m.Positions[ringStart].Y, 0f);
            int center = m.Positions.Count;
            m.Positions.Add(c); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(circ * 0.5f, circ * 0.5f));
            int first = m.Positions.Count;
            for (int s = 0; s <= sides; s++)
            {
                V3 p = m.Positions[ringStart + s];
                m.Positions.Add(p); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(circ * 0.5f + p.X, circ * 0.5f + p.Z));
            }
            for (int s = 0; s < sides; s++) Tri(m, center, first + s, first + s + 1, normal);
        }

        private static V3 Outward(MeshData m, int a, int b, int c) => (m.Normals[a] + m.Normals[b] + m.Normals[c]).Normalized;
        private static V3 Avg(MeshData m, int a, int b, int c) => (m.Normals[a] + m.Normals[b] + m.Normals[c]).Normalized;

        /// <summary>Añade el triángulo orientado hacia "outward" (convención de Winding); descarta los degenerados.</summary>
        private static void Tri(MeshData m, int a, int b, int c, V3 outward)
        {
            V3 n = Winding.FrontNormal(m.Positions[a], m.Positions[b], m.Positions[c]);
            if (n.Length < 1e-10f) return;
            bool flip = V3.Dot(n, outward) < 0f;
            m.Triangles.Add(a); m.Triangles.Add(flip ? c : b); m.Triangles.Add(flip ? b : c);
        }

        private static float Sq(float x) => x * x;
    }
}
