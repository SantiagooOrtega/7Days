using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Animación procedural pura de los zombis: ataques con impacto en 0.5, reacciones por zona, formas de morir ajustadas al suelo y emerger.</summary>
    public class ZombieMotionTests
    {
        public static IEnumerable<string> Types() => ZombieAnatomies.Types;
        private static float Dist(V3 a, V3 b) => (a - b).Length;

        [Test]
        public void EveryAttack_StartsAndEndsInGuard_AndStrikesAtTheDamageWindow()
        {
            var gl = new V3(-0.12f, -0.15f, 0.82f); var gr = new V3(0.12f, -0.15f, 0.82f);
            foreach (ZombieAttackKind k in Enum.GetValues(typeof(ZombieAttackKind)))
            {
                var a0 = ZombieMotion.Attack(k, 0f, gl, gr); var a1 = ZombieMotion.Attack(k, 1f, gl, gr);
                Assert.Less(Dist(a0.LeftHand, gl) + Dist(a0.RightHand, gr), 0.01f, k + " empieza en guardia");
                Assert.Less(Dist(a1.LeftHand, gl) + Dist(a1.RightHand, gr), 0.01f, k + " termina en guardia");
                Assert.Less(a1.Torso.X * a1.Torso.X + a1.Torso.Y * a1.Torso.Y, 1f, k + " torso de vuelta");
                var mid = ZombieMotion.Attack(k, ZombieMotion.ImpactT, gl, gr);
                Assert.Greater(mid.Impact, 0.95f, k + " impacto en 0.5");
                // en el golpe alguna mano está lejos de la guardia (hay movimiento real) o el torso se lanza
                float travel = Math.Max(Dist(mid.LeftHand, gl), Dist(mid.RightHand, gr)) + Math.Abs(mid.Torso.X) / 100f;
                Assert.Greater(travel, 0.3f, k + " se mueve en el golpe");
            }
        }

        [Test]
        public void Attacks_AreContinuous_NoJumpsBetweenFrames()
        {
            var gl = new V3(-0.12f, -0.15f, 0.82f); var gr = new V3(0.12f, -0.15f, 0.82f);
            foreach (ZombieAttackKind k in Enum.GetValues(typeof(ZombieAttackKind)))
            {
                var prev = ZombieMotion.Attack(k, 0f, gl, gr);
                for (int i = 1; i <= 120; i++)
                {
                    var f = ZombieMotion.Attack(k, i / 120f, gl, gr);
                    Assert.Less(Dist(f.LeftHand, prev.LeftHand), 0.3f, k + " mano izquierda salta en " + i);
                    Assert.Less(Dist(f.RightHand, prev.RightHand), 0.3f, k + " mano derecha salta en " + i);
                    Assert.Less(Math.Abs(f.Torso.X - prev.Torso.X), 12f, k + " torso salta");
                    prev = f;
                }
            }
        }

        [Test]
        public void AttackHasAnticipationBeforeTheStrike()
        {
            var gl = new V3(-0.12f, -0.15f, 0.82f); var gr = new V3(0.12f, -0.15f, 0.82f);
            var wind = ZombieMotion.Attack(ZombieAttackKind.Swipe, 0.4f, gl, gr);
            Assert.Greater(wind.RightHand.Y, 0.4f, "la mano sube antes de golpear");
            var follow = ZombieMotion.Attack(ZombieAttackKind.Swipe, 0.7f, gl, gr);
            Assert.Less(follow.RightHand.Y, wind.RightHand.Y, "y baja después (seguimiento)");
        }

        [Test]
        public void Flinch_DependsOnTheZone()
        {
            var front = new V3(0, 0, 1);
            var head = ZombieMotion.FlinchFor(HitZone.Head, front, 1f); var torso = ZombieMotion.FlinchFor(HitZone.Torso, front, 1f);
            var arm = ZombieMotion.FlinchFor(HitZone.Arms, new V3(1, 0, 0), 1f); var leg = ZombieMotion.FlinchFor(HitZone.Legs, front, 1f);
            Assert.Less(head.Head.X, -100f, "cabeza hacia atrás");
            Assert.Greater(torso.Torso.X, 60f, "torso doblado");
            Assert.Greater(arm.LeftArm + arm.RightArm, 1f, "brazo golpeado");
            Assert.Less(leg.PelvisDrop, -0.3f, "pierna que cede");
        }

        [Test]
        public void DeathForm_FollowsHitDirectionAndZone()
        {
            Assert.AreEqual(ZombieDeathForm.FallBack, ZombieMotion.DeathFor(HitZone.Head, new V3(0, 0, 1), true, 1));
            Assert.AreEqual(ZombieDeathForm.FallForward, ZombieMotion.DeathFor(HitZone.Torso, new V3(0, 0, -1), false, 1));
            Assert.AreEqual(ZombieDeathForm.Kneel, ZombieMotion.DeathFor(HitZone.Legs, new V3(0, 0, 1), false, 1));
        }

        [Test]
        public void Emerge_RisesFromBelowTheGround()
        {
            var e0 = ZombieMotion.Emerge(0f); var e1 = ZombieMotion.Emerge(1f);
            Assert.Less(e0.PelvisOffset.Y, -0.5f); Assert.AreEqual(0f, e1.PelvisOffset.Y, 1e-4f); Assert.Greater(e0.Torso.X, 60f); Assert.AreEqual(0f, e1.Torso.X, 1e-3f);
        }

        [Test]
        public void Limp_DropsTheHipOnTheLimpingSide()
        {
            ZombieMotion.Limp(1f, 1, 0.25f, out float roll, out float torso);
            Assert.Greater(roll, 2f); Assert.Less(torso, -1f);
            ZombieMotion.Limp(1f, -1, 0.25f, out float rollL, out _);
            Assert.Less(rollL, -2f);
            ZombieMotion.Limp(0f, 1, 0.25f, out float none, out _);
            Assert.AreEqual(0f, none, 1e-5f);
        }

        public static IEnumerable<TestCaseData> DeathCases()
        {
            foreach (string t in ZombieAnatomies.Types)
                foreach (ZombieDeathForm f in Enum.GetValues(typeof(ZombieDeathForm)))
                    yield return new TestCaseData(t, f).SetName("Muerte " + f + " de " + t);
        }

        [TestCaseSource(nameof(DeathCases))]
        public void DeathPose_TouchesTheGround_NeverBelowAndLiesLow(string type, ZombieDeathForm form)
        {
            var a = ZombieAnatomies.For(type);
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), type)[0];
            var model = ZombieModel.Build(a, v, 2);
            var skinned = ZombieSkinning.Flatten(model, a.Size);
            var pose = ZombiePoses.Lying(form, model.Spec);
            pose.HipsPos = new V3(0f, ZombiePoses.FitToGround(skinned, model.Spec, pose), 0f);
            var rot = new Mat3[22]; var pos = new V3[22];
            ZombiePoses.Forward(model.Spec, pose, rot, pos);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var part in new[] { skinned.Skin, skinned.Cloth })
                for (int i = 0; i < part.Mesh.Positions.Count; i++)
                {
                    float y = ZombiePoses.SkinPoint(part.Mesh.Positions[i], part.Weights[i], skinned.RestBonePositions, skinned.BoneToSkeleton, rot, pos).Y;
                    min = Math.Min(min, y); max = Math.Max(max, y);
                }
            float h = a.Proportions.height;
            TestContext.WriteLine(type + " " + form + ": cadera " + pose.HipsPos.Y.ToString("0.00") + " m, punto más alto " + max.ToString("0.00") + " m (altura " + h.ToString("0.00") + ")");
            Assert.AreEqual(0.012f, min, 0.002f, "toca el suelo sin atravesarlo");
            if (form == ZombieDeathForm.Kneel) Assert.Less(max, h * 0.85f, "de rodillas es más bajo que de pie");
            else Assert.Less(max, h * 0.42f, "tumbado, todo queda cerca del suelo");
        }
    }
}
