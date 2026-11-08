using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Mallas reales del zombi (puras): presupuesto de triángulos por tipo y nivel de detalle, UV y tangentes válidas, caras hacia fuera y nada bajo el suelo.</summary>
    public class ZombieModelTests
    {
        public static IEnumerable<ZombieVariantData> Variants() => ZombieVariants.Defaults();

        private static ZombieModelResult Build(ZombieVariantData v, int lod, int bucket = 1) => ZombieModel.Build(ZombieAnatomies.For(v.typeId, bucket), v, lod);

        /// <summary>Posición de un vértice de la pieza en el espacio del cuerpo, en la pose de reposo.</summary>
        public static V3 RestPos(ZombieModelResult r, ZombiePiece p, V3 v)
        {
            V3 origin = p.Extra >= 0
                ? HumanoidRigSpec.RestPosition(r.Spec, r.Extras[p.Extra].Parent) + r.Extras[p.Extra].LocalPos
                : HumanoidRigSpec.RestPosition(r.Spec, p.Bone);
            return origin + p.Pivot + v;
        }

        [TestCaseSource(nameof(Variants))]
        public void Lod0_WithinTriangleBudget(ZombieVariantData v)
        {
            int tris = Build(v, 0).Triangles();
            TestContext.WriteLine(v.id + " LOD0: " + tris + " triángulos");
            Assert.LessOrEqual(tris, ZombieModel.Budget(v.typeId), v.id);
        }

        [TestCaseSource(nameof(Variants))]
        public void Lods_ReduceTriangles(ZombieVariantData v)
        {
            int t0 = Build(v, 0).Triangles(), t1 = Build(v, 1).Triangles(), t2 = Build(v, 2).Triangles();
            TestContext.WriteLine(v.id + ": LOD0 " + t0 + ", LOD1 " + t1 + ", LOD2 " + t2);
            Assert.Less(t1, t0 * 0.7f, v.id + " LOD1");
            Assert.Less(t2, t1 * 0.6f, v.id + " LOD2");
        }

        [TestCaseSource(nameof(Variants))]
        public void EveryPiece_HasValidUvsNormalsAndTangents(ZombieVariantData v)
        {
            for (int lod = 0; lod < 3; lod++)
                foreach (var p in Build(v, lod).Pieces)
                {
                    var m = p.Mesh;
                    Assert.Greater(m.Triangles.Count, 0, p.Name);
                    Assert.AreEqual(m.Positions.Count, m.Normals.Count, p.Name); Assert.AreEqual(m.Positions.Count, m.Uvs.Count, p.Name); Assert.AreEqual(m.Positions.Count, m.Tangents.Count, p.Name);
                    for (int i = 0; i < m.Positions.Count; i++)
                    {
                        var uv = m.Uvs[i];
                        Assert.That(uv.X, Is.InRange(-1e-4f, 1f + 1e-4f), p.Name + " u"); Assert.That(uv.Y, Is.InRange(-1e-4f, 1f + 1e-4f), p.Name + " v");
                        Assert.AreEqual(1f, m.Normals[i].Length, 0.02f, p.Name + " normal"); Assert.AreEqual(1f, m.Tangents[i].Length, 0.02f, p.Name + " tangente");
                        Assert.IsFalse(float.IsNaN(m.Positions[i].X + m.Positions[i].Y + m.Positions[i].Z), p.Name);
                    }
                    foreach (int t in m.Triangles) Assert.That(t, Is.InRange(0, m.Positions.Count - 1), p.Name);
                }
        }

        [TestCaseSource(nameof(Variants))]
        public void ClosedPieces_FaceOutward(ZombieVariantData v)
        {
            foreach (var p in Build(v, 0).Pieces) Assert.Greater(Winding.SignedVolume(p.Mesh), 0f, v.id + " " + p.Name + ": volumen negativo (caras hacia dentro)");
        }

        [TestCaseSource(nameof(Variants))]
        public void NothingBelowTheGround_AndHeightMatchesTheProportions(ZombieVariantData v)
        {
            for (int bucket = 0; bucket < 3; bucket++)
                for (int lod = 0; lod < 3; lod++)
                {
                    var r = Build(v, lod, bucket);
                    float minY = float.MaxValue, maxY = float.MinValue;
                    foreach (var p in r.Pieces)
                        foreach (var q in p.Mesh.Positions) { float y = RestPos(r, p, q).Y; minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                    float h = ZombieAnatomies.For(v.typeId, bucket).Proportions.height;
                    Assert.GreaterOrEqual(minY, -0.012f * h, v.id + " LOD" + lod + ": vértice bajo el suelo (" + minY.ToString("0.000") + ")");
                    Assert.LessOrEqual(minY, 0.02f * h, v.id + " LOD" + lod + ": el cuerpo flota (" + minY.ToString("0.000") + ")");
                    Assert.That(maxY, Is.InRange(h * 0.97f, h * 1.12f), v.id + " LOD" + lod + ": altura " + maxY.ToString("0.00") + " frente a " + h.ToString("0.00"));
                }
        }

        [TestCaseSource(nameof(Variants))]
        public void UsesTwoMaterialsOnly_AndSkinHasTheBody(ZombieVariantData v)
        {
            var r = Build(v, 0);
            var slots = new HashSet<int>();
            foreach (var p in r.Pieces) slots.Add(p.Slot);
            Assert.LessOrEqual(slots.Count, 2, "1-2 materiales");
            Assert.Greater(r.Triangles(ZombieModel.SlotSkin), 2000, "la piel lleva el cuerpo");
        }

        [Test]
        public void Tank_RightArmIsMuchBigger_AndHasScarPlates()
        {
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Tank)[0];
            var r = Build(v, 0);
            float Width(string bone) { float w = 0f; foreach (var p in r.Pieces) if (p.Name == bone + " brazo") foreach (var q in p.Mesh.Positions) w = Math.Max(w, Math.Abs(q.X)); return w; }
            Assert.Greater(Width("RightUpperArm"), Width("LeftUpperArm") * 1.35f, "brazo derecho mucho más grande");
            Assert.IsTrue(r.Pieces.Exists(p => p.Name.EndsWith("placas")), "placas de tejido cicatrizado");
        }

        [Test]
        public void Explosive_HasAnInflatableBellyBone()
        {
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Explosive)[0];
            var r = Build(v, 0);
            Assert.AreEqual(1, r.Extras.Count);
            Assert.IsTrue(r.Pieces.Exists(p => p.Extra == 0 && p.Name.Contains("vientre")));
            Assert.AreEqual(0, Build(ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Common)[0], 0).Extras.Count);
        }

        [Test]
        public void Armored_EveryArmorStyleAddsPieces_AndHeadStaysExposed()
        {
            foreach (var v in ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Armored))
            {
                var r = Build(v, 0);
                int armorTris = 0;
                foreach (var p in r.Pieces) if (p.Slot == ZombieModel.SlotCloth && (p.Name.Contains("placas") || p.Name.Contains("puerta") || p.Name.Contains("neumatico") || p.Name.Contains("chapa") || p.Name.Contains("hombrera"))) armorTris += p.Mesh.Triangles.Count / 3;
                Assert.Greater(armorTris, 40, v.id + ": sin armadura");
                foreach (var p in r.Pieces)
                {
                    if (!p.Name.Contains("placas") && !p.Name.Contains("puerta") && !p.Name.Contains("neumatico") && !p.Name.Contains("chapa")) continue;
                    Assert.AreNotEqual(BoneId.Head, p.Bone, v.id + ": la armadura no cubre la cabeza");
                }
            }
        }

        [Test]
        public void HeadStyles_ProduceDifferentSkulls()
        {
            var seen = new HashSet<string>();
            foreach (var v in ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Common))
            {
                var r = Build(v, 0);
                var sb = new System.Text.StringBuilder();
                foreach (var p in r.Pieces) if (p.Bone == BoneId.Head) { sb.Append(p.Name).Append(p.Mesh.Triangles.Count); double sum = 0; foreach (var q in p.Mesh.Positions) sum += q.X * 3.1 + q.Y * 5.7 + q.Z * 7.3; sb.Append('@').Append(Math.Round(sum, 3)).Append(';'); }
                seen.Add(sb.ToString());
            }
            Assert.AreEqual(4, seen.Count, "cada variante común tiene una cabeza distinta");
        }

        [Test]
        public void TextureBudget_IsDocumented()
        {
            // atlas de piel (por tipo) + ropa + heridas: 512 x 512 color BC (0.17 MB con mips ~0.35) + normales 256 x 256 RGBA (0.35 MB) + máscara BC (0.35 MB)
            double perAtlasMb = 512.0 * 512 * 1 * 1.33 / 1048576 * 2 + 256.0 * 256 * 4 * 1.33 / 1048576;
            double total = perAtlasMb * (ZombieAnatomies.Types.Length + 2);
            TestContext.WriteLine("Memoria de texturas de zombis ~" + total.ToString("0.0") + " MB (" + (ZombieAnatomies.Types.Length + 2) + " atlas de 512)");
            Assert.Less(total, 12.0);
        }
    }
}
