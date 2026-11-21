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
        public void NoBackpack_ExceptTheMedicsMedicalPack()
        {
            foreach (var v in CharacterVariants.Defaults())
            {
                bool any = false;
                foreach (var p in Build(v)) if (p.Name.ToLowerInvariant().Contains("mochila")) { any = true; Assert.IsTrue(v.IsMedic, v.id + ": solo el médico lleva mochila (" + p.Name + ")"); }
                if (v.IsMedic) Assert.IsTrue(any, "el médico lleva su mochila médica");
            }
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

        // piezas de equipo que solo cambian en Sam: sin chaleco táctico y con collarín de la bota de trabajo
        private static readonly string[] SamGearOnly = { "chaleco", "portacargadores", "radio", "correas", "collarín" };
        private static bool SamOnly(ModelPiece p) { foreach (var n in SamGearOnly) if (p.Name.EndsWith(" " + n)) return true; return false; }

        [Test]
        public void SamDuarte_OnlyClothesChange_SameRigProportionsAndBody()
        {
            var sam = CharacterVariants.Find(CharacterVariants.Defaults(), "sam_duarte");
            Assert.IsNotNull(sam, "Sam Duarte en la selección");
            Assert.AreEqual("Sam Duarte", sam.displayName);
            Assert.IsTrue(sam.hiVis); Assert.AreEqual(Headgear.HoodCap, sam.headgear);
            Assert.AreEqual(1f, sam.buildScale, "mismas proporciones"); Assert.AreEqual(100f, sam.maxHealth);
            // mismo rig (huesos) y mismo cuerpo que una variante de referencia: mismas piezas y vértices fuera de la cabeza, salvo el equipo que solo cambia en Sam.
            // El agarre del arma y las hitboxes dependen solo de los huesos, las manos y estas mallas, así que no cambian.
            var refV = CharacterVariants.Find(CharacterVariants.Defaults(), "francotirador");
            var samAll = Build(sam); var refAll = Build(refV);
            var a = samAll.FindAll(p => p.Bone != BoneId.Head && !SamOnly(p)); var b = refAll.FindAll(p => p.Bone != BoneId.Head && !SamOnly(p));
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
            var samNames = samAll.ConvertAll(p => p.Name); var refNames = refAll.ConvertAll(p => p.Name);
            foreach (var n in new[] { "chaleco", "portacargadores", "correas" })
            {
                Assert.IsFalse(samNames.Exists(x => x.EndsWith(" " + n)), "Sam sin chaleco táctico: " + n);
                Assert.IsTrue(refNames.Exists(x => x.EndsWith(" " + n)), "las otras variantes conservan el chaleco: " + n);
            }
            foreach (var other in CharacterVariants.Defaults())
                if (!other.hiVis) Assert.IsTrue(Build(other).Exists(p => p.Name.EndsWith(" chaleco")), other.id + " conserva el chaleco");
            Assert.AreEqual(2, samNames.FindAll(x => x.EndsWith(" collarín")).Count, "botas de trabajo con collarín (separadas del pantalón)");
            Assert.AreEqual(2, samNames.FindAll(x => x.EndsWith(" bota")).Count, "botas con suela y cordones");
            var head = samAll.FindAll(p => p.Bone == BoneId.Head).ConvertAll(p => p.Name);
            CollectionAssert.IsSupersetOf(head, new[] { "Head pasamontañas", "Head capucha", "Head gorra", "Head pañuelo", "Head rostro" }, "capucha bajo la gorra, rostro y pañuelo");
            CollectionAssert.DoesNotContain(head, "Head casco");
            Assert.LessOrEqual(SoldierModel.Triangles(samAll), 15000, "presupuesto");
        }

        [Test]
        public void SamDuarte_HoodShadesEyes_NoseVisible_BandanaBelowNose()
        {
            var sam = CharacterVariants.Find(CharacterVariants.Defaults(), "sam_duarte");
            var pieces = Build(sam);
            ModelPiece P(string n) => pieces.Find(p => p.Name == "Head " + n);
            float MinY(ModelPiece p, float minZ) { float y = 9f; foreach (var q in p.Mesh.Positions) if (q.Z > minZ && q.Y < y) y = q.Y; return y; }
            float MaxY(ModelPiece p, float minZ) { float y = -9f; foreach (var q in p.Mesh.Positions) if (q.Z > minZ && q.Y > y) y = q.Y; return y; }
            float MaxZ(ModelPiece p) { float z = -9f; foreach (var q in p.Mesh.Positions) z = Math.Max(z, q.Z); return z; }
            float eyeY = SkullFace.ChinY + (SkullFace.BrowY - SkullFace.ChinY) * 0.7f, noseY = SkullFace.ChinY + (SkullFace.BrowY - SkullFace.ChinY) * 0.45f;
            var hood = P("capucha"); var face = P("rostro"); var band = P("pañuelo");
            // borde delantero de la capucha: sobre los ojos (deja la frente cubierta y los ojos en su sombra) y por delante de la cara
            float hoodEdge = MinY(hood, 0.11f);
            Assert.Greater(hoodEdge, eyeY, "la capucha no tapa los ojos");
            Assert.Less(hoodEdge, eyeY + 0.03f, "la capucha cubre la frente y deja los ojos en sombra");
            Assert.Greater(MaxZ(hood), MaxZ(face) - 0.02f, "la capucha sobresale sobre la cara");
            // nariz en relieve y pañuelo solo por debajo de la nariz
            Assert.Greater(MaxZ(face), SoldierModel.HeadCz + SoldierModel.HeadRz + 0.012f + 0.012f, "nariz en relieve");
            Assert.Less(MaxY(band, 0.06f), noseY, "el pañuelo queda bajo la nariz");
            // la textura del rostro: piel en las mejillas, ojos oscuros y frente en sombra
            var atlas = CharacterTextures.Generate(AtlasKind.Helmet, sam, 256);
            var cell = CharacterTextures.Cell(AtlasKind.Helmet, CharacterTextures.HSkull);
            float L(float u, float v)
            {
                var m = cell.Map(u, v); int x = (int)(m.X * 256), y = (int)(m.Y * 256);
                int i = (y * 256 + x) * 4; return (atlas.Albedo[i] + atlas.Albedo[i + 1] + atlas.Albedo[i + 2]) / 765f;
            }
            Assert.Greater(L(0.2f, 0.5f), 0.3f, "piel");
            Assert.Less(L(0.33f, 0.7f), L(0.2f, 0.5f) * 0.6f, "ojos");
            Assert.Less(L(0.5f, 0.92f), L(0.2f, 0.5f) * 0.75f, "frente en sombra");
        }

        [Test]
        public void SamDuarte_JacketAndTrousersOrangeWithReflectiveBands_HoodBlack()
        {
            var sam = CharacterVariants.Find(CharacterVariants.Defaults(), "sam_duarte");
            var a = CharacterTextures.Generate(AtlasKind.Uniform, sam, 128);
            int n = 128, half = n / 2;
            int Count(int x0, int y0, Func<float, float, float, bool> f)
            {
                int c = 0;
                for (int y = y0; y < y0 + half; y++)
                    for (int x = x0; x < x0 + half; x++) { int i = (y * n + x) * 4; if (f(a.Albedo[i] / 255f, a.Albedo[i + 1] / 255f, a.Albedo[i + 2] / 255f)) c++; }
                return c;
            }
            bool Orange(float r, float g, float bl) => r > 0.45f && r > g * 1.6f && r > bl * 3f;
            bool Silver(float r, float g, float bl) => r > 0.45f && Math.Abs(r - g) < 0.08f && Math.Abs(g - bl) < 0.08f;
            int cellPx = half * half;
            Assert.Greater(Count(0, 0, Orange), cellPx / 2, "chaqueta naranja (celda del torso y brazos)");
            Assert.Greater(Count(0, 0, Silver), cellPx / 20, "bandas reflectantes de la chaqueta");
            Assert.Greater(Count(half, 0, Orange), cellPx / 2, "pantalón naranja (celda de las piernas)");
            Assert.Greater(Count(half, 0, Silver), cellPx / 25, "bandas reflectantes del pantalón");
            Assert.Greater(Count(half, half, (r, g, bl) => r < 0.1f && g < 0.1f && bl < 0.1f), cellPx * 9 / 10, "capucha negra");
            Assert.IsTrue(CharacterTextures.InTrouserBand(0.64f) && !CharacterTextures.InTrouserBand(0.3f), "banda bajo la rodilla, por encima de la bota");
            Assert.IsTrue(CharacterTextures.InBand(0.33f) && CharacterTextures.InBand(0.66f) && !CharacterTextures.InBand(0.5f));
            // las otras variantes siguen con su camuflaje (sin naranja en las piernas)
            var other = CharacterTextures.Generate(AtlasKind.Uniform, CharacterVariants.Find(CharacterVariants.Defaults(), "francotirador"), 128);
            int o = 0; for (int y = 0; y < half; y++) for (int x = half; x < n; x++) { int i = (y * n + x) * 4; if (Orange(other.Albedo[i] / 255f, other.Albedo[i + 1] / 255f, other.Albedo[i + 2] / 255f)) o++; }
            Assert.Less(o, cellPx / 20, "pantalón naranja solo en Sam");
        }

        private static float[] Mean(byte[] a, int n, int cx, int cy, int grid)
        {
            int c = n / grid; double r = 0, g = 0, b = 0; int k = 0;
            for (int y = cy * c + c / 8; y < (cy + 1) * c - c / 8; y++)
                for (int x = cx * c + c / 8; x < (cx + 1) * c - c / 8; x++) { int i = (y * n + x) * 4; r += a[i]; g += a[i + 1]; b += a[i + 2]; k++; }
            return new[] { (float)(r / k / 255), (float)(g / k / 255), (float)(b / k / 255) };
        }

        [Test]
        public void Medic_Recognizable_SurgicalUniform_WhiteVestGreenCross_BlueLatex_LightBoots()
        {
            var med = CharacterVariants.Find(CharacterVariants.Defaults(), "medico");
            Assert.IsTrue(med.IsMedic); Assert.AreEqual(Headgear.MedicHelmet, med.headgear); Assert.IsTrue(med.armband);
            const int n = 128;
            var uni = CharacterTextures.Generate(AtlasKind.Uniform, med, n).Albedo;
            var torso = Mean(uni, n, 0, 0, 2);
            Assert.Greater(torso[1], torso[0] * 1.4f, "uniforme verde quirúrgico (verde azulado)"); Assert.Greater(torso[2], torso[0] * 1.3f);
            var gear = CharacterTextures.Generate(AtlasKind.Gear, med, n).Albedo;
            var vest = Mean(gear, n, CharacterTextures.GVest % 4, CharacterTextures.GVest / 4, 4);
            Assert.Greater((vest[0] + vest[1] + vest[2]) / 3f, 0.5f, "chaleco claro (blanco sucio)");
            var glove = Mean(gear, n, CharacterTextures.GGlove % 4, CharacterTextures.GGlove / 4, 4);
            Assert.Greater(glove[2], glove[0] * 2f, "guantes de látex azules");
            var boot = Mean(gear, n, CharacterTextures.GBoot % 4, CharacterTextures.GBoot / 4, 4);
            Assert.Greater((boot[0] + boot[1] + boot[2]) / 3f, 0.3f, "botas claras");
            // cruz propia: verde (no roja como la de la Cruz Roja) en el centro del brazalete
            int c = n / 4, ax = CharacterTextures.GArmband % 4, ay = CharacterTextures.GArmband / 4;
            int ci = ((ay * c + c / 2) * n + ax * c + c / 2) * 4;
            Assert.Greater(gear[ci + 1], gear[ci] * 2, "cruz verde"); Assert.Less(gear[ci], 120, "nada de cruz roja");
            // mochila médica con cruz, bolsas de suministros, rodilleras y gafas
            var names = Build(med).ConvertAll(p => p.Name);
            foreach (var part in new[] { "mochila médica", "cruz mochila", "bolsas médicas", "rodillera", "gafas", "brazalete" })
                Assert.IsTrue(names.Exists(x => x.EndsWith(" " + part)), "pieza " + part);
            Assert.LessOrEqual(SoldierModel.Triangles(Build(med)), 15000, "presupuesto");
        }

        [Test]
        public void Medic_DistinctColorsFromEveryOtherVariant()
        {
            const int n = 64;
            var all = CharacterVariants.Defaults();
            var med = CharacterVariants.Find(all, "medico");
            var mt = Mean(CharacterTextures.Generate(AtlasKind.Uniform, med, n).Albedo, n, 0, 0, 2);
            var mv = Mean(CharacterTextures.Generate(AtlasKind.Gear, med, n).Albedo, n, 0, 0, 4);
            foreach (var o in all)
            {
                if (o.id == med.id) continue;
                var ot = Mean(CharacterTextures.Generate(AtlasKind.Uniform, o, n).Albedo, n, 0, 0, 2);
                var ov = Mean(CharacterTextures.Generate(AtlasKind.Gear, o, n).Albedo, n, 0, 0, 4);
                float d = Math.Abs(mt[0] - ot[0]) + Math.Abs(mt[1] - ot[1]) + Math.Abs(mt[2] - ot[2]) + Math.Abs(mv[0] - ov[0]) + Math.Abs(mv[1] - ov[1]) + Math.Abs(mv[2] - ov[2]);
                Assert.Greater(d, 0.35f, "el médico se distingue de " + o.id);
            }
        }

        [Test]
        public void MedicAsset_MatchesCodeColors()
        {
            var med = CharacterVariants.Find(CharacterVariants.Defaults(), "medico");
            string y = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/Resources/Config/Variants/medico.asset"));
            foreach (var (k, v) in new[] { ("camoBase", med.camoBase), ("gear", med.gear), ("leather", med.leather), ("boot", med.boot), ("helmet", med.helmet), ("accent", med.accent) })
            {
                string block = "    " + k + ":\n    - " + v[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n    - " + v[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n    - " + v[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
                StringAssert.Contains(block, y.Replace("\r\n", "\n"), "medico.asset (manda sobre el código) con el color nuevo de " + k);
            }
        }
    }
}
