using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class FoliageGeometryTests
    {
        private static IEnumerable<MeshData> AllMeshes()
        {
            for (int v = 0; v < FoliageGeometry.TreeVariants; v++) yield return FoliageGeometry.Tree(v, 1);
            for (int v = 0; v < FoliageGeometry.BushVariants; v++) yield return FoliageGeometry.Bush(v, 1);
            yield return FoliageGeometry.GrassPatch(1);
            yield return FoliageGeometry.Tire(1);
            yield return FoliageGeometry.Bag(1);
        }

        private static void Bounds(MeshData m, out V3 min, out V3 max)
        {
            float x0 = 1e9f, y0 = 1e9f, z0 = 1e9f, x1 = -1e9f, y1 = -1e9f, z1 = -1e9f;
            foreach (var p in m.Positions)
            {
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
            }
            min = new V3(x0, y0, z0); max = new V3(x1, y1, z1);
        }

        [Test]
        public void AllMeshes_AreWellFormed()
        {
            int n = 0;
            foreach (var m in AllMeshes())
            {
                n++;
                Assert.Greater(m.Positions.Count, 3, "malla " + n + " vacía");
                Assert.Less(m.Positions.Count, 30000, "demasiados vértices para instanciar muchos");
                Assert.AreEqual(m.Positions.Count, m.Normals.Count);
                Assert.AreEqual(m.Positions.Count, m.Uvs.Count);
                Assert.AreEqual(m.Positions.Count, m.Tangents.Count);
                Assert.AreEqual(0, m.Triangles.Count % 3);
                foreach (int i in m.Triangles) Assert.That(i, Is.InRange(0, m.Positions.Count - 1));
                for (int i = 0; i < m.Positions.Count; i++)
                {
                    Assert.IsFalse(float.IsNaN(m.Positions[i].X + m.Positions[i].Y + m.Positions[i].Z), "NaN en posición");
                    Assert.AreEqual(1f, m.Normals[i].Length, 0.02f, "normal no unitaria en malla " + n);
                }
            }
            Assert.AreEqual(FoliageGeometry.TreeVariants + FoliageGeometry.BushVariants + 3, n);
        }

        [Test]
        public void Trees_AreDeterministic_AndVariantsDiffer()
        {
            var a = FoliageGeometry.Tree(0, 4);
            var b = FoliageGeometry.Tree(0, 4);
            CollectionAssert.AreEqual(Flat(a), Flat(b));
            var counts = new HashSet<int>();
            for (int v = 0; v < FoliageGeometry.TreeVariants; v++) counts.Add(FoliageGeometry.Tree(v, 4).Positions.Count);
            Assert.GreaterOrEqual(counts.Count, 3, "las variantes deben ser realmente distintas");
            CollectionAssert.AreNotEqual(Flat(FoliageGeometry.Tree(0, 4)), Flat(FoliageGeometry.Tree(0, 5)), "otra semilla, otro árbol");
        }

        [Test]
        public void Trees_StandOnTheGround_HaveRealisticHeight_AndBranchesAddGeometry()
        {
            for (int v = 0; v < FoliageGeometry.TreeVariants; v++)
            {
                var t = FoliageGeometry.Tree(v, 2);
                Bounds(t, out var min, out var max);
                Assert.AreEqual(0f, min.Y, 0.05f, "el tronco apoya en y=0 (variante " + v + ")");
                if (v == 2) Assert.That(max.Y, Is.InRange(2.5f, 5.5f), "tronco roto: bajo");
                else Assert.That(max.Y, Is.InRange(5f, 11f), "árbol alto, variante " + v);
                Assert.Greater(t.Positions.Count, 150, "tronco con ramas, variante " + v);
            }
            Assert.Greater(FoliageGeometry.Tree(1, 2).Positions.Count, FoliageGeometry.Tree(2, 2).Positions.Count * 2, "el inclinado tiene muchas más ramas que el tronco roto");
        }

        [Test]
        public void TreeTriangles_FaceOutward()
        {
            var t = FoliageGeometry.Tree(0, 3);
            // el 90% de los triángulos debe mirar hacia fuera del eje del tronco (los extremos/tapas pueden variar)
            int ok = 0, total = 0;
            for (int i = 0; i < t.Triangles.Count; i += 3)
            {
                V3 a = t.Positions[t.Triangles[i]], b = t.Positions[t.Triangles[i + 1]], c = t.Positions[t.Triangles[i + 2]];
                V3 g = Winding.FrontNormal(a, b, c);
                V3 n = (t.Normals[t.Triangles[i]] + t.Normals[t.Triangles[i + 1]] + t.Normals[t.Triangles[i + 2]]).Normalized;
                total++;
                if (V3.Dot(g, n) > 0f) ok++;
            }
            Assert.Greater(ok / (double)total, 0.97);
        }

        [Test]
        public void Grass_IsLow_SingleLayer_WithUpwardNormals()
        {
            var g = FoliageGeometry.GrassPatch(1);
            Bounds(g, out var min, out var max);
            Assert.That(max.Y, Is.InRange(0.2f, 0.75f));
            Assert.That(Math.Max(max.X - min.X, max.Z - min.Z), Is.LessThan(0.9f));
            Assert.AreEqual(g.Positions.Count, g.Triangles.Count, "una hoja = un triángulo (3 vértices, 3 índices): sin capas duplicadas");
            // con Cull Off las dos caras usan la MISMA normal (URP Lit no la voltea), así que debe ser "hacia arriba" para que ambas se iluminen bien
            foreach (var n in g.Normals) Assert.AreEqual(1f, n.Y, 1e-4f);
        }

        [Test]
        public void Tire_HasTheRightSize_AndLiesFlat()
        {
            var t = FoliageGeometry.Tire(1);
            Bounds(t, out var min, out var max);
            Assert.AreEqual(0f, min.Y, 0.01f);
            Assert.AreEqual(0.26f, max.Y, 0.01f);
            Assert.AreEqual(0.92f, max.X - min.X, 0.05f);
        }

        [Test]
        public void Bushes_AreLowAndCompact()
        {
            for (int v = 0; v < FoliageGeometry.BushVariants; v++)
            {
                var b = FoliageGeometry.Bush(v, 3);
                Bounds(b, out var min, out var max);
                Assert.That(max.Y, Is.InRange(0.3f, 1.6f), "arbusto variante " + v);
                Assert.That(max.X - min.X, Is.LessThan(2.6f));
            }
        }

        private static float[] Flat(MeshData m)
        {
            var r = new List<float>();
            foreach (var p in m.Positions) { r.Add(p.X); r.Add(p.Y); r.Add(p.Z); }
            return r.ToArray();
        }
    }
}
