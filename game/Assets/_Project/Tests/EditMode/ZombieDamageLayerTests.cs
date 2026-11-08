using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class ZombieDamageLayerTests
    {
        private static List<KeyValuePair<HitZone, int>> Step(ZombieDamageLayer l, float hp, HitZone z) { var r = new List<KeyValuePair<HitZone, int>>(); l.Update(hp, z, r); return r; }

        [Test]
        public void NothingRevealed_AboveSeventyFivePercent()
        {
            var l = new ZombieDamageLayer();
            Assert.IsEmpty(Step(l, 1f, HitZone.Torso)); Assert.IsEmpty(Step(l, 0.8f, HitZone.Head));
            Assert.AreEqual(0, l.QuartersLost);
        }

        [Test]
        public void EachQuarter_RevealsOneWoundInTheZoneThatWasHit()
        {
            var l = new ZombieDamageLayer();
            var a = Step(l, 0.74f, HitZone.Torso); Assert.AreEqual(1, a.Count); Assert.AreEqual(HitZone.Torso, a[0].Key); Assert.AreEqual(1, a[0].Value);
            Assert.IsEmpty(Step(l, 0.6f, HitZone.Torso), "entre cuartos no aparece nada");
            var b = Step(l, 0.49f, HitZone.Head); Assert.AreEqual(HitZone.Head, b[0].Key); Assert.AreEqual(1, b[0].Value);
            var c = Step(l, 0.2f, HitZone.Legs); Assert.AreEqual(HitZone.Legs, c[0].Key);
            Assert.AreEqual(3, l.QuartersLost);
        }

        [Test]
        public void OneBigHit_RevealsSeveralStagesInTheSameZone()
        {
            var l = new ZombieDamageLayer();
            var r = Step(l, 0.2f, HitZone.Arms);
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual(1, r[0].Value); Assert.AreEqual(2, r[1].Value); Assert.AreEqual(3, r[2].Value);
            foreach (var x in r) Assert.AreEqual(HitZone.Arms, x.Key);
        }

        [Test]
        public void ResetStartsClean()
        {
            var l = new ZombieDamageLayer(); Step(l, 0.1f, HitZone.Head); l.Reset();
            Assert.AreEqual(0, l.QuartersLost); Assert.AreEqual(0, l.StageOf(HitZone.Head));
            Assert.AreEqual(1, Step(l, 0.7f, HitZone.Head).Count);
        }

        [Test]
        public void WeakPointCountsAsTorso()
        {
            var l = new ZombieDamageLayer();
            Assert.AreEqual(HitZone.Torso, Step(l, 0.7f, HitZone.WeakPoint)[0].Key);
        }
    }
}
