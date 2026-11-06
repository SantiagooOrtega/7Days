using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Malla en listas planas (se convierte a UnityEngine.Mesh en el lado de Unity).</summary>
    public class MeshData
    {
        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<V3> Tangents = new List<V3>(); // w siempre = +1
        public readonly List<V2> Uvs = new List<V2>();
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Cantidad de vértices que pertenecen a las 6 caras principales (van primero).</summary>
        public int FaceVertexCount;
        /// <summary>Si es >= 0: los primeros N índices de Triangles forman el submesh 0 y el resto el submesh 1 (placa de señal: frente / dorso y cantos).</summary>
        public int SubmeshSplit = -1;
    }

    /// <summary>
    /// Caja con bisel, UV por cara en metros y tangentes. Cada cara recibe la textura a escala real
    /// (tileMeters por repetición), sin espejos ni giros: la "arriba" de la textura (v) apunta siempre a +Y en las caras
    /// verticales. El bisel suaviza las aristas con una tira a 45° y esquinas triangulares, y así las uniones entre caras
    /// no muestran costuras. Convención de Unity: el frente de un triángulo es el que se ve en sentido horario.
    /// </summary>
    public static class BoxGeometry
    {
        // Por cada cara: eje de la normal, signo, eje y signo de u, eje y signo de v. cross(u, v) = normal exterior.
        private static readonly int[][] FaceDefs =
        {
            //  normalAxis, sign, uAxis, uSign, vAxis, vSign
            new[] { 2,  1, 0,  1, 1,  1 }, // +Z: u=+X, v=+Y
            new[] { 2, -1, 0, -1, 1,  1 }, // -Z: u=-X, v=+Y
            new[] { 0,  1, 2, -1, 1,  1 }, // +X: u=-Z, v=+Y
            new[] { 0, -1, 2,  1, 1,  1 }, // -X: u=+Z, v=+Y
            new[] { 1,  1, 0,  1, 2, -1 }, // +Y: u=+X, v=-Z
            new[] { 1, -1, 0,  1, 2,  1 }, // -Y: u=+X, v=+Z
        };

        public static MeshData Build(V3 size, float tileMeters, V2 uvOffset, float bevel)
        {
            var m = new MeshData();
            var h = new V3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f);
            float minSize = Math.Min(size.X, Math.Min(size.Y, size.Z));
            float b = Math.Max(0f, Math.Min(bevel, minSize * 0.25f));

            for (int f = 0; f < 6; f++) AddFace(m, f, h, b, tileMeters, uvOffset);
            m.FaceVertexCount = m.Positions.Count;

            if (b > 1e-5f)
            {
                for (int i = 0; i < 3; i++)
                    for (int j = i + 1; j < 3; j++)
                        for (int si = -1; si <= 1; si += 2)
                            for (int sj = -1; sj <= 1; sj += 2)
                                AddEdgeStrip(m, i, si, j, sj, h, b, tileMeters, uvOffset);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            AddCorner(m, sx, sy, sz, h, b, tileMeters, uvOffset);
            }
            return m;
        }

        private static V2 FaceUv(int face, V3 p, V3 h, float tile, V2 off)
        {
            var d = FaceDefs[face];
            V3 u = V3.Axis(d[2]) * d[3], v = V3.Axis(d[4]) * d[5];
            float halfU = h[d[2]], halfV = h[d[4]];
            return new V2((V3.Dot(p, u) + halfU) / tile + off.X, (V3.Dot(p, v) + halfV) / tile + off.Y);
        }

        private static void AddFace(MeshData m, int f, V3 h, float b, float tile, V2 off)
        {
            var d = FaceDefs[f];
            V3 n = V3.Axis(d[0]) * d[1];
            V3 u = V3.Axis(d[2]) * d[3], v = V3.Axis(d[4]) * d[5];
            float hu = h[d[2]] - b, hv = h[d[4]] - b;
            V3 c = n * h[d[0]];

            V3 p0 = c - u * hu - v * hv, p1 = c + u * hu - v * hv, p2 = c + u * hu + v * hv, p3 = c - u * hu + v * hv;
            int s = m.Positions.Count;
            foreach (var p in new[] { p0, p1, p2, p3 })
            {
                m.Positions.Add(p);
                m.Normals.Add(n);
                m.Tangents.Add(u);
                m.Uvs.Add(FaceUv(f, p, h, tile, off));
            }
            m.Triangles.Add(s); m.Triangles.Add(s + 3); m.Triangles.Add(s + 2);
            m.Triangles.Add(s); m.Triangles.Add(s + 2); m.Triangles.Add(s + 1);
        }

        private static int FaceIndex(int axis, int sign)
        {
            for (int i = 0; i < FaceDefs.Length; i++) if (FaceDefs[i][0] == axis && FaceDefs[i][1] == sign) return i;
            throw new ArgumentException("cara inexistente");
        }

        private static void AddEdgeStrip(MeshData m, int i, int si, int j, int sj, V3 h, float b, float tile, V2 off)
        {
            int k = 3 - i - j;
            float k0 = -(h[k] - b), k1 = h[k] - b;
            V3 A(float kc) => V3.FromAxes(i, si * h[i], j, sj * (h[j] - b), k, kc);
            V3 B(float kc) => V3.FromAxes(i, si * (h[i] - b), j, sj * h[j], k, kc);
            V3 outward = (V3.Axis(i) * si + V3.Axis(j) * sj).Normalized;
            int faceA = FaceIndex(i, si);
            AddQuad(m, A(k0), A(k1), B(k1), B(k0), outward, faceA, h, tile, off);
        }

        private static void AddCorner(MeshData m, int sx, int sy, int sz, V3 h, float b, float tile, V2 off)
        {
            var px = new V3(sx * h.X, sy * (h.Y - b), sz * (h.Z - b));
            var py = new V3(sx * (h.X - b), sy * h.Y, sz * (h.Z - b));
            var pz = new V3(sx * (h.X - b), sy * (h.Y - b), sz * h.Z);
            var outward = new V3(sx, sy, sz).Normalized;
            int face = FaceIndex(0, sx);
            AddTri(m, px, py, pz, outward, face, h, tile, off);
        }

        private static V3 Tangent(int face, V3 normal)
        {
            var d = FaceDefs[face];
            V3 t = V3.Axis(d[2]) * d[3];
            t = t - normal * V3.Dot(t, normal); // proyectar al plano de la normal
            return t.Length > 1e-4f ? t.Normalized : V3.Cross(normal, new V3(0, 1, 0)).Normalized;
        }

        private static void AddQuad(MeshData m, V3 p0, V3 p1, V3 p2, V3 p3, V3 outward, int uvFace, V3 h, float tile, V2 off)
        {
            // Orientar el triángulo para que, visto desde fuera, quede en sentido horario (frente en Unity).
            if (V3.Dot(V3.Cross(p1 - p0, p2 - p0), outward) > 0f) { var t = p1; p1 = p3; p3 = t; }
            int s = m.Positions.Count;
            V3 tan = Tangent(uvFace, outward);
            foreach (var p in new[] { p0, p1, p2, p3 })
            {
                m.Positions.Add(p);
                m.Normals.Add(outward);
                m.Tangents.Add(tan);
                m.Uvs.Add(FaceUv(uvFace, p, h, tile, off));
            }
            m.Triangles.Add(s); m.Triangles.Add(s + 1); m.Triangles.Add(s + 2);
            m.Triangles.Add(s); m.Triangles.Add(s + 2); m.Triangles.Add(s + 3);
        }

        private static void AddTri(MeshData m, V3 p0, V3 p1, V3 p2, V3 outward, int uvFace, V3 h, float tile, V2 off)
        {
            if (V3.Dot(V3.Cross(p1 - p0, p2 - p0), outward) > 0f) { var t = p1; p1 = p2; p2 = t; }
            int s = m.Positions.Count;
            V3 tan = Tangent(uvFace, outward);
            foreach (var p in new[] { p0, p1, p2 })
            {
                m.Positions.Add(p);
                m.Normals.Add(outward);
                m.Tangents.Add(tan);
                m.Uvs.Add(FaceUv(uvFace, p, h, tile, off));
            }
            m.Triangles.Add(s); m.Triangles.Add(s + 1); m.Triangles.Add(s + 2);
        }
    }
}
