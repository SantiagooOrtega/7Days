using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>La complexión fuerte frente a la esbelta anterior: los porcentajes pedidos y que no cambian altura, largo de brazos y piernas ni el presupuesto.</summary>
    public class BuildProportionTests
    {
        private static RigProportions Slim() => new RigProportions { height = 1.82f, shoulderWidth = RigProportions.SlimShoulders, hipWidth = RigProportions.SlimHips, bulk = RigProportions.SlimBulk, torsoDepth = 0.9f, limbThickness = RigProportions.SlimLimbs };

        private static float Extent(List<ModelPiece> pieces, string contains, int axis)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in pieces)
            {
                if (!p.Name.Contains(contains)) continue;
                foreach (var q in p.Mesh.Positions) { float v = q[axis]; if (v < lo) lo = v; if (v > hi) hi = v; }
            }
            return hi - lo;
        }

        private static float MaxRadius(List<ModelPiece> pieces, string name, float aboveY)
        {
            float r = 0f;
            foreach (var p in pieces)
                if (p.Name == name) foreach (var q in p.Mesh.Positions) if (q.Y > aboveY) r = System.Math.Max(r, new V3(q.X, 0, q.Z).Length);
            return r;
        }

        [Test]
        public void StrongBuild_IsRoughly12To15PercentWiderAndDeeperTorso_Shoulders9_Limbs11()
        {
            var slim = Slim(); var strong = RigProportions.Soldier();
            var v = CharacterVariants.Defaults()[1];   // Explorador sin el grosor propio: se compara la base
            v.buildScale = 1f;
            var a = SoldierModel.Build(HumanoidRigSpec.Build(slim), slim, v);
            var b = SoldierModel.Build(HumanoidRigSpec.Build(strong), strong, v);
            float tw = Extent(b, "torso", 0) / Extent(a, "torso", 0), td = Extent(b, "torso", 2) / Extent(a, "torso", 2);
            Assert.That(tw, Is.InRange(1.12f, 1.15f), "ancho del torso x" + tw);
            Assert.That(td, Is.InRange(1.12f, 1.15f), "fondo del torso x" + td);
            Assert.That(strong.shoulderWidth / slim.shoulderWidth, Is.InRange(1.08f, 1.10f), "hombros");
            float arm = MaxRadius(b, "LeftUpperArm limb", -0.2f) / MaxRadius(a, "LeftUpperArm limb", -0.2f);
            float thigh = MaxRadius(b, "LeftUpperLeg limb", -0.15f) / MaxRadius(a, "LeftUpperLeg limb", -0.15f);
            Assert.That(arm, Is.InRange(1.10f, 1.12f), "brazos x" + arm);
            Assert.That(thigh, Is.InRange(1.00f, 1.12f), "muslos x" + thigh + " (más abajo se estrechan al radio de la rótula)");
            float neck = MaxRadius(b, "Neck braga", -1f) / MaxRadius(a, "Neck braga", -1f);
            Assert.That(neck, Is.InRange(1.05f, 1.2f), "cuello x" + neck);
        }

        [Test]
        public void SameHeight_SameLimbLengths_SoCameraGripAndFeetAreUnchanged()
        {
            var sa = HumanoidRigSpec.Build(Slim()); var sb = HumanoidRigSpec.Build(RigProportions.Soldier());
            foreach (var id in new[] { BoneId.LeftUpperLeg, BoneId.LeftLowerLeg, BoneId.LeftUpperArm, BoneId.LeftLowerArm, BoneId.Neck, BoneId.Head })
                Assert.AreEqual(sa[(int)id].Length, sb[(int)id].Length, 1e-5f, id.ToString());
            Assert.AreEqual(sa[(int)BoneId.Hips].LocalPosition.Y, sb[(int)BoneId.Hips].LocalPosition.Y, 1e-5f, "altura de la cadera");
        }

        [Test]
        public void EveryVariant_StaysWithinBudget_AndCalaveraIsTheBulkiest()
        {
            var baseP = RigProportions.Soldier();
            float calavera = 0f, others = 0f;
            foreach (var v in CharacterVariants.Defaults())
            {
                var p = baseP.WithBuild(v.buildScale);
                int tris = SoldierModel.Triangles(SoldierModel.Build(HumanoidRigSpec.Build(p), p, v));
                Assert.LessOrEqual(tris, SoldierModel.TriangleBudget, v.id);
                if (v.id == "calavera") calavera = v.buildScale; else others = System.Math.Max(others, v.buildScale);
            }
            Assert.Greater(calavera, others, "la Calavera es algo más corpulenta que el resto");
            Assert.LessOrEqual(calavera, 1.06f, "pero sin exagerar");
        }

        [Test]
        public void WithBuild_ClampsAndDoesNotTouchTheOriginal()
        {
            var p = RigProportions.Soldier(); float bulk = p.bulk;
            var c = p.WithBuild(5f);
            Assert.AreEqual(bulk, p.bulk, "no toca el original");
            Assert.LessOrEqual(c.bulk, 2.5f);
        }
    }
}
