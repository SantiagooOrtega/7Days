using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class LimbGeometryTests
    {
        private static readonly float[] Rings = { 0f, 0.0875f, 0.5f, 0.08f, 0.92f, 0.055f, 1f, 0.052f };

        [Test]
        public void Segment_PivotAtJoint_DoesNotPokeAboveIt_AndEndsAtTheChildJoint()
        {
            var m = LimbGeometry.Segment(0.41f, Rings, 10, 1.08f);
            float maxY = float.MinValue, minY = float.MaxValue;
            foreach (var p in m.Positions) { if (p.Y > maxY) maxY = p.Y; if (p.Y < minY) minY = p.Y; }
            Assert.AreEqual(0f, maxY, 1e-5f, "la pieza no asoma por encima de su articulación");
            Assert.AreEqual(-0.41f, minY, 1e-5f, "y llega justo a la articulación siguiente");
        }

        [Test]
        public void Segment_KneeEnd_FitsInsideTheKneeJoint()
        {
            var m = LimbGeometry.Segment(0.41f, Rings, 10, 1.08f);
            foreach (var p in m.Positions)
                if (p.Y < -0.41f * 0.99f) Assert.LessOrEqual(new V3(p.X, 0f, p.Z).Length, 0.0575f, "junto a la rodilla, dentro de la rótula");
        }

        private static readonly FootShape Boot = new FootShape { Drop = 0.0819f, Heel = 0.07f, Ball = 0.1365f, Toe = 0.075f };

        [Test]
        public void FootFlat_AnkleAtDropPlusSole()
            => Assert.AreEqual(Boot.Drop + LegSolver.Sole, FootGeometry.MinAnkleHeight(Boot, 0f, 0f), 1e-5f);

        [TestCase(-12f), TestCase(-25f)]
        public void HeelStrike_RaisesAnkleSoTheHeelStaysAboveGround(float pitch)
        {
            float h = FootGeometry.MinAnkleHeight(Boot, pitch, 0f);
            double p = pitch * System.Math.PI / 180.0;
            float heelDrop = Boot.Drop * (float)System.Math.Cos(p) - Boot.Heel * (float)System.Math.Sin(p);
            Assert.GreaterOrEqual(h, heelDrop + LegSolver.Sole - 1e-5f);
            Assert.Greater(h, FootGeometry.MinAnkleHeight(Boot, 0f, 0f) - 0.01f);
        }

        [TestCase(28f), TestCase(70f)]
        public void ToeDown_WithFlatToe_RollsOnTheBall(float pitch)
        {
            double p = pitch * System.Math.PI / 180.0;
            float ball = Boot.Drop * (float)System.Math.Cos(p) + Boot.Ball * (float)System.Math.Sin(p);
            Assert.AreEqual(ball + LegSolver.Sole, FootGeometry.MinAnkleHeight(Boot, pitch, FootGeometry.FlatToeBend(pitch)), 1e-4f, "punta plana: la bola es lo más bajo");
            Assert.Greater(FootGeometry.MinAnkleHeight(Boot, pitch, 0f), ball + LegSolver.Sole, "sin doblar la punta, la punta bajaría más: el tobillo sube más");
        }

        [Test]
        public void RearKneelAnkle_FollowsTheBootHeight_ShinStillNearlyHorizontal()
        {
            float ankleY = FootGeometry.MinAnkleHeight(Boot, HalfKneel.RearFootPitch, FootGeometry.FlatToeBend(HalfKneel.RearFootPitch));
            V3 a = HalfKneel.RearAnkle(0.15f, 0.38f, ankleY);
            Assert.AreEqual(ankleY, a.Y, 1e-5f);
            V3 knee = HalfKneel.RearKnee(0.15f);
            Assert.AreEqual(0.38f, (a - knee).Length, 1e-4f, "la espinilla conserva su largo");
            double pitch = System.Math.Asin((a.Y - knee.Y) / 0.38) * 180.0 / System.Math.PI;
            Assert.Less(pitch, 25.0, "espinilla casi horizontal");
        }
    }
}

namespace SevenDays.Tests
{
    public class LocomotionShapeTests
    {
        private static readonly LocomotionPoseData D = new LocomotionPoseData();
        private const float Thigh = 0.4117f, Shin = 0.38f;

        [Test]
        public void StandHip_KneeAlmostStraight()
        {
            float h = GaitPose.StandHipJoint(0.097f, Thigh, Shin, D.standKnee);
            Assert.AreEqual(0.097f + LegSolver.Distance(Thigh, Shin, D.standKnee), h, 1e-5f);
            Assert.Greater(h, 0.097f + (Thigh + Shin) * 0.995f, "de pie con la rodilla a ~6°: casi recta (antes 41° sin que se viera)");
        }

        [Test]
        public void ReachStance_RunHasFlight_WalkHasDoubleSupport()
        {
            float hip = GaitPose.StandHipJoint(0.097f, Thigh, Shin, D.standKnee);
            float run = GaitPose.ReachStance(GaitCycle.StanceFraction(6.8f), GaitCycle.CycleLength(6.8f, 0.92f), hip, 0.108f, 0.08f, D, Thigh, Shin);
            float walk = GaitPose.ReachStance(GaitCycle.StanceFraction(1.6f), GaitCycle.CycleLength(1.6f, 0.92f), hip, 0.108f, 0.08f, D, Thigh, Shin);
            Assert.That(run, Is.InRange(0.17f, 0.35f), "corriendo: vuelo largo, apoyo corto");
            Assert.Greater(walk, 0.5f, "caminando siempre hay doble apoyo");
        }

        [Test]
        public void FlightDrop_HighInTheMiddle_LowerAtTheEnds()
        {
            float s = 0.25f;
            Assert.AreEqual(-1f, GaitPose.FlightDrop(D, 0.1f, s), "en apoyo no hay vuelo");
            Assert.AreEqual(0f, GaitPose.FlightDrop(D, 0.375f, s), 1e-4f, "a mitad del vuelo, lo más alto");
            Assert.AreEqual(GaitPose.FlightDip(D), GaitPose.FlightDrop(D, 0.4999f, s), 1e-3f, "al llegar al contacto, baja");
        }

        [Test]
        public void MoveLean_Ranges_AndWeaponLowers_AndNeverOver15()
        {
            Assert.That(GaitPose.MoveLean(D, 0f, 1f, 0f, 0f), Is.InRange(5f, 8f), "trotar");
            Assert.That(GaitPose.MoveLean(D, 0f, 0f, 1f, 0f), Is.InRange(8f, 12f), "correr");
            Assert.That(GaitPose.MoveLean(D, 0f, 0f, 1f, 1f), Is.InRange(3f, 6f), "con arma");
            var big = new LocomotionPoseData { runLean = 40f }; big.Validate();
            Assert.LessOrEqual(GaitPose.MoveLean(big, 0f, 0f, 1f, 0f), 15f);
            Assert.That(GaitPose.PelvisLean(D, GaitPose.MoveLean(D, 0f, 0f, 1f, 0f)), Is.InRange(2f, 4f), "la pelvis pone 2-4°");
        }

        [Test]
        public void Migrate_OldAssetGetsNewRunLean_KeepsOtherEdits()
        {
            var old = new LocomotionPoseData { runLean = 14f, contactKnee = 18f, poseVersion = 0 };
            Assert.IsTrue(old.Migrate());
            Assert.AreEqual(new LocomotionPoseData().runLean, old.runLean);
            Assert.AreEqual(18f, old.contactKnee, "lo demás se respeta");
            Assert.IsFalse(old.Migrate(), "solo una vez");
        }
    }
}
