using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class SoldierModelTests
    {
        private static IEnumerable<CharacterVariantData> Variants() => CharacterVariants.Defaults();

        private static List<ModelPiece> Build(CharacterVariantData v)
        {
            var p = RigProportions.Soldier();
            return SoldierModel.Build(HumanoidRigSpec.Build(p), p, v);
        }

        [TestCaseSource(nameof(Variants))]
        public void EachVariant_WithinTriangleBudget_AndFourMaterialsMax(CharacterVariantData v)
        {
            var pieces = Build(v);
            int tris = SoldierModel.Triangles(pieces);
            Assert.LessOrEqual(tris, SoldierModel.TriangleBudget, v.id + ": " + tris + " triángulos");
            var slots = new HashSet<int>();
            foreach (var p in pieces) slots.Add(p.Slot);
            Assert.LessOrEqual(slots.Count, 4, "3-4 materiales");
            TestContext.WriteLine(v.id + ": " + tris + " triángulos, " + pieces.Count + " piezas");
        }

        [TestCaseSource(nameof(Variants))]
        public void EveryPiece_HasValidUvsNormalsAndTangents(CharacterVariantData v)
        {
            foreach (var p in Build(v))
            {
                var m = p.Mesh;
                Assert.Greater(m.Triangles.Count, 0, p.Name);
                Assert.AreEqual(m.Positions.Count, m.Normals.Count, p.Name);
                Assert.AreEqual(m.Positions.Count, m.Uvs.Count, p.Name);
                Assert.AreEqual(m.Positions.Count, m.Tangents.Count, p.Name);
                for (int i = 0; i < m.Positions.Count; i++)
                {
                    var uv = m.Uvs[i];
                    Assert.That(uv.X, Is.InRange(0f, 1f), p.Name + " u"); Assert.That(uv.Y, Is.InRange(0f, 1f), p.Name + " v");
                    Assert.AreEqual(1f, m.Normals[i].Length, 0.02f, p.Name + " normal");
                    Assert.AreEqual(1f, m.Tangents[i].Length, 0.02f, p.Name + " tangente");
                    float w = m.WOf(i);
                    Assert.IsTrue(w == 1f || w == -1f, p.Name + " signo de tangente");
                    Assert.IsFalse(float.IsNaN(m.Positions[i].X + m.Positions[i].Y + m.Positions[i].Z), p.Name);
                }
            }
        }

        [TestCaseSource(nameof(Variants))]
        public void ClosedPieces_FaceOutward(CharacterVariantData v)
        {
            foreach (var p in Build(v))
            {
                if (p.Slot == SoldierModel.SlotVisor) continue;   // lámina de una capa (material sin culling)
                Assert.Greater(Winding.SignedVolume(p.Mesh), 0f, p.Name + ": volumen negativo (caras hacia dentro)");
            }
        }

        [Test]
        public void Legs_KeepTheValidatedNamesAndKneeRadius()
        {
            var pieces = Build(CharacterVariants.Defaults()[0]);
            var names = new HashSet<string>();
            foreach (var p in pieces) names.Add(p.Name);
            foreach (var b in new[] { "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm" })
                Assert.IsTrue(names.Contains(b + " limb"), "falta " + b + " limb");
            // todo lo que rodea la rodilla cabe en la rótula (arrodillado, nada atraviesa el suelo)
            foreach (var p in pieces)
            {
                if (p.Bone != BoneId.LeftLowerLeg || (!p.Name.Contains("rodillera") && !p.Name.Contains("rótula"))) continue;
                foreach (var q in p.Mesh.Positions) Assert.LessOrEqual(new V3(q.X, Math.Min(0f, q.Y + 0.02f), q.Z).Length, 0.058f, p.Name);
            }
        }

        [Test]
        public void NoBackpack()
        {
            foreach (var v in CharacterVariants.Defaults())
                foreach (var p in Build(v)) StringAssert.DoesNotContain("mochila", p.Name.ToLowerInvariant());
        }

        [Test]
        public void SkullPlate_DesignHasSocketsNoseCheeksAndTeeth()
        {
            Assert.Less(SkullFace.Relief(0.37f, 0.71f), -0.008f, "cuencas hundidas");
            Assert.Less(SkullFace.Relief(0f, 0.54f), -0.004f, "nariz hundida");
            Assert.Greater(SkullFace.Relief(0.68f, 0.52f), SkullFace.Relief(0.52f, 0.36f), "pómulo por encima de la mejilla");
            Assert.Less(SkullFace.Detail(0.04f, 0.3f), SkullFace.Detail(0f, 0.3f), "surco entre dientes");
            Assert.AreEqual(SkullFace.Relief(0.3f, 0.5f), SkullFace.Relief(-0.3f, 0.5f), 1e-6f, "simétrica");
            Assert.Less(SkullFace.PaintCover(0.37f, 0.71f), 0.1f, "cuencas sin pintar");
            Assert.Greater(SkullFace.PaintCover(0.2f, 0.95f), 0.9f, "frente pintada");
        }

        [TestCase(AtlasKind.Uniform), TestCase(AtlasKind.Gear), TestCase(AtlasKind.Helmet), TestCase(AtlasKind.Weapon)]
        public void Atlas_IsDeterministic_AndHasAllMaps(AtlasKind kind)
        {
            var v = CharacterVariants.Defaults()[0];
            var a = CharacterTextures.Generate(kind, v, 128);
            var b = CharacterTextures.Generate(kind, v, 128);
            Assert.AreEqual(128 * 128 * 4, a.Albedo.Length); Assert.AreEqual(a.NormalSize * a.NormalSize * 4, a.Normal.Length); Assert.AreEqual(a.Albedo.Length, a.Mask.Length);
            CollectionAssert.AreEqual(a.Albedo, b.Albedo, "misma semilla, mismo resultado");
            // las normales apuntan hacia fuera de la superficie (z > 0.5 en el mapa)
            for (int i = 2; i < a.Normal.Length; i += 4) Assert.Greater(a.Normal[i], 127);
        }

        [Test]
        public void Variants_ValidateFromBrokenJson()
        {
            var v = new CharacterVariantData { id = "", camoBase = null, gear = new[] { 2f, -1f, 0.5f }, maxHealth = 9999f, moveSpeed = 0f, headgear = Headgear.CapGoggles, visor = true };
            v.Validate();
            Assert.IsNotEmpty(v.id); Assert.AreEqual(3, v.camoBase.Length);
            Assert.AreEqual(1f, v.gear[0]); Assert.AreEqual(0f, v.gear[1]);
            Assert.LessOrEqual(v.maxHealth, 140f); Assert.GreaterOrEqual(v.moveSpeed, 0.85f);
            Assert.IsFalse(v.visor, "el visor solo existe con la placa calavera");
            var ids = new HashSet<string>();
            foreach (var d in CharacterVariants.Defaults()) Assert.IsTrue(ids.Add(d.id), "ids únicos");
            Assert.GreaterOrEqual(ids.Count, 3);
        }

        [Test]
        public void SamDuarte_OnlyClothesChange_SameRigProportionsAndBody()
        {
            var sam = CharacterVariants.Find(CharacterVariants.Defaults(), "sam_duarte");
            Assert.IsNotNull(sam, "Sam Duarte en la selección");
            Assert.AreEqual("Sam Duarte", sam.displayName);
            Assert.IsTrue(sam.hiVis); Assert.AreEqual(Headgear.HoodCap, sam.headgear);
            Assert.AreEqual(1f, sam.buildScale, "mismas proporciones"); Assert.AreEqual(100f, sam.maxHealth);
            // mismo cuerpo que una variante de referencia (misma constitución, sin brazalete): mismas piezas, huesos y vértices fuera de la cabeza.
            // El agarre del arma y las hitboxes dependen solo de los huesos y de estas mallas, así que no cambian.
            var refV = CharacterVariants.Find(CharacterVariants.Defaults(), "francotirador");
            var a = Build(sam).FindAll(p => p.Bone != BoneId.Head); var b = Build(refV).FindAll(p => p.Bone != BoneId.Head);
            Assert.AreEqual(b.Count, a.Count, "mismas piezas del cuerpo");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(b[i].Name, a[i].Name); Assert.AreEqual(b[i].Bone, a[i].Bone, a[i].Name); Assert.AreEqual(b[i].Slot, a[i].Slot, a[i].Name);
                Assert.AreEqual(b[i].Mesh.Positions.Count, a[i].Mesh.Positions.Count, a[i].Name);
                for (int k = 0; k < a[i].Mesh.Positions.Count; k++)
                {
                    var pa = a[i].Mesh.Positions[k]; var pb = b[i].Mesh.Positions[k];
                    Assert.Less(Math.Abs(pa.X - pb.X) + Math.Abs(pa.Y - pb.Y) + Math.Abs(pa.Z - pb.Z), 1e-5f, a[i].Name + " vértice " + k);
                }
            }
            var head = Build(sam).FindAll(p => p.Bone == BoneId.Head).ConvertAll(p => p.Name);
            CollectionAssert.IsSupersetOf(head, new[] { "Head pasamontañas", "Head capucha", "Head gorra", "Head pañuelo" }, "capucha bajo la gorra y mascarilla");
            CollectionAssert.DoesNotContain(head, "Head casco");
        }

        [Test]
        public void SamDuarte_JacketIsOrangeWithReflectiveBands_AndDarkTrousers()
        {
            var sam = CharacterVariants.Find(CharacterVariants.Defaults(), "sam_duarte");
            var a = CharacterTextures.Generate(AtlasKind.Uniform, sam, 128);
            int orange = 0, silver = 0, darkPants = 0, n = 128;
            for (int y = 0; y < n / 2; y++)
                for (int x = 0; x < n / 2; x++)
                {
                    int i = (y * n + x) * 4; float r = a.Albedo[i] / 255f, g = a.Albedo[i + 1] / 255f, bl = a.Albedo[i + 2] / 255f;
                    if (r > 0.45f && r > g * 1.6f && r > bl * 3f) orange++;
                    if (r > 0.45f && Math.Abs(r - g) < 0.08f && Math.Abs(g - bl) < 0.08f) silver++;
                }
            for (int y = 0; y < n / 2; y++)
                for (int x = n / 2; x < n; x++) { int i = (y * n + x) * 4; if (a.Albedo[i] < 80 && a.Albedo[i + 1] < 80) darkPants++; }
            int cell = (n / 2) * (n / 2);
            Assert.Greater(orange, cell / 2, "chaqueta naranja (celda del torso y brazos)");
            Assert.Greater(silver, cell / 20, "bandas reflectantes");
            Assert.Greater(darkPants, cell * 3 / 4, "pantalón oscuro (celda de las piernas)");
            Assert.IsTrue(CharacterTextures.InBand(0.33f) && CharacterTextures.InBand(0.66f) && !CharacterTextures.InBand(0.5f));
        }
    }
}
