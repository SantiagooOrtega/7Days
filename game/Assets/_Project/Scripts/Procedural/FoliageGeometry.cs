using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Geometría procedural de vegetación y relleno (C# puro): árboles muertos con ramas (4 variantes), arbustos secos,
    /// matas de hierba, neumáticos y bolsas. Todo se genera con una semilla, así que es determinista y se puede probar.
    /// Cada malla está pensada para dibujarse con GPU instancing (decenas de instancias, una sola llamada).
    /// </summary>
    public static class FoliageGeometry
    {
        public const int TreeVariants = 4;
        public const int BushVariants = 2;

        // ------------------------------------------------------------------ Árboles muertos
        /// <summary>Árbol muerto de ~7 m en su variante base: 0 esbelto, 1 inclinado con muchas ramas, 2 tronco roto, 3 bifurcado.</summary>
        public static MeshData Tree(int variant, int seed)
        {
            variant = Math.Abs(variant) % TreeVariants;
            var rng = new Random(seed * 31 + variant * 7 + 1);
            var m = new MeshData();
            float height = variant == 2 ? 3.2f + F(rng) * 1.2f : 6f + F(rng) * 2.2f;
            float baseR = variant == 2 ? 0.3f : 0.2f + F(rng) * 0.06f;
            float lean = variant == 1 ? 2.4f : 0.45f;
            float dirA = F(rng) * 6.2832f;

            var trunk = TrunkPath(rng, height, lean, dirA, out var radii, baseR, variant == 2 ? 0.55f : 0.14f);
            Tube(m, trunk, radii, 7, 2f, true);

            int branches = variant == 0 ? 5 : variant == 1 ? 9 : variant == 2 ? 1 : 7;
            for (int i = 0; i < branches; i++)
            {
                float t = 0.38f + F(rng) * 0.57f;
                V3 origin = PointAt(trunk, t);
                float r0 = Math.Max(0.03f, RadiusAt(radii, t) * 0.7f);
                AddBranch(m, rng, origin, r0, (2.3f + (1f - t) * 2.4f) * (height / 7f), 2, 0.6f + F(rng) * 0.8f);
            }

            if (variant == 3)
            {
                // Segunda rama gruesa que sale del tronco a media altura (forma de "Y")
                V3 origin = PointAt(trunk, 0.42f);
                AddBranch(m, rng, origin, baseR * 0.55f, height * 0.65f, 2, 0.4f + F(rng) * 0.2f);
            }
            return m;
        }

        private static List<V3> TrunkPath(Random rng, float height, float lean, float dirA, out List<float> radii, float baseR, float topR)
        {
            const int rings = 9;
            var path = new List<V3>();
            radii = new List<float>();
            float cx = (float)Math.Cos(dirA), cz = (float)Math.Sin(dirA);
            float wob1 = F(rng) * 6f, wob2 = F(rng) * 6f;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float bend = lean * t * t * height * 0.12f;
                float wx = (float)Math.Sin(t * 3f + wob1) * 0.12f * t, wz = (float)Math.Sin(t * 3f + wob2) * 0.12f * t;
                path.Add(new V3(cx * bend + wx, t * height, cz * bend + wz));
                float flare = t < 0.1f ? 1f + (0.1f - t) * 4f : 1f; // base ensanchada
                radii.Add((baseR + (topR - baseR) * (float)Math.Pow(t, 0.8f)) * flare);
            }
            return path;
        }

        private static void AddBranch(MeshData m, Random rng, V3 origin, float r0, float length, int depth, float spread)
        {
            float ang = F(rng) * 6.2832f;
            float fromVertical = 0.5f + spread;
            V3 dir = new V3((float)(Math.Cos(ang) * Math.Sin(fromVertical)), (float)Math.Cos(fromVertical), (float)(Math.Sin(ang) * Math.Sin(fromVertical))).Normalized;
            const int pts = 4;
            var path = new List<V3>();
            var radii = new List<float>();
            V3 p = origin;
            for (int i = 0; i < pts; i++)
            {
                path.Add(p);
                radii.Add(Math.Max(0.008f, r0 * (1f - i / (float)pts) * (1f - i * 0.12f)));
                float step = length / (pts - 1);
                dir = (dir + new V3((F(rng) - 0.5f) * 0.4f, -0.12f, (F(rng) - 0.5f) * 0.4f)).Normalized; // se curvan y caen un poco
                p = p + dir * step;
            }
            radii[pts - 1] = 0.006f;
            Tube(m, path, radii, 5, 1.5f, true);

            if (depth > 0)
            {
                int subs = 2;
                for (int s = 0; s < subs; s++)
                {
                    float t = 0.35f + F(rng) * 0.5f;
                    AddBranch(m, rng, PointAt(path, t), Math.Max(0.01f, RadiusAt(radii, t) * 0.7f), length * 0.45f, depth - 1, 0.5f + F(rng) * 0.6f);
                }
            }
        }

        // ------------------------------------------------------------------ Arbustos secos
        public static MeshData Bush(int variant, int seed)
        {
            variant = Math.Abs(variant) % BushVariants;
            var rng = new Random(seed * 17 + variant * 3 + 5);
            var m = new MeshData();
            if (variant == 0)
            {
                // Matorral de "bultos" aplastados (hojas secas)
                int lumps = 6 + rng.Next(4);
                for (int i = 0; i < lumps; i++)
                {
                    float a = F(rng) * 6.2832f, d = F(rng) * 0.55f;
                    float r = 0.28f + F(rng) * 0.25f;
                    AddLump(m, new V3((float)Math.Cos(a) * d, r * 0.55f + F(rng) * 0.25f, (float)Math.Sin(a) * d), r, 0.65f, rng);
                }
            }
            else
            {
                // Arbusto de ramitas: muchas varas finas que salen del centro hacia arriba y afuera
                int sticks = 16 + rng.Next(8);
                for (int i = 0; i < sticks; i++)
                {
                    float a = F(rng) * 6.2832f, fromV = 0.2f + F(rng) * 0.9f, len = 0.6f + F(rng) * 0.6f;
                    V3 dir = new V3((float)(Math.Cos(a) * Math.Sin(fromV)), (float)Math.Cos(fromV), (float)(Math.Sin(a) * Math.Sin(fromV)));
                    var path = new List<V3> { new V3(0, 0, 0), dir * (len * 0.5f) + new V3(0, 0.02f, 0), dir * len };
                    Tube(m, path, new List<float> { 0.02f, 0.012f, 0.004f }, 4, 1f, true);
                }
            }
            return m;
        }

        private static void AddLump(MeshData m, V3 center, float radius, float flatten, Random rng)
        {
            // Icosaedro subdividido una vez, deformado: bulto orgánico de ~80 triángulos
            var ico = Icosphere();
            int s = m.Positions.Count;
            foreach (var v in ico.verts)
            {
                float noise = 0.85f + F(rng) * 0.3f;
                var p = new V3(v.X * radius * noise, v.Y * radius * flatten * noise, v.Z * radius * noise) + center;
                m.Positions.Add(p);
                m.Normals.Add(v.Normalized);
                m.Tangents.Add(new V3(1, 0, 0));
                m.Uvs.Add(new V2(v.X * 0.5f + 0.5f, v.Z * 0.5f + 0.5f));
            }
            int triStart = m.Triangles.Count;
            foreach (int i in ico.tris) m.Triangles.Add(s + i);
            SmoothNormals(m, s, triStart); // el bulto está aplastado y deformado: las normales salen de la geometría, no de la esfera
        }

        /// <summary>Normales suaves de los vértices desde "firstVertex" y los triángulos desde "firstTriIndex" (suma de normales frontales ponderadas por área).</summary>
        private static void SmoothNormals(MeshData m, int firstVertex, int firstTriIndex)
        {
            var acc = new V3[m.Positions.Count - firstVertex];
            for (int i = firstTriIndex; i < m.Triangles.Count; i += 3)
            {
                int a = m.Triangles[i], b = m.Triangles[i + 1], c = m.Triangles[i + 2];
                V3 n = Winding.FrontNormal(m.Positions[a], m.Positions[b], m.Positions[c]); // su longitud = 2 x área
                acc[a - firstVertex] = acc[a - firstVertex] + n;
                acc[b - firstVertex] = acc[b - firstVertex] + n;
                acc[c - firstVertex] = acc[c - firstVertex] + n;
            }
            for (int v = 0; v < acc.Length; v++)
            {
                V3 n = acc[v].Normalized;
                if (n.Length > 0.5f) m.Normals[firstVertex + v] = n;
            }
        }

        private static (List<V3> verts, List<int> tris) _ico;

        private static (List<V3> verts, List<int> tris) Icosphere()
        {
            if (_ico.verts != null) return _ico;
            float t = (1f + (float)Math.Sqrt(5)) / 2f;
            var v = new List<V3>
            {
                new V3(-1, t, 0), new V3(1, t, 0), new V3(-1, -t, 0), new V3(1, -t, 0),
                new V3(0, -1, t), new V3(0, 1, t), new V3(0, -1, -t), new V3(0, 1, -t),
                new V3(t, 0, -1), new V3(t, 0, 1), new V3(-t, 0, -1), new V3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].Normalized;
            // Caras (sentido horario visto desde fuera, convención de Unity)
            int[][] f =
            {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1},
            };
            var tris = new List<int>();
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                var mid = ((v[a] + v[b]) * 0.5f).Normalized;
                v.Add(mid);
                cache[key] = v.Count - 1;
                return v.Count - 1;
            }
            foreach (var tri in f)
            {
                int a = tri[0], b = tri[1], c = tri[2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                // orientar cada triángulo hacia afuera para el frente de Unity
                foreach (var t3 in new[] { new[] { a, ab, ca }, new[] { b, bc, ab }, new[] { c, ca, bc }, new[] { ab, bc, ca } })
                {
                    V3 p0 = v[t3[0]], p1 = v[t3[1]], p2 = v[t3[2]];
                    V3 centroid = (p0 + p1 + p2) * (1f / 3f);
                    bool inward = !Winding.FacesOutward(p0, p1, p2, centroid);
                    tris.Add(t3[0]); tris.Add(inward ? t3[2] : t3[1]); tris.Add(inward ? t3[1] : t3[2]);
                }
            }
            _ico = (v, tris);
            return _ico;
        }

        // ------------------------------------------------------------------ Hierba seca
        /// <summary>
        /// Mata de hierba: hojas finas de UNA sola capa en un radio de ~0.25 m. Se dibuja con material de doble cara (Cull Off) y
        /// todas las normales apuntan hacia arriba: así las dos caras reciben la misma luz suave sin duplicar triángulos
        /// (URP Lit no voltea las normales de las caras traseras).
        /// </summary>
        public static MeshData GrassPatch(int seed)
        {
            var rng = new Random(seed * 13 + 3);
            var m = new MeshData();
            int blades = 14 + rng.Next(6);
            for (int i = 0; i < blades; i++)
            {
                float a = F(rng) * 6.2832f, d = (float)Math.Sqrt(F(rng)) * 0.25f;
                V3 b = new V3((float)Math.Cos(a) * d, 0f, (float)Math.Sin(a) * d);
                float h = 0.25f + F(rng) * 0.35f, w = 0.025f + F(rng) * 0.02f, yaw = F(rng) * 6.2832f;
                float lean = (F(rng) - 0.3f) * h * 0.7f;
                V3 right = new V3((float)Math.Cos(yaw), 0f, (float)Math.Sin(yaw));
                V3 fwd = new V3(-(float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
                V3 l = b - right * (w * 0.5f), r = b + right * (w * 0.5f), tip = b + new V3(0, h, 0) + fwd * lean;
                AddSingleLayerTri(m, l, r, tip);
            }
            return m;
        }

        private static void AddSingleLayerTri(MeshData m, V3 a, V3 b, V3 c)
        {
            int s = m.Positions.Count;
            foreach (var p in new[] { a, b, c })
            {
                m.Positions.Add(p);
                m.Normals.Add(new V3(0, 1, 0)); // arriba para las dos caras
                m.Tangents.Add(new V3(1, 0, 0));
                m.Uvs.Add(new V2(0, 0));
            }
            // con Cull Off el orden da igual, pero se deja con el frente hacia arriba por si se usa con Cull Back
            if (Winding.FacesOutward(a, b, c, new V3(0, 1, 0))) { m.Triangles.Add(s); m.Triangles.Add(s + 1); m.Triangles.Add(s + 2); }
            else { m.Triangles.Add(s); m.Triangles.Add(s + 2); m.Triangles.Add(s + 1); }
        }

        // ------------------------------------------------------------------ Neumático y bolsa
        /// <summary>Neumático tendido en el suelo (toro): radio mayor 0.33 m, menor 0.13 m.</summary>
        public static MeshData Tire(int seed)
        {
            const int major = 14, minor = 8;
            const float R = 0.33f, r = 0.13f;
            var m = new MeshData();
            for (int i = 0; i <= major; i++)
            {
                float a = i / (float)major * 6.2832f;
                V3 c = new V3((float)Math.Cos(a) * R, r, (float)Math.Sin(a) * R);
                V3 radial = new V3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                for (int j = 0; j <= minor; j++)
                {
                    float b = j / (float)minor * 6.2832f;
                    V3 n = radial * (float)Math.Cos(b) + new V3(0, (float)Math.Sin(b), 0);
                    m.Positions.Add(c + n * r);
                    m.Normals.Add(n);
                    m.Tangents.Add(new V3(-(float)Math.Sin(a), 0, (float)Math.Cos(a)));
                    m.Uvs.Add(new V2(i / (float)major * 3f, j / (float)minor));
                }
            }
            int row = minor + 1;
            for (int i = 0; i < major; i++)
            {
                for (int j = 0; j < minor; j++)
                {
                    int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    OrientedTri(m, a0, b0, a1);
                    OrientedTri(m, a1, b0, b1);
                }
            }
            return m;
        }

        /// <summary>Bolsa de basura: bulto irregular aplastado de ~0.35 m.</summary>
        public static MeshData Bag(int seed)
        {
            var rng = new Random(seed * 19 + 1);
            var m = new MeshData();
            AddLump(m, new V3(0, 0.1f, 0), 0.22f + F(rng) * 0.06f, 0.55f, rng);
            return m;
        }

        // ------------------------------------------------------------------ utilidades
        private static float F(Random r) => (float)r.NextDouble();

        private static void OrientedTri(MeshData m, int a, int b, int c)
        {
            V3 p0 = m.Positions[a], p1 = m.Positions[b], p2 = m.Positions[c];
            V3 n = (m.Normals[a] + m.Normals[b] + m.Normals[c]).Normalized;
            bool flip = !Winding.FacesOutward(p0, p1, p2, n);
            m.Triangles.Add(a); m.Triangles.Add(flip ? c : b); m.Triangles.Add(flip ? b : c);
        }

        private static V3 PointAt(IList<V3> path, float t)
        {
            float f = Math.Max(0f, Math.Min(1f, t)) * (path.Count - 1);
            int i = Math.Min(path.Count - 2, (int)f);
            float k = f - i;
            return path[i] + (path[i + 1] - path[i]) * k;
        }

        private static float RadiusAt(IList<float> radii, float t)
        {
            float f = Math.Max(0f, Math.Min(1f, t)) * (radii.Count - 1);
            int i = Math.Min(radii.Count - 2, (int)f);
            return radii[i] + (radii[i + 1] - radii[i]) * (f - i);
        }

        /// <summary>Tubo cónico a lo largo de una trayectoria. UV: u alrededor, v en metros/tile a lo largo. Normales suaves.</summary>
        public static void Tube(MeshData m, IList<V3> path, IList<float> radii, int sides, float tileMeters, bool capEnd)
        {
            int start = m.Positions.Count;
            float dist = 0f;
            V3 prevFrameUp = new V3(0, 1, 0);
            for (int i = 0; i < path.Count; i++)
            {
                V3 t = (i < path.Count - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).Normalized;
                V3 refUp = Math.Abs(V3.Dot(t, prevFrameUp)) > 0.95f ? new V3(1, 0, 0) : prevFrameUp;
                V3 n1 = V3.Cross(t, refUp).Normalized;
                V3 n2 = V3.Cross(t, n1).Normalized;
                if (i > 0) dist += (path[i] - path[i - 1]).Length;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * 6.2832f;
                    V3 n = n1 * (float)Math.Cos(a) + n2 * (float)Math.Sin(a);
                    m.Positions.Add(path[i] + n * radii[i]);
                    m.Normals.Add(n);
                    m.Tangents.Add((n1 * -(float)Math.Sin(a) + n2 * (float)Math.Cos(a)).Normalized);
                    m.Uvs.Add(new V2(s / (float)sides * 2f, dist / tileMeters));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a0 = start + i * row + s, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    OrientedTri(m, a0, b0, a1);
                    OrientedTri(m, a1, b0, b1);
                }
            }
            if (capEnd && path.Count > 1)
            {
                V3 axis = (path[path.Count - 1] - path[path.Count - 2]).Normalized;
                int c = m.Positions.Count;
                m.Positions.Add(path[path.Count - 1]);
                m.Normals.Add(axis);
                m.Tangents.Add(new V3(1, 0, 0));
                m.Uvs.Add(new V2(0.5f, dist / tileMeters));
                int last = start + (path.Count - 1) * row;
                for (int s = 0; s < sides; s++) OrientedTri(m, c, last + s + 1, last + s);
            }
        }
    }
}
