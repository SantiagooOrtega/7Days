using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class LegSolverTests
    {
        private static readonly BoneDef[] Bones = HumanoidRigSpec.Build(RigProportions.Soldier());
        private static float Thigh => Bones[(int)BoneId.LeftUpperLeg].Length;
        private static float Shin => Bones[(int)BoneId.LeftLowerLeg].Length;
        private static float AnkleH => Bones[(int)BoneId.LeftFoot].Length;
        private static float HipH => Bones[(int)BoneId.Hips].LocalPosition.Y;
        private static float Half => Math.Abs(Bones[(int)BoneId.LeftUpperLeg].LocalPosition.X);
        private static readonly V3 Fwd = new V3(0, 0, 1), Right = new V3(1, 0, 0);

        [Test]
        public void FeetNeverGoBelowTheGround_AtAnyCrouchOrCoverLevel()
        {
            // agachado de 0 a 1, en suelo plano y con suelo más alto o más bajo bajo el pie (bordes, aceras), pidiendo incluso el tobillo bajo el suelo
            foreach (float ground in new[] { 0f, 0.15f, -0.12f })
                for (float c = 0f; c <= 1.0001f; c += 0.1f)
                    foreach (float side in new[] { -1f, 1f })
                        foreach (float wantY in new[] { -0.3f, 0f, 0.05f })
                        {
                            float pelvis = CrouchPose.PelvisHeight(HipH, c) - HipH * 0.02f;
                            var hip = new V3(side * Half, pelvis, CrouchPose.PelvisBack(c));
                            var target = new V3(side * CrouchPose.FootSide(Half, c), wantY, CrouchPose.FootAhead(c));
                            var r = LegSolver.Solve(hip, target, ground, AnkleH, CrouchPose.KneePole(hip, Fwd, Right, side), Thigh, Shin);
                            Assert.GreaterOrEqual(r.Ankle.Y, ground + AnkleH + LegSolver.Sole - 1e-3f, "tobillo bajo el suelo c=" + c + " suelo=" + ground);
                            Assert.GreaterOrEqual(r.Knee.Y, ground + LegSolver.KneeRadius - 0.011f, "rodilla bajo el suelo c=" + c + " suelo=" + ground);
                        }
        }

        [Test]
        public void KneeFlexionStaysWithinLimits_AndNeverHyperextends()
        {
            var rng = new Random(9);
            for (int i = 0; i < 500; i++)
            {
                var hip = new V3(0.15f, 0.2f + (float)rng.NextDouble() * 0.9f, 0f);
                var target = new V3(0.15f + (float)(rng.NextDouble() - 0.5) * 0.3f, -0.2f + (float)rng.NextDouble() * 0.5f, (float)(rng.NextDouble() - 0.4) * 0.8f);
                var pole = hip + Fwd * 1.2f + Right * 0.25f;
                var r = LegSolver.Solve(hip, target, 0f, AnkleH, pole, Thigh, Shin);
                Assert.That(r.KneeFlexion, Is.InRange(0f, LegSolver.MaxKneeFlexion + 0.5f));
                // hiperextensión = doblarse hacia el lado contrario al polo; la rodilla siempre se dobla hacia el polo (delante y algo fuera)
                V3 line = (r.Ankle - hip).Normalized;
                V3 onLine = hip + line * V3.Dot(r.Knee - hip, line);
                V3 poleSide = pole - (hip + line * V3.Dot(pole - hip, line));
                if (r.KneeFlexion > 1f) Assert.Greater(V3.Dot(r.Knee - onLine, poleSide), 0f, "la rodilla se dobla hacia el polo, nunca al revés (caso " + i + ")");
            }
        }

        [Test]
        public void TooLowPelvis_IsLimitedInsteadOfSinkingTheFoot()
        {
            float minHip = LegSolver.MinHipHeight(0f, AnkleH, 0.1f, Thigh, Shin);
            Assert.Greater(minHip, AnkleH);
            var r = LegSolver.Solve(new V3(0, minHip, 0), new V3(0, 0, 0.1f), 0f, AnkleH, new V3(0, minHip, 1.2f), Thigh, Shin);
            Assert.LessOrEqual(r.KneeFlexion, LegSolver.MaxKneeFlexion + 0.5f);
            Assert.GreaterOrEqual(r.Ankle.Y, AnkleH - 1e-3f);
            // con la pelvis por debajo del mínimo el solver no hunde el pie: lo aleja y avisa
            var low = LegSolver.Solve(new V3(0, minHip - 0.15f, 0), new V3(0, 0, 0.05f), 0f, AnkleH, new V3(0, 0.3f, 1.2f), Thigh, Shin);
            Assert.IsTrue(low.FlexionLimited || low.AnkleLifted);
            Assert.GreaterOrEqual(low.Ankle.Y, AnkleH - 1e-3f);
        }

        [Test]
        public void KneesBendForwardAndSlightlyOut_NeverCross()
        {
            for (float c = 0f; c <= 1f; c += 0.25f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float pelvis = CrouchPose.PelvisHeight(HipH, c) - HipH * 0.02f;
                    var hip = new V3(side * Half, pelvis, CrouchPose.PelvisBack(c));
                    var r = LegSolver.Solve(hip, new V3(side * CrouchPose.FootSide(Half, c), AnkleH, CrouchPose.FootAhead(c)), 0f, AnkleH, CrouchPose.KneePole(hip, Fwd, Right, side), Thigh, Shin);
                    Assert.Greater(r.Knee.X * side, Half * 0.8f, "rodilla en su lado (c=" + c + ")");
                }
        }

        [Test]
        public void Kneel_OneKneeOnTheGroundShinFlat_OtherAround90()
        {
            float hipY = KneelPose.HipHeight(Thigh);
            var hipK = new V3(Half, hipY, 0f);
            var kneeTarget = KneelPose.KneelingKnee(Half);
            var k = LegSolver.Solve(hipK, KneelPose.KneelingAnkle(Half, Thigh, Shin, AnkleH), 0f, AnkleH, kneeTarget + new V3(0, 0, 0.4f), Thigh, Shin);
            Assert.AreEqual(LegSolver.KneeRadius, k.Knee.Y, 0.03f, "rodilla apoyada en el suelo");
            Assert.Less(Math.Abs(k.Knee.Y - k.Ankle.Y), 0.06f, "pantorrilla casi plana");
            Assert.Less(k.Ankle.Z, k.Knee.Z, "el pie queda detrás de la rodilla");

            var hipF = new V3(-Half, hipY, 0f);
            var f = LegSolver.Solve(hipF, KneelPose.FrontAnkle(-Half, Thigh, AnkleH), 0f, AnkleH, hipF + Fwd * 1.2f - Right * 0.25f, Thigh, Shin);
            Assert.That(f.KneeFlexion, Is.InRange(60f, 120f), "pierna delantera doblada unos 90°");
            Assert.GreaterOrEqual(f.Ankle.Y, AnkleH);
        }

        [Test]
        public void PoseChangesContinuously_WhenCrouchingAndKneeling()
        {
            V3 prevKnee = default; bool first = true;
            for (float c = 0f; c <= 1f; c += 0.01f)
            {
                float pelvis = CrouchPose.PelvisHeight(HipH, c) - HipH * 0.02f;
                var hip = new V3(Half, pelvis, CrouchPose.PelvisBack(c));
                var r = LegSolver.Solve(hip, new V3(CrouchPose.FootSide(Half, c), AnkleH, CrouchPose.FootAhead(c)), 0f, AnkleH, CrouchPose.KneePole(hip, Fwd, Right, 1f), Thigh, Shin);
                if (!first) Assert.Less((r.Knee - prevKnee).Length, 0.03f, "sin saltos de pose (c=" + c + ")");
                prevKnee = r.Knee; first = false;
            }
        }

        [Test]
        public void Arms_SwingOppositeToTheLegs()
        {
            for (float p = 0f; p < 1f; p += 0.05f)
            {
                Assert.Less(ArmSwingMath.Offset(p, true) * ArmSwingMath.FootForward(p, true), 1e-4f, "brazo izquierdo contra pie izquierdo, fase " + p);
                Assert.Less(ArmSwingMath.Offset(p, false) * ArmSwingMath.FootForward(p, false), 1e-4f, "brazo derecho contra pie derecho, fase " + p);
                Assert.AreEqual(-ArmSwingMath.Offset(p, true), ArmSwingMath.Offset(p, false), 1e-5f, "los brazos se cruzan entre sí");
            }
            Assert.AreEqual(-1f, ArmSwingMath.Offset(0f, true), 1e-5f, "al apoyar el pie izquierdo delante, el brazo izquierdo va atrás");
            float reach = ArmSwingMath.RunningReach * 2f;   // brazo y antebrazo iguales
            float elbow = 180f - (float)(Math.Acos((2f - reach * reach) / 2f) * 180.0 / Math.PI);
            Assert.That(elbow, Is.InRange(75f, 105f), "codos a unos 90° corriendo");
        }
    }
}
