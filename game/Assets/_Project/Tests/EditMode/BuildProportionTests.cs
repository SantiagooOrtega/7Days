using System;
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

        private static RigProportions V2() => new RigProportions { height = 1.82f, shoulderWidth = RigProportions.V2Shoulders, hipWidth = RigProportions.SoldierHips, bulk = RigProportions.SoldierBulk, torsoDepth = 0.9f, limbThickness = RigProportions.V2Limbs };

        [Test]
        public void MuscularBuild_TorsoWiderAndDeeper_ShouldersWider_LimbsAnother10Percent()
        {
            var slim = Slim(); var now = RigProportions.Soldier();
            var v = CharacterVariants.Defaults()[1]; v.buildScale = 1f;
            var a = SoldierModel.Build(HumanoidRigSpec.Build(slim), slim, v);
            var b = SoldierModel.Build(HumanoidRigSpec.Build(now), now, v);
            float tw = Extent(b, "torso", 0) / Extent(a, "torso", 0), td = Extent(b, "torso", 2) / Extent(a, "torso", 2);
            TestContext.WriteLine("torso x" + tw + " fondo x" + td);
            Assert.That(tw, Is.InRange(1.10f, 1.20f), "ancho del torso x" + tw);
            Assert.That(td, Is.InRange(1.10f, 1.25f), "fondo del torso x" + td + " (incluye el pectoral)");
            Assert.That(now.WithBuild(1.04f).shoulderWidth / slim.shoulderWidth, Is.InRange(1.12f, 1.16f), "hombros de la Calavera frente a la esbelta");
            // brazos y piernas "otro +10 a 12 %" sobre la versión anterior: mismo modelo, limbThickness 0.905 -> 1.0
            var prev = V2();
            var c = SoldierModel.Build(HumanoidRigSpec.Build(prev), prev, v);
            float arm = Extent(b, "RightUpperArm limb", 0) / Extent(c, "RightUpperArm limb", 0);
            float calf = Extent(b, "RightLowerLeg limb", 0) / Extent(c, "RightLowerLeg limb", 0);
            Assert.That(arm, Is.InRange(1.09f, 1.13f), "brazos x" + arm);
            Assert.That(calf, Is.InRange(1.05f, 1.13f), "pantorrillas x" + calf);
            Assert.That(now.limbThickness / prev.limbThickness, Is.InRange(1.10f, 1.12f));
        }

        [Test]
        public void Migrate_FromSlimAndFromV2_ToTheMuscularBuild_KeepsHandEdits()
        {
            foreach (var old in new[] { Slim(), V2() })
            {
                old.headSize = 1.1f;   // edición a mano: se respeta
                SevenDays.Procedural.RigProportions.Soldier();
                var m = old;
                Migrate(m);
                Assert.AreEqual(RigProportions.SoldierLimbs, m.limbThickness, 1e-4f);
                Assert.AreEqual(RigProportions.SoldierShoulders, m.shoulderWidth, 1e-4f);
                Assert.AreEqual(RigProportions.SoldierBulk, m.bulk, 1e-4f);
                Assert.AreEqual(1.1f, m.headSize, 1e-4f);
            }
            var custom = RigProportions.Soldier(); custom.limbThickness = 0.8f;
            Migrate(custom);
            Assert.AreEqual(0.8f, custom.limbThickness, 1e-4f, "un valor propio no se toca");
        }

        // la migración vive en Assembly-CSharp (ProportionSettings); aquí se replica la misma regla pura para poder probarla sin Unity
        private static void Migrate(RigProportions p)
        {
            const float e = 0.0015f;
            if (Math.Abs(p.shoulderWidth - RigProportions.SlimShoulders) < e || Math.Abs(p.shoulderWidth - RigProportions.V2Shoulders) < e) p.shoulderWidth = RigProportions.SoldierShoulders;
            if (Math.Abs(p.hipWidth - RigProportions.SlimHips) < e) p.hipWidth = RigProportions.SoldierHips;
            if (Math.Abs(p.bulk - RigProportions.SlimBulk) < e) p.bulk = RigProportions.SoldierBulk;
            if (Math.Abs(p.limbThickness - RigProportions.SlimLimbs) < e || Math.Abs(p.limbThickness - RigProportions.V2Limbs) < e) p.limbThickness = RigProportions.SoldierLimbs;
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
