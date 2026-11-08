using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Anatomía por tipo, variantes y rasgos individuales de los zombis.</summary>
    public class ZombieLookTests
    {
        [Test]
        public void DefaultVariants_AreValid_AndEveryTypeHasThreeToFour()
        {
            var all = ZombieVariants.Defaults();
            Assert.IsEmpty(ZombieLookValidator.Validate(all));
            foreach (string t in ZombieAnatomies.Types)
            {
                int n = ZombieVariants.ForType(all, t).Count;
                Assert.That(n, Is.InRange(3, 4), t);
            }
        }

        [Test]
        public void Variants_OfOneType_DifferInHeadAndClothing()
        {
            var all = ZombieVariants.Defaults();
            foreach (string t in ZombieAnatomies.Types)
            {
                var list = ZombieVariants.ForType(all, t);
                var heads = new HashSet<ZombieHeadStyle>(); var clothes = new HashSet<ZombieClothing>();
                foreach (var v in list) { heads.Add(v.head); clothes.Add(v.clothing); }
                Assert.GreaterOrEqual(heads.Count, 3, t + ": cabezas distintas");
                Assert.GreaterOrEqual(clothes.Count, 2, t + ": ropas distintas");
            }
        }

        [Test]
        public void Validator_RejectsBadVariants()
        {
            var all = ZombieVariants.Defaults();
            all[0].typeId = "dragon";
            all[1].skinTone = new[] { 1f, 1f };
            all[2].eyeGlow = 4f;
            all[3].armor = ZombieArmor.DoorShield;
            all.Add(all[4].Clone());
            var errors = ZombieLookValidator.Validate(all);
            Assert.GreaterOrEqual(errors.Count, 5, string.Join("; ", errors));
        }

        [Test]
        public void Tank_IsHuge_Bulky_AndAsymmetric()
        {
            var tank = ZombieAnatomies.For(ZombieIds.Tank); var common = ZombieAnatomies.For(ZombieIds.Common);
            Assert.That(tank.Proportions.height, Is.InRange(2.5f, 2.8f));
            Assert.Greater(tank.Proportions.bulk, common.Proportions.bulk * 1.2f);
            Assert.Greater(tank.BigArmScale, 1.3f);
            Assert.IsTrue(tank.ScarPlates);
        }

        [Test]
        public void Runner_IsThinHunchedAndLongArmed()
        {
            var r = ZombieAnatomies.For(ZombieIds.Runner); var c = ZombieAnatomies.For(ZombieIds.Common);
            Assert.Less(r.Proportions.bulk, c.Proportions.bulk * 0.9f);
            Assert.Greater(r.Proportions.hunch, c.Proportions.hunch * 1.3f);
            Assert.Greater(r.Proportions.armRatio, c.Proportions.armRatio);
            Assert.IsTrue(r.VisibleRibs);
        }

        [Test]
        public void ScaleBuckets_AreMinusPlusSixPercent()
        {
            var lo = ZombieAnatomies.For(ZombieIds.Common, 0); var mid = ZombieAnatomies.For(ZombieIds.Common, 1); var hi = ZombieAnatomies.For(ZombieIds.Common, 2);
            Assert.AreEqual(-0.06f, lo.Proportions.height / mid.Proportions.height - 1f, 0.002f);
            Assert.AreEqual(0.06f, hi.Proportions.height / mid.Proportions.height - 1f, 0.002f);
        }

        [Test]
        public void EveryAnatomy_BuildsAValidSkeleton()
        {
            foreach (string t in ZombieAnatomies.Types)
                for (int b = 0; b < 3; b++)
                {
                    var a = ZombieAnatomies.For(t, b);
                    var spec = HumanoidRigSpec.Build(a.Proportions);
                    Assert.AreEqual(HumanoidRigSpec.BoneCount, spec.Length);
                    Assert.Greater(HumanoidRigSpec.LegLength(spec), 0.34f * a.Proportions.height, t);
                }
        }

        [Test]
        public void Individuals_AreDeterministic_AndDifferFromEachOther()
        {
            var v = ZombieVariants.Defaults()[0];
            var a = ZombieIndividual.Roll(5, 4, v); var b = ZombieIndividual.Roll(5, 4, v);
            Assert.AreEqual(a.WoundMask, b.WoundMask); Assert.AreEqual(a.Limp, b.Limp); Assert.AreEqual(a.VariantIndex, b.VariantIndex);
            var distinct = new HashSet<string>();
            for (int s = 0; s < 20; s++)
            {
                var z = ZombieIndividual.Roll(s, 4, v);
                distinct.Add(z.VariantIndex + "|" + z.WoundMask + "|" + z.Limp.ToString("0.00") + "|" + z.SpeedJitter.ToString("0.00"));
                Assert.That(z.SpeedJitter, Is.InRange(0.9f, 1.1f));
            }
            Assert.GreaterOrEqual(distinct.Count, 18, "20 individuos casi todos distintos");
        }
    }
}
