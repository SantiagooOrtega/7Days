using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Fallan si el balance de armas contra un zombi común (100 PV) sale de los rangos acordados. Los números reales están en docs/BALANCE.md.</summary>
    public class BalanceTests
    {
        private static readonly ZombieTypeData Common = ZombieCatalog.Get(ZombieIds.Common);

        private static int Shots(string weapon, HitZone zone, float distance) => DamageModel.ShotsToKill(WeaponCatalog.Get(weapon), Common, zone, distance);

        [Test] public void Common_Has100Health() => Assert.AreEqual(100f, Common.maxHealth);

        [Test] public void Pistol_TwoHeadshots() => Assert.AreEqual(2, Shots("pistol", HitZone.Head, 10f));
        [Test] public void Pistol_AboutSixBodyShots() { int n = Shots("pistol", HitZone.Torso, 10f); Assert.That(n, Is.InRange(5, 7)); }
        [Test] public void Shotgun_OneOrTwoShotsCloseUp() { Assert.That(Shots("shotgun", HitZone.Torso, 3f), Is.InRange(1, 2)); Assert.That(Shots("shotgun", HitZone.Head, 3f), Is.InRange(1, 2)); }
        [Test] public void Shotgun_UselessFarAway() => Assert.Greater(Shots("shotgun", HitZone.Torso, 28f), 6);
        [Test] public void AssaultRifle_TwoOrThreeBodyShots() => Assert.That(Shots("assault_rifle", HitZone.Torso, 10f), Is.InRange(2, 3));
        [Test] public void Sniper_OneHeadshotAtAnyRange() { foreach (float d in new[] { 5f, 60f, 130f, 260f }) Assert.AreEqual(1, Shots("sniper", HitZone.Head, d), "d=" + d); }

        [Test]
        public void HeadIsAlwaysBetterThanBody()
        {
            foreach (var w in WeaponCatalog.Defaults())
            {
                float d = w.IsMelee ? 1f : 10f;
                Assert.LessOrEqual(DamageModel.ShotsToKill(w, Common, HitZone.Head, d), DamageModel.ShotsToKill(w, Common, HitZone.Torso, d), w.id);
                Assert.Greater(DamageModel.PerShot(w, Common, HitZone.Head, d), DamageModel.PerShot(w, Common, HitZone.Torso, d), w.id);
            }
        }

        [Test]
        public void DamageNeverIncreasesWithDistance()
        {
            foreach (var w in WeaponCatalog.Defaults())
            {
                if (w.IsMelee) continue;
                int last = int.MaxValue;
                for (float d = 0f; d <= w.range; d += w.range / 20f)
                {
                    int dmg = DamageModel.PerShot(w, Common, HitZone.Torso, d);
                    Assert.LessOrEqual(dmg, last, w.id + " d=" + d);
                    Assert.Greater(dmg, 0, w.id);
                    last = dmg;
                }
            }
        }

        [Test]
        public void EveryWeaponKillsEveryNormalZombieEventually()
        {
            foreach (var w in WeaponCatalog.Defaults())
                foreach (var z in ZombieCatalog.Defaults())
                    if (!z.isBoss) Assert.Less(DamageModel.ShotsToKill(w, z, HitZone.Head, w.IsMelee ? 1f : 10f), 80, w.id + " vs " + z.id);
        }

        [Test]
        public void TankNeedsMoreFirepowerThanCommon_AndArmoredPunishesBodyShots()
        {
            var tank = ZombieCatalog.Get(ZombieIds.Tank); var armored = ZombieCatalog.Get(ZombieIds.Armored);
            var pistol = WeaponCatalog.Get("pistol");
            Assert.Greater(DamageModel.ShotsToKill(pistol, tank, HitZone.Torso, 10f), 20);
            Assert.Greater(DamageModel.ShotsToKill(pistol, armored, HitZone.Torso, 10f), 4 * DamageModel.ShotsToKill(pistol, armored, HitZone.Head, 10f));
        }

        [Test]
        public void BossesHave10To30TimesCommonHealth()
        {
            foreach (var b in ZombieCatalog.Defaults()) if (b.isBoss) Assert.That(b.maxHealth / Common.maxHealth, Is.InRange(10f, 30f), b.id);
        }

        [Test]
        public void Validator_AcceptsDefaultsAndRejectsBadData()
        {
            var ok = ZombieValidator.Validate(ZombieCatalog.Defaults());
            Assert.IsTrue(ok.Ok, string.Join("; ", ok.Errors));
            var bad = ZombieCatalog.Defaults();
            bad[0].zones.head = -1f; bad[1].resistance = 2f; bad[2].dropKind = "nope";
            Assert.IsFalse(ZombieValidator.Validate(bad).Ok);
            var weakBoss = ZombieCatalog.Defaults(); foreach (var z in weakBoss) if (z.isBoss) z.maxHealth = 300f;
            Assert.IsFalse(ZombieValidator.Validate(weakBoss).Ok, "un jefe con 3x la vida no pasa");
        }
    }
}
