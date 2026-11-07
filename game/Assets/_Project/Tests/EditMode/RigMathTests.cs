using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class RigMathTests
    {
        // ------------------------------------------------------------ suavizado crítico
        [Test]
        public void CriticalSpring_ReachesTargetWithoutOvershoot()
        {
            var s = new CriticalSpring(0f);
            float max = 0f;
            for (int i = 0; i < 120; i++) { s.Step(1f, 0.15f, 1f / 60f); max = Math.Max(max, s.Value); }
            Assert.AreEqual(1f, s.Value, 1e-3f);
            Assert.LessOrEqual(max, 1f + 1e-5f, "amortiguación crítica: nunca se pasa del objetivo");
        }

        [Test]
        public void CriticalSpring_IsFrameRateIndependentAndStable()
        {
            var a = new CriticalSpring(0f); var b = new CriticalSpring(0f);
            for (int i = 0; i < 30; i++) a.Step(1f, 0.2f, 1f / 30f);
            for (int i = 0; i < 120; i++) b.Step(1f, 0.2f, 1f / 120f);
            Assert.AreEqual(a.Value, b.Value, 0.01f, "mismo resultado a 30 y a 120 FPS");
            var c = new CriticalSpring(0f); c.Step(1f, 0.1f, 5f);   // un tirón enorme de un frame
            Assert.That(c.Value, Is.InRange(0.99f, 1.0001f), "un dt gigante no explota");
        }

        [Test]
        public void CriticalSpring_NoJumpWhenTargetChanges()
        {
            var s = new CriticalSpring(0f);
            for (int i = 0; i < 10; i++) s.Step(1f, 0.2f, 1f / 60f);
            float before = s.Value;
            s.Step(-1f, 0.2f, 1f / 60f);   // el objetivo cambia de golpe
            Assert.Less(Math.Abs(s.Value - before), 0.05f, "la pose no salta aunque el objetivo sí");
        }

        [Test]
        public void CriticalSpring_AngleTakesTheShortWay()
        {
            var s = new CriticalSpring(170f);
            for (int i = 0; i < 120; i++) s.StepAngle(-170f, 0.1f, 1f / 60f);
            Assert.AreEqual(190f, s.Value, 0.1f, "de 170 a -170 por 180, no dando la vuelta");
        }

        // ------------------------------------------------------------ IK
        private static float Dist(V3 a, V3 b) => (a - b).Length;

        [Test]
        public void TwoBoneIK_ReachesTargetKeepsLengthsAndBendsTowardPole()
        {
            var root = new V3(0, 1, 0); var target = new V3(0.1f, 0.2f, 0.2f); var pole = new V3(0, 0.6f, 1f);
            float reach = TwoBoneIK.Solve(root, target, pole, 0.45f, 0.42f, out V3 mid, out V3 end);
            Assert.AreEqual(0f, Dist(end, target), 1e-4f);
            Assert.AreEqual(0.45f, Dist(root, mid), 1e-4f); Assert.AreEqual(0.42f, Dist(mid, end), 1e-4f);
            Assert.Greater(mid.Z, 0.1f, "la rodilla se dobla hacia el pole (delante)");
            Assert.Less(reach, 1f);
        }

        [Test]
        public void TwoBoneIK_ClampsUnreachableTargets_NeverStretches()
        {
            var root = new V3(0, 1, 0);
            TwoBoneIK.Solve(root, new V3(0, -5, 0), new V3(0, 0, 1), 0.45f, 0.42f, out V3 mid, out V3 end);
            Assert.AreEqual(0.45f, Dist(root, mid), 1e-4f); Assert.AreEqual(0.42f, Dist(mid, end), 1e-4f);
            Assert.LessOrEqual(Dist(root, end), 0.87f);
            // demasiado cerca: tampoco se rompe
            TwoBoneIK.Solve(root, root, new V3(0, 0, 1), 0.45f, 0.42f, out mid, out end);
            Assert.AreEqual(0.45f, Dist(root, mid), 1e-3f); Assert.AreEqual(0.42f, Dist(mid, end), 1e-3f);
            Assert.IsFalse(float.IsNaN(mid.X) || float.IsNaN(end.Y));
        }

        [Test]
        public void TwoBoneIK_DegeneratePoleStillGivesAValidBend()
        {
            var root = new V3(0, 1, 0);
            TwoBoneIK.Solve(root, new V3(0, 0.3f, 0), new V3(0, -2, 0), 0.45f, 0.42f, out V3 mid, out V3 end);   // pole en la misma recta
            Assert.AreEqual(0.45f, Dist(root, mid), 1e-3f);
            Assert.IsFalse(float.IsNaN(mid.X));
        }

        // ------------------------------------------------------------ zancada
        [Test]
        public void Gait_IsDrivenByDistance_SoFeetDoNotSkate()
        {
            // misma distancia = misma fase, vaya rápido o despacio (el pie apoyado no se desliza respecto al suelo)
            var slow = new GaitCycle(); var fast = new GaitCycle();
            float len = GaitCycle.CycleLength(3f);
            for (int i = 0; i < 100; i++) slow.Advance(0.01f, len);
            for (int i = 0; i < 10; i++) fast.Advance(0.1f, len);
            Assert.AreEqual(slow.Phase, fast.Phase, 1e-4f);
            var still = new GaitCycle(); still.Advance(0f, len);
            Assert.AreEqual(0f, still.Phase, "quieto, la fase no avanza");
        }

        [Test]
        public void Gait_AlternatesFeetTwicePerCycle()
        {
            var g = new GaitCycle(); float len = 2f; int left = 0, right = 0;
            for (int i = 0; i < 402; i++) { int f = g.Advance(0.02f, len); if (f == 0) left++; if (f == 1) right++; }
            Assert.AreEqual(4, left); Assert.AreEqual(4, right, "8.04 m / 2 m por ciclo = 4 pasos de cada pie");
            Assert.AreEqual(0.5f, Math.Abs(g.FootPhase(0) - g.FootPhase(1)), 1e-4f);
        }

        [Test]
        public void Gait_StrideGrowsWithSpeed_AndRunningHasLessStance()
        {
            Assert.Greater(GaitCycle.CycleLength(6.8f), GaitCycle.CycleLength(1.6f));
            Assert.Less(GaitCycle.StanceFraction(6.8f), GaitCycle.StanceFraction(1.6f));
            Assert.Less(GaitCycle.CycleLength(6.8f, 1f, 0.12f), GaitCycle.CycleLength(6.8f), "con fatiga, pasos más cortos");
            Assert.That(GaitCycle.StanceFraction(0f), Is.InRange(0.55f, 0.65f));
        }

        [Test]
        public void Swing_StartsAndEndsOnTheGround()
        {
            var from = new V3(0, 0, 0); var to = new V3(0, 0, 1);
            Assert.AreEqual(0f, FootPlacement.Swing(from, to, 0f, 0.2f).Y, 1e-5f);
            Assert.AreEqual(0f, FootPlacement.Swing(from, to, 1f, 0.2f).Y, 1e-5f);
            Assert.AreEqual(0.2f, FootPlacement.Swing(from, to, 0.5f, 0.2f).Y, 1e-5f);
            Assert.AreEqual(1f, FootPlacement.Swing(from, to, 1f, 0.2f).Z, 1e-5f);
        }

        [Test]
        public void PredictPlant_LandsUnderTheBodyAtMidStance()
        {
            var plant = FootPlacement.PredictPlant(new V3(0, 0, 0), new V3(0, 0, 4f), 0.1f, 0.3f, new V3(0.1f, 0, 0));
            Assert.AreEqual(4f * (0.1f + 0.15f), plant.Z, 1e-5f);
            Assert.AreEqual(0.1f, plant.X, 1e-5f);
        }

        // ------------------------------------------------------------ mezcla
        [Test]
        public void SpeedWeights_SumToOneAndAreContinuous()
        {
            var w = new float[4]; float[] prev = null;
            for (float sp = 0f; sp <= 8f; sp += 0.01f)
            {
                LocomotionBlend.SpeedWeights(sp, w);
                float sum = 0f; foreach (var x in w) sum += x;
                Assert.AreEqual(1f, sum, 1e-4f);
                if (prev != null) for (int i = 0; i < 4; i++) Assert.Less(Math.Abs(w[i] - prev[i]), 0.02f, "sin saltos entre estados");
                prev = (float[])w.Clone();
            }
            LocomotionBlend.SpeedWeights(0f, w); Assert.AreEqual(1f, w[LocomotionBlend.Idle]);
            LocomotionBlend.SpeedWeights(6.8f, w); Assert.AreEqual(1f, w[LocomotionBlend.Run], 1e-5f);
        }

        [Test]
        public void DirectionWeights_EightDirections_Continuous()
        {
            var w = new float[8];
            LocomotionBlend.DirectionWeights(0f, 1f, w); Assert.AreEqual(1f, w[0], 1e-5f);
            LocomotionBlend.DirectionWeights(1f, 0f, w); Assert.AreEqual(1f, w[2], 1e-5f);
            LocomotionBlend.DirectionWeights(0f, -1f, w); Assert.AreEqual(1f, w[4], 1e-5f);
            LocomotionBlend.DirectionWeights(-1f, 0f, w); Assert.AreEqual(1f, w[6], 1e-5f);
            LocomotionBlend.DirectionWeights(0.7f, 0.7f, w); Assert.AreEqual(1f, w[1], 1e-3f, "diagonal delante-derecha");
            LocomotionBlend.DirectionWeights(0.3f, 1f, w); Assert.AreEqual(1f, w[0] + w[1], 1e-4f); Assert.Greater(w[0], w[1]);
            LocomotionBlend.DirectionWeights(0f, 0f, w); foreach (var x in w) Assert.AreEqual(0f, x);
        }

        // ------------------------------------------------------------ esqueleto
        [Test]
        public void Skeleton_HasValidHierarchyAndHeight()
        {
            var p = RigProportions.Soldier(); var b = HumanoidRigSpec.Build(p);
            Assert.AreEqual(HumanoidRigSpec.BoneCount, b.Length);
            for (int i = 0; i < b.Length; i++) { Assert.AreEqual((BoneId)i, b[i].Id); Assert.Less(b[i].Parent, i, "el padre siempre va antes"); }
            Assert.AreEqual(-1, b[0].Parent);
            float top = HumanoidRigSpec.RestPosition(b, BoneId.Head).Y + b[(int)BoneId.Head].Length;
            Assert.AreEqual(p.height, top, 1e-3f, "de los pies a la coronilla = altura");
            Assert.AreEqual(0f, HumanoidRigSpec.RestPosition(b, BoneId.LeftToes).Y, 1e-3f, "los pies tocan el suelo en reposo");
        }

        [Test]
        public void Skeleton_IsSymmetricAndScalesWithProportions()
        {
            var b = HumanoidRigSpec.Build(new RigProportions());
            var l = HumanoidRigSpec.RestPosition(b, BoneId.LeftHand); var r = HumanoidRigSpec.RestPosition(b, BoneId.RightHand);
            Assert.AreEqual(-l.X, r.X, 1e-5f); Assert.AreEqual(l.Y, r.Y, 1e-5f);
            var tall = HumanoidRigSpec.Build(new RigProportions { height = 2.6f, shoulderWidth = 0.9f });
            Assert.Greater(HumanoidRigSpec.LegLength(tall), HumanoidRigSpec.LegLength(b));
            Assert.Greater(HumanoidRigSpec.RestPosition(tall, BoneId.RightUpperArm).X, r.X - 0.5f);
            var bad = new RigProportions { height = 50f, hunch = 3f }; bad.Validate();
            Assert.LessOrEqual(bad.height, 4.5f); Assert.LessOrEqual(bad.hunch, 1f);
        }
    }
}
