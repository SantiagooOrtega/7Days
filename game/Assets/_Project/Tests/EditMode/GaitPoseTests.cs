using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class GaitPoseTests
    {
        private static readonly LocomotionPoseData D = new LocomotionPoseData();
        private static readonly BoneDef[] B = HumanoidRigSpec.Build(RigProportions.Soldier());
        private static float Thigh => B[(int)BoneId.LeftUpperLeg].Length;
        private static float Shin => B[(int)BoneId.LeftLowerLeg].Length;
        private static float AnkleH => B[(int)BoneId.LeftFoot].Length;

        [Test]
        public void StancePhases_ContactMidStanceToeOff()
        {
            float stance = GaitCycle.StanceFraction(1.6f);
            Assert.That(GaitPose.KneeFlexion(D, 0f, stance, 0f), Is.InRange(10f, 20f), "contacto del talón: 10-20°");
            Assert.That(GaitPose.KneeFlexion(D, stance * 0.5f, stance, 0f), Is.LessThan(10f), "apoyo medio casi extendida");
            Assert.That(GaitPose.KneeFlexion(D, stance * 0.999f, stance, 0f), Is.InRange(25f, 40f), "despegue: empieza a doblar");
        }

        [Test]
        public void SwingPeak_WalkVsRun()
        {
            float maxWalk = 0f, maxRun = 0f;
            float sw = GaitCycle.StanceFraction(1.6f), sr = GaitCycle.StanceFraction(6.8f);
            for (float p = 0f; p < 1f; p += 0.005f) { maxWalk = Math.Max(maxWalk, GaitPose.KneeFlexion(D, p, sw, 0f)); maxRun = Math.Max(maxRun, GaitPose.KneeFlexion(D, p, sr, 1f)); }
            Assert.That(maxWalk, Is.InRange(50f, 70f), "caminando 50-70°");
            Assert.That(maxRun, Is.InRange(100f, 120f), "corriendo 100-120°");
            float tw = 0f, tr = 0f;
            for (float p = 0f; p < 1f; p += 0.005f) { tw = Math.Max(tw, GaitPose.ThighAngle(D, p, sw, 0f)); tr = Math.Max(tr, GaitPose.ThighAngle(D, p, sr, 1f)); }
            Assert.That(tw, Is.InRange(25f, 35f), "muslo adelante ~30° caminando");
            Assert.That(tr, Is.InRange(50f, 60f), "muslo adelante 50-60° corriendo");
        }

        [Test]
        public void KneeCurveIsContinuousOverTheCycle()
        {
            foreach (float run in new[] { 0f, 1f })
            {
                float stance = GaitCycle.StanceFraction(run > 0f ? 6.8f : 1.6f);
                float prev = GaitPose.KneeFlexion(D, 0f, stance, run);
                for (float p = 0.005f; p <= 1.0001f; p += 0.005f)
                {
                    float k = GaitPose.KneeFlexion(D, p % 1f, stance, run);
                    Assert.Less(Math.Abs(k - prev), 6f, "sin saltos (fase " + p + ", run " + run + ")");
                    prev = k;
                }
            }
        }

        [Test]
        public void FootRolls_HeelThenFlatThenToe()
        {
            float stance = 0.6f;
            Assert.Less(GaitPose.FootPitch(D, 0f, stance), -5f, "talón: punta arriba al contactar");
            Assert.AreEqual(0f, GaitPose.FootPitch(D, stance * 0.4f, stance), "planta apoyada en el apoyo medio");
            Assert.Greater(GaitPose.FootPitch(D, stance * 0.99f, stance), 20f, "punta: flexión plantar al despegar");
            Assert.Greater(GaitPose.HeelRaise(25f, 0.13f), 0.04f, "al levantar el talón el tobillo sube (la punta sigue en el suelo)");
            Assert.AreEqual(0f, GaitPose.HeelRaise(-10f, 0.13f));
        }

        [Test]
        public void SwingFoot_HeelGoesTowardTheGluteWhenRunning()
        {
            var a = GaitPose.AnkleFromAngles(0f, 110f, Thigh, Shin);
            Assert.Less(a.Z, 0f, "con el muslo vertical y la rodilla a 110°, el talón va detrás");
            Assert.Greater(a.Y, -Thigh, "y sube por encima de la rodilla");
            var straight = GaitPose.AnkleFromAngles(0f, 0f, Thigh, Shin);
            Assert.AreEqual(-(Thigh + Shin), straight.Y, 1e-4f);
        }

        [Test]
        public void StanceLegThroughTheSolver_HasTheRequestedKneeAndStaysAboveGround()
        {
            foreach (float run in new[] { 0f, 1f })
            {
                float stance = GaitCycle.StanceFraction(run > 0f ? 6.8f : 1.6f);
                for (float s = 0f; s < 1f; s += 0.1f)
                {
                    float fp = s * stance, knee = GaitPose.KneeFlexion(D, fp, stance, run);
                    float horiz = (0.5f - s) * 0.5f;   // el pie pasa de delante a detrás de la cadera durante el apoyo
                    float hipY = AnkleH + LegSolver.Sole + GaitPose.HipAboveAnkle(knee, Math.Abs(horiz), Thigh, Shin);
                    var hip = new V3(0.15f, hipY, 0f);
                    var r = LegSolver.Solve(hip, new V3(0.15f, AnkleH + LegSolver.Sole, horiz), 0f, AnkleH, hip + new V3(0.04f, -0.4f, 1.2f), Thigh, Shin);
                    Assert.AreEqual(knee, r.KneeFlexion, 2f, "rodilla pedida en la fase " + s);
                    Assert.GreaterOrEqual(r.Ankle.Y, AnkleH - 1e-3f); Assert.GreaterOrEqual(r.Knee.Y, LegSolver.KneeRadius - 0.011f);
                }
            }
        }

        [Test]
        public void HalfKneel_FrontKnee90ShinVertical_RearKneeNearGroundShinFlat()
        {
            float hipJ = HalfKneel.HipJointHeight(Thigh);
            // pierna trasera
            var rearHip = new V3(0.15f, hipJ, 0f);
            var rr = LegSolver.Solve(rearHip, HalfKneel.RearAnkle(0.15f, Shin), 0f, AnkleH, HalfKneel.RearKnee(0.15f) + new V3(0, 0, 0.5f), Thigh, Shin);
            Assert.That(rr.Knee.Y, Is.InRange(0.03f, 0.08f), "rodilla trasera a 3-8 cm del suelo");
            float shinPitch = (float)(Math.Atan2(rr.Ankle.Y - rr.Knee.Y, Math.Abs(rr.Ankle.Z - rr.Knee.Z)) * 180.0 / Math.PI);
            Assert.That(shinPitch, Is.InRange(-2f, 15f), "espinilla trasera casi horizontal");
            // pierna delantera
            var frontHip = new V3(-0.15f, hipJ, 0f);
            var fa = HalfKneel.FrontAnkle(-0.15f, Thigh, Shin, AnkleH, hipJ);
            var fr = LegSolver.Solve(frontHip, fa, 0f, AnkleH, frontHip + new V3(-0.04f, 0.3f, 1.2f), Thigh, Shin);
            Assert.That(fr.KneeFlexion, Is.InRange(80f, 100f), "rodilla delantera ~90°");
            Assert.Less(Math.Abs(fr.Knee.Z - fr.Ankle.Z), 0.05f, "espinilla delantera vertical");
            Assert.GreaterOrEqual(fr.Ankle.Y, AnkleH - 1e-3f, "pie delantero plano en el suelo");
            // pelvis centrada entre apoyos (rodilla trasera y pie delantero)
            float center = (rr.Knee.Z + fr.Ankle.Z) * 0.5f;
            Assert.Less(Math.Abs(center), 0.22f, "pelvis entre los apoyos");
            Assert.That(D.kneelTorsoLean, Is.InRange(25f, 35f));
        }

        [Test]
        public void Settings_AreClampedToSafeRanges()
        {
            var d = new LocomotionPoseData { runSwingKnee = 300f, contactKnee = -10f, backThigh = 20f };
            d.Validate();
            Assert.LessOrEqual(d.runSwingKnee, LegSolver.MaxKneeFlexion); Assert.GreaterOrEqual(d.contactKnee, 0f); Assert.LessOrEqual(d.backThigh, 0f);
        }
    }

    public class GripTests
    {
        private static readonly BoneDef[] B = HumanoidRigSpec.Build(RigProportions.Soldier());
        private static float Upper => B[(int)BoneId.RightUpperArm].Length;
        private static float Lower => B[(int)BoneId.RightLowerArm].Length;

        private static V3 Shoulder(bool right, float twist = 0f)
        {
            var p = HumanoidRigSpec.RestPosition(B, right ? BoneId.RightUpperArm : BoneId.LeftUpperArm);
            return GripLayout.Twist(new V3(p.X, p.Y - CrouchPose.StandBend, p.Z), twist);
        }

        /// <summary>Codo hacia abajo y un poco hacia dentro (nunca hacia fuera).</summary>
        private static V3 ElbowPole(V3 shoulder, bool right) => shoulder + new V3(right ? -0.15f : 0.15f, -0.5f, -0.1f);

        private static void CheckArm(V3 shoulder, V3 target, bool right, string what)
        {
            float d = (target - shoulder).Length;
            Assert.LessOrEqual(d, (Upper + Lower) * 0.999f, what + ": la mano llega (" + d.ToString("0.000") + " m)");
            TwoBoneIK.Solve(shoulder, target, ElbowPole(shoulder, right), Upper, Lower, out V3 elbow, out V3 hand);
            Assert.Less((hand - target).Length, 0.02f, what + ": mano a menos de 2 cm de su punto de agarre");
            float flex = LegSolver.KneeFlexion(shoulder, elbow, hand);
            Assert.That(flex, Is.InRange(0f, 150f), what + ": codo sin hiperextensión");
            Assert.Less(elbow.Y, shoulder.Y + 0.02f, what + ": codo hacia abajo");
            Assert.Less((elbow.X - shoulder.X) * (right ? 1f : -1f), 0.12f, what + ": codo nunca hacia fuera");
        }

        [Test]
        public void EveryWeapon_HandsReachTheirGripPoints_Aiming()
        {
            foreach (var w in WeaponCatalog.Defaults())
            {
                if (w.IsMelee) continue;
                var g = WeaponGrip.For(w.kind, w.length, w.thickness);
                float twist = g.shouldered ? GripLayout.ShoulderedTwist : 0f;   // con culata, postura de perfil
                V3 rs = Shoulder(true, twist), ls = Shoulder(false, twist);
                // apuntando: culata al hombro (rifles) o pistola delante del pecho a la altura de los ojos
                V3 origin = g.shouldered ? GripLayout.ShoulderedOrigin(GripLayout.StockPocket(rs), g) : new V3(0.05f, rs.Y + 0.02f, 0.42f);
                CheckArm(rs, origin + g.GripPos, true, w.id + " derecha");
                CheckArm(ls, origin + g.ForegripPos, false, w.id + " izquierda");
            }
        }

        [Test]
        public void Melee_RightHandOnTheBar()
        {
            var g = WeaponGrip.For(WeaponKind.Melee, 0.9f, 0.05f);
            Assert.AreEqual("melee", g.support, "a dos manos: agarre bajo y alto (MeleeGrips)"); Assert.IsFalse(g.shouldered);
            V3 rs = Shoulder(true);
            CheckArm(rs, rs + new V3(0f, -0.35f, 0.25f) + g.GripPos, true, "barra");
        }

        [Test]
        public void HandPoses_WithinLimits_TriggerFingerExtendsUntilFiring()
        {
            foreach (HandGrip h in Enum.GetValues(typeof(HandGrip))) Assert.IsTrue(HandPoses.WithinLimits(HandPoses.For(h)), h.ToString());
            Assert.Less(HandPoses.For(HandGrip.Grip, false).Index.Base, 15f, "índice fuera del guardamonte");
            Assert.Greater(HandPoses.For(HandGrip.Grip, true).Index.Base, 35f, "índice en el gatillo al disparar");
            Assert.Greater(HandPoses.For(HandGrip.Grip).Middle.Base, 70f, "dedos cerrados en la empuñadura");
            var mid = HandPose.Lerp(HandPoses.For(HandGrip.Open), HandPoses.For(HandGrip.Fist), 0.5f);
            Assert.IsTrue(HandPoses.WithinLimits(mid));
            Assert.AreEqual(HandGrip.Pump, WeaponGrip.For(WeaponKind.Shotgun, 0.85f, 0.11f).SupportGrip);
            Assert.AreEqual(HandGrip.MagGrip, WeaponGrip.For(WeaponKind.Smg, 0.55f, 0.09f).SupportGrip);
        }
    }
}
