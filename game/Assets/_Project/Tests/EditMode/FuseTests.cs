using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class FuseTests
    {
        private static FuseBrain Brain() => new FuseBrain { TriggerRadius = 3f, CancelRadius = 5.5f, FuseSeconds = 1.35f, RetriggerDelay = 0.6f };

        [Test]
        public void ChasesUntilInsideTriggerRadius_ThenBurnsAndDetonates()
        {
            var b = Brain();
            Assert.AreEqual(FuseEvent.None, b.Tick(0.1f, 6f));
            Assert.AreEqual(FuseState.Chasing, b.State);
            Assert.AreEqual(FuseEvent.Started, b.Tick(0.1f, 2.9f));
            Assert.AreEqual(FuseState.Burning, b.State);
            FuseEvent last = FuseEvent.None; float t = 0f;
            while (last != FuseEvent.Detonate && t < 3f) { last = b.Tick(0.05f, 2f); t += 0.05f; }
            Assert.AreEqual(FuseEvent.Detonate, last);
            Assert.That(t, Is.InRange(1.25f, 1.45f), "la mecha dura ~1.35 s");
            Assert.AreEqual(FuseState.Exploded, b.State);
            Assert.AreEqual(FuseEvent.None, b.Tick(0.1f, 2f), "explota una sola vez");
        }

        [Test]
        public void Cancels_WhenPlayerRunsBeyondCancelRadius_AndResumesChasing()
        {
            var b = Brain();
            b.Tick(0.1f, 2.5f);
            b.Tick(0.5f, 4.5f);                       // entre ambos radios: sigue ardiendo (histéresis)
            Assert.AreEqual(FuseState.Burning, b.State);
            Assert.AreEqual(FuseEvent.Cancelled, b.Tick(0.1f, 6f));
            Assert.AreEqual(FuseState.Chasing, b.State);
            Assert.AreEqual(0f, b.Progress);
            Assert.AreEqual(FuseEvent.None, b.Tick(0.1f, 2f), "pausa corta antes de poder reactivarse");
            Assert.AreEqual(FuseEvent.Started, Until(b, FuseEvent.Started, 2f));
        }

        [Test]
        public void ReleasingAfterCancel_RestartsFuseFromZero()
        {
            var b = Brain();
            b.Tick(0.1f, 2f); b.Tick(1.0f, 2f);
            Assert.Greater(b.Progress, 0.6f);
            b.Tick(0.1f, 7f);
            Until(b, FuseEvent.Started, 2f);
            Assert.AreEqual(0f, b.Elapsed, 1e-4f);
        }

        [Test]
        public void KilledDuringFuse_DetonatesOnce_EvenAHeadshot()
        {
            var b = Brain();
            b.Tick(0.1f, 2f); b.Tick(0.3f, 2f);
            Assert.AreEqual(FuseEvent.Detonate, b.ForceDetonate());
            Assert.AreEqual(FuseEvent.None, b.ForceDetonate());
            var c = Brain();
            Assert.AreEqual(FuseEvent.Detonate, c.ForceDetonate(), "también explota si lo matan persiguiendo");
        }

        [Test]
        public void Visuals_InflateAndFlashFaster()
        {
            var b = Brain();
            Assert.AreEqual(1f, b.Inflate(0f), 1e-4f);
            b.Tick(0.1f, 2f);
            float early = b.Inflate(0f);
            b.Tick(1.0f, 2f);
            Assert.Greater(b.Inflate(0f), early);
            Assert.LessOrEqual(b.Inflate(0.01f), 1.5f);
            int flips0 = Flips(Brain(), 0.0f), flips1 = Flips(b, 1f);
            Assert.GreaterOrEqual(flips1, flips0);
        }

        private static int Flips(FuseBrain b, float progressAdvance)
        {
            if (b.State != FuseState.Burning) b.Tick(0.1f, 2f);
            int n = 0; bool prev = b.FlashWhite(0f);
            for (float t = 0f; t < 1f; t += 0.01f) { bool w = b.FlashWhite(t); if (w != prev) { n++; prev = w; } }
            return n;
        }

        [Test]
        public void BlastDamage_FallsOffAndWallReducesIt()
        {
            Assert.AreEqual(45f, Blast.Damage(0f, 3.6f, 45f, false, 0.25f));
            Assert.Greater(Blast.Damage(1f, 3.6f, 45f, false, 0.25f), Blast.Damage(3f, 3.6f, 45f, false, 0.25f));
            Assert.AreEqual(0f, Blast.Damage(3.6f, 3.6f, 45f, false, 0.25f));
            Assert.AreEqual(Blast.Damage(1f, 3.6f, 45f, false, 0.25f) * 0.25f, Blast.Damage(1f, 3.6f, 45f, true, 0.25f), 1e-4f);
            Assert.AreEqual(0f, Blast.Damage(1f, 3.6f, 45f, true, 0f), "un muro totalmente sólido lo anula");
            Assert.Greater(Blast.Knockback(1f, 3.6f, 9f, false), Blast.Knockback(1f, 3.6f, 9f, true));
            Assert.AreEqual(0f, Blast.Knockback(4f, 3.6f, 9f, false));
        }

        [Test]
        public void ExplosiveType_HasFuseDataAndValidates()
        {
            var e = ZombieCatalog.Get(ZombieIds.Explosive);
            Assert.That(e.fuseSeconds, Is.InRange(1.2f, 1.5f));
            Assert.AreEqual(3f, e.fuseTriggerRadius); Assert.Greater(e.fuseCancelRadius, e.fuseTriggerRadius);
            Assert.IsTrue(ZombieValidator.Validate(ZombieCatalog.Defaults()).Ok);
            var list = ZombieCatalog.Defaults();
            list.Find(z => z.id == ZombieIds.Explosive).fuseCancelRadius = 3.5f;
            Assert.IsFalse(ZombieValidator.Validate(list).Ok, "el radio de cancelación debe superar al de activación");
            var f = FuseBrain.For(e);
            Assert.AreEqual(e.fuseSeconds, f.FuseSeconds);
        }

        private static FuseEvent Until(FuseBrain b, FuseEvent wanted, float dist)
        {
            for (int i = 0; i < 100; i++) { var e = b.Tick(0.05f, dist); if (e == wanted) return e; }
            return FuseEvent.None;
        }
    }
}
