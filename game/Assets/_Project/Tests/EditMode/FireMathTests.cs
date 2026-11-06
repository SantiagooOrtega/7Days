using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class FireMathTests
    {
        [Test]
        public void Burn_OnlyInsideTheRadius_AndAtAReasonableHeight()
        {
            Assert.AreEqual(4f * 0.25f, FireMath.BurnDamage(0.3f, 0f, 1f, 4f, 0.25f), 1e-5f);
            Assert.AreEqual(0f, FireMath.BurnDamage(1.2f, 0f, 1f, 4f, 0.25f), "fuera del radio");
            Assert.AreEqual(0f, FireMath.BurnDamage(0.3f, 3f, 1f, 4f, 0.25f), "saltando por encima");
            Assert.AreEqual(0f, FireMath.BurnDamage(0.3f, -1f, 1f, 4f, 0.25f), "muy por debajo del fuego");
            Assert.AreEqual(4f * 0.25f, FireMath.BurnDamage(1f, 0f, 1f, 4f, 0.25f), 1e-5f, "justo en el borde del radio");
        }

        [Test]
        public void Burn_IsConfigurable_AndZeroWhenDisabled()
        {
            Assert.AreEqual(0f, FireMath.BurnDamage(0.1f, 0f, 1f, 0f, 1f), "dps 0 = sin quemadura");
            Assert.AreEqual(0f, FireMath.BurnDamage(0.1f, 0f, 0f, 4f, 1f), "radio 0 (antorcha) no quema");
            Assert.AreEqual(0f, FireMath.BurnDamage(0.1f, 0f, 1f, 4f, 0f));
            Assert.Greater(FireMath.BurnDamage(0.1f, 0f, 1f, 8f, 1f), FireMath.BurnDamage(0.1f, 0f, 1f, 4f, 1f));
        }

        [Test]
        public void StayingInTheFire_AccumulatesDamageOverTime()
        {
            float total = 0f;
            for (int i = 0; i < 12; i++) total += FireMath.BurnDamage(0.2f, 0f, 0.9f, 4f, 0.25f); // 3 s dentro
            Assert.AreEqual(12f, total, 1e-3f);
        }

        [Test]
        public void Profiles_AreSensible_TorchesDontBurn_CarsAreBiggest()
        {
            Assert.AreEqual(0f, FireProfile.For(FireKind.Torch).BurnRadius);
            foreach (var k in new[] { FireKind.Barrel, FireKind.Campfire, FireKind.Car })
            {
                Assert.Greater(FireProfile.For(k).BurnRadius, 0f);
                Assert.Greater(FireProfile.For(k).ScorchSize, 0f, "deja mancha de quemadura");
            }
            Assert.Greater(FireProfile.For(FireKind.Car).BurnRadius, FireProfile.For(FireKind.Campfire).BurnRadius);
            Assert.Greater(FireProfile.For(FireKind.Car).LightIntensity, FireProfile.For(FireKind.Torch).LightIntensity);
            Assert.Greater(FireProfile.For(FireKind.Car).SmokeRate, FireProfile.For(FireKind.Torch).SmokeRate);
        }

        [Test]
        public void MaxLights_ShrinksWithTheGovernorLevel()
        {
            Assert.AreEqual(6, FireMath.MaxLights(6, 0));
            Assert.AreEqual(4, FireMath.MaxLights(6, 1));
            Assert.AreEqual(2, FireMath.MaxLights(6, 2));
            Assert.AreEqual(1, FireMath.MaxLights(6, 3));
            Assert.AreEqual(0, FireMath.MaxLights(0, 2));
            Assert.AreEqual(1, FireMath.MaxLights(1, 3));
        }

        [Test]
        public void NearestLights_PicksTheClosest_RespectsTheCap_AndPriorityBreaksTies()
        {
            var pos = new List<V3> { new V3(20, 0, 0), new V3(3, 0, 0), new V3(10, 0, 0), new V3(2, 0, 5) };
            var near = FireMath.NearestLights(pos, null, new V3(0, 0, 0), 2);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, near);
            Assert.AreEqual(4, FireMath.NearestLights(pos, null, new V3(0, 0, 0), 99).Length);
            Assert.AreEqual(0, FireMath.NearestLights(pos, null, new V3(0, 0, 0), 0).Length);

            var tie = new List<V3> { new V3(5, 0, 0), new V3(-5, 0, 0) };
            var first = FireMath.NearestLights(tie, new[] { 1, 3 }, new V3(0, 0, 0), 1);
            Assert.AreEqual(1, first[0], "a igual distancia gana la de mayor prioridad (el coche)");
        }

        [Test]
        public void SmokeAndSparks_ShrinkWithTheLevel()
        {
            Assert.AreEqual(1f, FireMath.SmokeFactor(0));
            Assert.Greater(FireMath.SmokeFactor(0), FireMath.SmokeFactor(1));
            Assert.Greater(FireMath.SmokeFactor(1), FireMath.SmokeFactor(2));
            Assert.AreEqual(0f, FireMath.SmokeFactor(3));
            Assert.AreEqual(1f, FireMath.SparkFactor(1));
            Assert.AreEqual(0f, FireMath.SparkFactor(2));
        }
    }
}
