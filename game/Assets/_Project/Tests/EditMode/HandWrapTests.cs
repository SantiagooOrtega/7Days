using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class HandWrapTests
    {
        [TestCase(0.02f), TestCase(0.03f), TestCase(0.045f), TestCase(0.06f)]
        public void Fingers_TouchButNeverEnterTheHandle(float r)
        {
            V3 c = HandWrap.Anchor(r, 1f);
            for (int i = 0; i < 4; i++)
            {
                var f = HandWrap.Finger(i, r, 1f);
                double a1 = f.Base * System.Math.PI / 180.0, a2 = (f.Base + f.Tip) * System.Math.PI / 180.0;
                float bx = 0f, by = -HandWrap.PalmLength;
                float jx = bx - (float)System.Math.Sin(a1) * HandWrap.L1[i], jy = by - (float)System.Math.Cos(a1) * HandWrap.L1[i];
                float tx = jx - (float)System.Math.Sin(a2) * HandWrap.L2[i], ty = jy - (float)System.Math.Cos(a2) * HandWrap.L2[i];
                float d1 = HandWrap.SegmentDistance(bx, by, jx, jy, c.X, c.Y), d2 = HandWrap.SegmentDistance(jx, jy, tx, ty, c.X, c.Y);
                Assert.GreaterOrEqual(d1, r + HandWrap.FingerHalf - 1e-3f, "falange 1 del dedo " + i);
                Assert.GreaterOrEqual(d2, r + HandWrap.FingerHalf - 1e-3f, "falange 2 del dedo " + i);
                Assert.That(f.Base, Is.InRange(0f, HandPoses.MaxCurl)); Assert.That(f.Tip, Is.InRange(0f, HandPoses.MaxCurl));
            }
        }

        [Test]
        public void ThinnerHandle_CurlsMore()
        {
            var thin = HandWrap.Finger(1, 0.02f, 1f); var thick = HandWrap.Finger(1, 0.06f, 1f);
            Assert.Greater(thin.Base + thin.Tip, thick.Base + thick.Tip);
            Assert.Greater(thick.Base + thick.Tip, 40f, "aun con un mango grueso, los dedos lo rodean");
        }

        [Test]
        public void Anchor_HandleSitsInFrontOfThePalm()
        {
            V3 a = HandWrap.Anchor(0.03f, 1f);
            Assert.AreEqual(-(HandWrap.PalmThickness * 0.5f + 0.03f), a.X, 1e-5f);
            Assert.That(-a.Y, Is.InRange(0.3f * HandWrap.PalmLength, HandWrap.PalmLength), "a lo largo de la palma");
        }

        [Test]
        public void Melee_SupportReleasesDuringTheStrike_ExceptShovel()
        {
            foreach (var id in new[] { "bar", "bat", "axe" })
            {
                var g = MeleeGrips.For(id, 0.9f);
                Assert.IsTrue(g.TwoHanded, id);
                Assert.IsFalse(MeleeGrips.Released(g, -1f, false), id + ": en reposo, a dos manos");
                Assert.IsTrue(MeleeGrips.Released(g, MeleeSwing.ImpactTime(false), false), id + ": suelta al golpear");
                Assert.IsFalse(MeleeGrips.Released(g, MeleeSwing.Total(false) + 0.1f, false), id + ": vuelve tras el golpe");
                Assert.Greater(g.High, g.Low, id + ": agarre alto y bajo");
            }
            Assert.IsFalse(MeleeGrips.Released(MeleeGrips.For("shovel", 1f), MeleeSwing.ImpactTime(true), true), "la pala empuja con las dos manos");
        }

        [Test]
        public void Weights_HeavierWeaponsWeighMore()
        {
            Assert.Less(WeaponWeights.For(WeaponKind.Pistol, "pistol"), WeaponWeights.For(WeaponKind.AssaultRifle, "assault_rifle"));
            Assert.Less(WeaponWeights.For(WeaponKind.AssaultRifle, "assault_rifle"), WeaponWeights.For(WeaponKind.SniperRifle, "sniper"));
        }

        [Test]
        public void MigrateV4_GripPointsBecomeHandleAxes()
        {
            var w = WeaponCatalog.Defaults().Find(x => x.id == "assault_rifle");
            w.dataVersion = 4; w.grip = new WeaponGrip { foregrip = new[] { 0f, -0.07f, 0.32f, 0f, 0f, 0f } };
            Assert.IsTrue(WeaponCatalog.Migrate(w));
            Assert.AreEqual(5, w.dataVersion);
            Assert.AreEqual(0f, w.grip.foregrip[1], 1e-5f, "el guardamano es ahora el eje del cañón");
            Assert.AreEqual(w.length * 0.36f, w.grip.foregrip[2], 1e-5f);
        }
    }
}
