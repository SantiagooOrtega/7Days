using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class BoxGeometryTests
    {
        private static readonly V3 Size = new V3(3.5f, 1.2f, 0.6f);
        private const float Tile = 2f;

        private static MeshData Make(float bevel, float tile = Tile) => BoxGeometry.Build(Size, tile, new V2(0, 0), bevel);

        private static V3 Tri(MeshData m, int t, int k) => m.Positions[m.Triangles[t * 3 + k]];

        [TestCase(0f)]
        [TestCase(0.04f)]
        public void EveryTriangle_FacesOutward_InUnityWindingOrder(float bevel)
        {
            var m = Make(bevel);
            for (int t = 0; t < m.Triangles.Count / 3; t++)
            {
                V3 a = Tri(m, t, 0), b = Tri(m, t, 1), c = Tri(m, t, 2);
                V3 geometric = -V3.Cross(b - a, c - a); // en Unity (horario = frente) la normal es el cruce invertido
                V3 centroid = (a + b + c) * (1f / 3f);
                Assert.Greater(V3.Dot(geometric, centroid), 0f, "triángulo " + t + " mira hacia adentro");
                V3 n = m.Normals[m.Triangles[t * 3]];
                Assert.Greater(V3.Dot(geometric.Normalized, n), 0.99f, "la normal del vértice no coincide con el triángulo " + t);
            }
        }

        [TestCase(0f)]
        [TestCase(0.04f)]
        public void NormalsAndTangents_AreUnitAndPerpendicular(float bevel)
        {
            var m = Make(bevel);
            for (int i = 0; i < m.Positions.Count; i++)
            {
                Assert.AreEqual(1f, m.Normals[i].Length, 1e-3f);
                Assert.AreEqual(1f, m.Tangents[i].Length, 1e-3f, "tangente " + i);
                Assert.AreEqual(0f, V3.Dot(m.Normals[i], m.Tangents[i]), 1e-3f, "tangente no perpendicular en " + i);
            }
        }

        [TestCase(0f)]
        [TestCase(0.04f)]
        public void Faces_AreNotMirroredOrRotated_AndUseRealMeters(float bevel)
        {
            var m = Make(bevel);
            for (int f = 0; f < 6; f++)
            {
                int i0 = f * 4;
                // T = d(posición)/d(u), B = d(posición)/d(v) a partir de la UV
                V3 p0 = m.Positions[i0], p1 = m.Positions[i0 + 1], p3 = m.Positions[i0 + 3];
                V2 u0 = m.Uvs[i0], u1 = m.Uvs[i0 + 1], u3 = m.Uvs[i0 + 3];
                V3 T = (p1 - p0) * (1f / (u1.X - u0.X));
                V3 B = (p3 - p0) * (1f / (u3.Y - u0.Y));
                V3 n = m.Normals[i0];

                Assert.AreEqual(Tile, T.Length, 1e-3f, "cara " + f + ": 1 unidad de UV debe valer " + Tile + " m en u");
                Assert.AreEqual(Tile, B.Length, 1e-3f, "cara " + f + ": 1 unidad de UV debe valer " + Tile + " m en v");
                Assert.Greater(V3.Dot(V3.Cross(T, B), n), 0f, "cara " + f + " con la textura espejada");
                Assert.Greater(V3.Dot(T.Normalized, m.Tangents[i0]), 0.999f, "la tangente debe seguir la dirección de u");
                if (Math.Abs(n.Y) < 0.5f)
                    Assert.Greater(B.Normalized.Y, 0.999f, "cara " + f + ": la 'arriba' de la textura debe apuntar a +Y (veta y líneas verticales)");
            }
        }

        [Test]
        public void NoVertexLiesOutsideTheBox()
        {
            var m = Make(0.05f);
            foreach (var p in m.Positions)
            {
                Assert.LessOrEqual(Math.Abs(p.X), Size.X / 2 + 1e-4f);
                Assert.LessOrEqual(Math.Abs(p.Y), Size.Y / 2 + 1e-4f);
                Assert.LessOrEqual(Math.Abs(p.Z), Size.Z / 2 + 1e-4f);
            }
        }

        [Test]
        public void BeveledMesh_IsWatertight_EveryEdgeSharedByTwoTriangles()
        {
            foreach (float bevel in new[] { 0f, 0.05f })
            {
                var m = Make(bevel);
                var edges = new Dictionary<string, int>();
                void Count(V3 a, V3 b)
                {
                    string ka = Key(a), kb = Key(b);
                    string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "-" + kb : kb + "-" + ka;
                    edges[key] = edges.TryGetValue(key, out int c) ? c + 1 : 1;
                }
                for (int t = 0; t < m.Triangles.Count / 3; t++)
                {
                    Count(Tri(m, t, 0), Tri(m, t, 1));
                    Count(Tri(m, t, 1), Tri(m, t, 2));
                    Count(Tri(m, t, 2), Tri(m, t, 0));
                }
                foreach (var kv in edges)
                {
                    // las diagonales de los cuadriláteros aparecen en 2 triángulos del mismo cuadrilátero; el resto, en caras vecinas
                    Assert.AreEqual(2, kv.Value, "arista abierta con bisel " + bevel + ": " + kv.Key);
                }
            }
        }

        [Test]
        public void Bevel_AddsEdgeStripsAndCorners()
        {
            Assert.AreEqual(24, Make(0f).Positions.Count);
            // 24 de caras + 12 aristas x 4 + 8 esquinas x 3
            Assert.AreEqual(24 + 48 + 24, Make(0.05f).Positions.Count);
        }

        [Test]
        public void Bevel_IsClampedForThinBoxes()
        {
            var thin = BoxGeometry.Build(new V3(4f, 0.02f, 0.5f), 1f, new V2(0, 0), 0.5f);
            foreach (var p in thin.Positions) Assert.LessOrEqual(Math.Abs(p.Y), 0.01f + 1e-4f);
        }

        [Test]
        public void UvOffset_ShiftsAllUvs()
        {
            var a = Make(0f);
            var b = BoxGeometry.Build(Size, Tile, new V2(0.25f, 0.5f), 0f);
            for (int i = 0; i < a.Uvs.Count; i++)
            {
                Assert.AreEqual(a.Uvs[i].X + 0.25f, b.Uvs[i].X, 1e-5f);
                Assert.AreEqual(a.Uvs[i].Y + 0.5f, b.Uvs[i].Y, 1e-5f);
            }
        }

        [Test]
        public void WholeFaceTexture_CoversExactlyOneTilePerCubeFace()
        {
            // Caja de 1.2 m con tile 1.2: cada cara va de UV 0 a 1 (la textura de la caja se ve completa una vez)
            var m = BoxGeometry.Build(new V3(1.2f, 1.2f, 1.2f), 1.2f, new V2(0, 0), 0f);
            for (int f = 0; f < 6; f++)
            {
                float minU = 9, maxU = -9, minV = 9, maxV = -9;
                for (int k = 0; k < 4; k++)
                {
                    var uv = m.Uvs[f * 4 + k];
                    minU = Math.Min(minU, uv.X); maxU = Math.Max(maxU, uv.X);
                    minV = Math.Min(minV, uv.Y); maxV = Math.Max(maxV, uv.Y);
                }
                Assert.AreEqual(0f, minU, 1e-4f); Assert.AreEqual(1f, maxU, 1e-4f);
                Assert.AreEqual(0f, minV, 1e-4f); Assert.AreEqual(1f, maxV, 1e-4f);
            }
        }

        private static string Key(V3 p) => Math.Round(p.X, 4) + "," + Math.Round(p.Y, 4) + "," + Math.Round(p.Z, 4);
    }
}
