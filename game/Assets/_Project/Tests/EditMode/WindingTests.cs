using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>
    /// Verifica la convención de caras de Unity con el ejemplo del manual y la aplica a TODAS las mallas generadas por código:
    /// cada triángulo debe mirar hacia afuera (si no, el backface culling lo hace desaparecer visto desde fuera y se ve el
    /// interior del lado opuesto, es decir, el objeto parece transparente).
    /// </summary>
    public class WindingTests
    {
        [Test]
        public void UnityManualQuad_FrontNormalIsMinusZ()
        {
            // Manual de Unity, "Creating meshes": vértices 0:(0,0,0) 1:(w,0,0) 2:(0,h,0) 3:(w,h,0), triángulos {0,2,1, 2,3,1},
            // normales = -Vector3.forward. Si Winding.FrontNormal no da -Z aquí, la convención está mal.
            var v0 = new V3(0, 0, 0); var v1 = new V3(2, 0, 0); var v2 = new V3(0, 3, 0); var v3 = new V3(2, 3, 0);
            V3 n1 = Winding.FrontNormal(v0, v2, v1);
            V3 n2 = Winding.FrontNormal(v2, v3, v1);
            Assert.AreEqual(-1f, n1.Normalized.Z, 1e-5f);
            Assert.AreEqual(-1f, n2.Normalized.Z, 1e-5f);
            Assert.IsTrue(Winding.FacesOutward(v0, v2, v1, new V3(0, 0, -1)));
            Assert.IsFalse(Winding.FacesOutward(v0, v2, v1, new V3(0, 0, 1)));
        }

        [Test]
        public void UnityManualQuad_UvIsNotMirrored()
        {
            // En ese quad (visto desde -Z) u crece hacia +X (derecha) y v hacia +Y (arriba): Cross(dP/du, dP/dv) = +Z, contra la normal (-Z)
            V3 T = new V3(1, 0, 0), B = new V3(0, 1, 0), n = new V3(0, 0, -1);
            Assert.Less(V3.Dot(V3.Cross(T, B), n), 0f);
        }

        public static IEnumerable<TestCaseData> ClosedMeshes()
        {
            // (malla, mínimo del coseno entre la normal del vértice y la del triángulo: 0.9 en piezas planas, menos en las suaves)
            yield return new TestCaseData(BoxGeometry.Build(new V3(3.5f, 1.2f, 0.6f), 2f, new V2(0, 0), 0f), 0.9f).SetName("Caja sin bisel");
            yield return new TestCaseData(BoxGeometry.Build(new V3(3.5f, 1.2f, 0.6f), 2f, new V2(0, 0), 0.05f), 0.9f).SetName("Caja con bisel");
            yield return new TestCaseData(BoxGeometry.Build(new V3(1.2f, 1.2f, 1.2f), 1.2f, new V2(0, 0), 0.025f), 0.9f).SetName("Caja cúbica con bisel");
            yield return new TestCaseData(BoxGeometry.Build(new V3(2.5f, 2.6f, 6f), 2.6f, new V2(0.3f, 0), 0.025f), 0.9f).SetName("Contenedor");
            yield return new TestCaseData(BoxGeometry.Build(new V3(61f, 4f, 1f), 3f, new V2(0, 0), 0.03f), 0.9f).SetName("Muro largo");
            yield return new TestCaseData(BoxGeometry.Build(new V3(0.4f, 0.02f, 3f), 1f, new V2(0, 0), 0.03f), 0.9f).SetName("Placa fina de caja (bisel recortado)");
            foreach (SignType t in Enum.GetValues(typeof(SignType)))
                yield return new TestCaseData(SignGeometry.Plate(t), 0.9f).SetName("Placa de señal " + t);
            yield return new TestCaseData(FoliageGeometry.Tire(1), 0.7f).SetName("Neumático");
            yield return new TestCaseData(FoliageGeometry.Bag(1), 0.2f).SetName("Bolsa");
            yield return new TestCaseData(FoliageGeometry.Bush(0, 3), 0.2f).SetName("Arbusto de bultos");
            yield return new TestCaseData(LimbGeometry.Segment(0.41f, new[] { 0f, 0.0875f, 0.5f, 0.08f, 0.92f, 0.055f, 1f, 0.052f }, 10, 1.08f), 0.5f).SetName("Muslo (pivote en la cadera)");
            yield return new TestCaseData(LimbGeometry.Segment(0.38f, new[] { 0f, 0.052f, 0.3f, 0.062f, 0.65f, 0.05f, 1f, 0.045f }, 10, 1.05f), 0.5f).SetName("Pantorrilla con gemelo");
            yield return new TestCaseData(MeshBuilder.LatheData(new[] { -0.1f, 0.03f, 0.1f, 0.03f }, 12, 1f, true, true), 0.5f).SetName("Torno: cilindro con tapas");
            yield return new TestCaseData(MeshBuilder.LatheData(new[] { -0.2f, 0.12f, 0f, 0.16f, 0.2f, 0.13f }, 14, 0.65f, true, true), 0.3f).SetName("Torno: torso ovalado");
            yield return new TestCaseData(MeshBuilder.DomeData(new V3(0.12f, 0.11f, 0.13f), lon => -10f, 0.01f, 20, 8), 0.2f).SetName("Cúpula con grosor (casco)");
            yield return new TestCaseData(MeshBuilder.DomeData(new V3(0.06f, 0.05f, 0.04f), lon => 0f, 0.008f, 12, 5, -90f, 90f), 0.2f).SetName("Media cúpula (rodillera)");
            yield return new TestCaseData(MeshBuilder.Sheet((u, v, side) => new V3(u * 0.2f - 0.1f, v * 0.15f, side > 0 ? 0.01f * (float)System.Math.Sin(u * 6f) : -0.006f), 12, 10, true), 0.2f).SetName("Lámina con relieve y grosor");
        }

        [TestCaseSource(nameof(ClosedMeshes))]
        public void ClosedMesh_HasPositiveSignedVolume_AllTrianglesPointOutward(MeshData m, float minDot)
        {
            Assert.Greater(Winding.SignedVolume(m), 0f, "volumen negativo: la malla está al revés (se vería transparente desde fuera)");

            // y cada triángulo mira hacia afuera del centro de su malla cerrada y coincide con su normal
            V3 center = new V3(0, 0, 0);
            foreach (var p in m.Positions) center = center + p;
            center = center * (1f / m.Positions.Count);
            int bad = 0;
            for (int i = 0; i < m.Triangles.Count; i += 3)
            {
                V3 a = m.Positions[m.Triangles[i]], b = m.Positions[m.Triangles[i + 1]], c = m.Positions[m.Triangles[i + 2]];
                V3 g = Winding.FrontNormal(a, b, c);
                if (g.Length < 1e-9f) continue; // triángulo degenerado (bisel recortado): no se ve
                if (V3.Dot(g.Normalized, m.Normals[m.Triangles[i]]) < minDot) bad++;
            }
            Assert.AreEqual(0, bad, "triángulos cuya normal frontal no coincide con la normal del vértice");
        }

        [Test]
        public void ConvexBoxes_EveryTriangleFacesAwayFromTheCenter()
        {
            foreach (float bevel in new[] { 0f, 0.04f })
            {
                var m = BoxGeometry.Build(new V3(2.5f, 2.6f, 6f), 2.6f, new V2(0, 0), bevel);
                for (int i = 0; i < m.Triangles.Count; i += 3)
                {
                    V3 a = m.Positions[m.Triangles[i]], b = m.Positions[m.Triangles[i + 1]], c = m.Positions[m.Triangles[i + 2]];
                    V3 centroid = (a + b + c) * (1f / 3f); // la caja está centrada en el origen
                    Assert.IsTrue(Winding.FacesOutward(a, b, c, centroid), "triángulo " + i / 3 + " mira hacia adentro (bisel " + bevel + ")");
                }
            }
        }

        [Test]
        public void BoxVolume_MatchesTheDimensions()
        {
            var m = BoxGeometry.Build(new V3(3f, 2f, 1.5f), 1f, new V2(0, 0), 0f);
            Assert.AreEqual(3f * 2f * 1.5f, Winding.SignedVolume(m), 1e-3f);
        }

        [Test]
        public void Tangents_UseUnityHandedness_ForBoxesAndPlates()
        {
            // bitangente = Cross(normal, tangente) * w debe ser la dirección en la que crece v
            var box = BoxGeometry.Build(new V3(2f, 1f, 3f), 1f, new V2(0, 0), 0f);
            Assert.AreEqual(-1f, box.TangentSign);
            for (int f = 0; f < 6; f++)
            {
                int i0 = f * 4;
                V3 B = (box.Positions[i0 + 3] - box.Positions[i0]).Normalized; // dirección de v en esta cara
                V3 bit = V3.Cross(box.Normals[i0], box.Tangents[i0]) * box.TangentSign;
                Assert.Greater(V3.Dot(bit, B), 0.999f, "cara " + f + ": el mapa de normales se iluminaría al revés");
            }
            var plate = SignGeometry.Plate(SignType.Stop);
            Assert.AreEqual(-1f, plate.TangentSign);
            V3 pb = V3.Cross(plate.Normals[0], plate.Tangents[0]) * plate.TangentSign;
            Assert.Greater(pb.Y, 0.999f, "en la placa v apunta hacia arriba");
        }
    }
}
