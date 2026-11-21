using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Granada: daño de área con caída, cobertura sólida, efecto por tipo de zombi, al jugador solo muy cerca, conteo 2/6, drops y trayectoria.</summary>
    public class GrenadeTests
    {
        [Test]
        public void AreaDamage_FallsOffWithDistance_ZeroOutsideTheRadius()
        {
            float prev = float.MaxValue;
            for (float d = 0f; d < GrenadeRules.Radius; d += 0.5f) { float v = GrenadeRules.Damage(d, false); Assert.Less(v, prev + 1e-4f); Assert.Greater(v, 0f); prev = v; }
            Assert.AreEqual(GrenadeRules.MaxDamage, GrenadeRules.Damage(0f, false), 1e-3f);
            Assert.AreEqual(0f, GrenadeRules.Damage(GrenadeRules.Radius, false));
            Assert.AreEqual(0f, GrenadeRules.Damage(GrenadeRules.Radius + 3f, false));
            Assert.AreEqual(6f, GrenadeRules.Radius, 1e-4f, "~6 m de radio");
            Assert.Greater(GrenadeRules.Push(1f, false), GrenadeRules.Push(5f, false));
        }

        [Test]
        public void SolidCover_BlocksTheBlast()
        {
            Assert.AreEqual(0f, GrenadeRules.Damage(1f, true));
            Assert.AreEqual(0f, GrenadeRules.PlayerDamage(1f, true));
            Assert.AreEqual(0f, GrenadeRules.Push(1f, true));
        }

        [Test]
        public void KillsCommons_HurtsTanksAndArmored_BarelyScratchesBosses()
        {
            var common = ZombieCatalog.Get(ZombieIds.Common); var tank = ZombieCatalog.Get(ZombieIds.Tank); var armored = ZombieCatalog.Get(ZombieIds.Armored); var boss = ZombieCatalog.Get(ZombieIds.BossBrute);
            float Hit(ZombieTypeData z, float d) => GrenadeRules.Damage(d, false, GrenadeRules.TypeFactor(z.id, z.isBoss)) * (1f - z.resistance);
            for (float d = 0f; d <= 4.5f; d += 0.5f) Assert.GreaterOrEqual(Hit(common, d), common.maxHealth, "un común a " + d + " m muere");
            Assert.That(Hit(tank, 0.5f) / tank.maxHealth, Is.InRange(0.3f, 0.6f), "bastante a un tanque");
            Assert.That(Hit(armored, 0.5f) / armored.maxHealth, Is.InRange(0.6f, 0.99f), "bastante a un blindado");
            Assert.Less(Hit(boss, 0f) / boss.maxHealth, 0.05f, "poco a un jefe");
        }

        [Test]
        public void Player_OnlyHurtWhenVeryClose()
        {
            Assert.Greater(GrenadeRules.PlayerDamage(1f, false), 50f);
            Assert.AreEqual(0f, GrenadeRules.PlayerDamage(GrenadeRules.SelfRadius, false));
            Assert.AreEqual(0f, GrenadeRules.PlayerDamage(5.5f, false), "a 5.5 m (dentro del radio de zombis) no daña al jugador");
            Assert.Less(GrenadeRules.PlayerDamage(1f, false), GrenadeRules.Damage(1f, false));
        }

        [Test]
        public void Pouch_StartsWith2_MaxIs6_AndUsesOne()
        {
            var p = new GrenadePouch();
            Assert.AreEqual(2, p.Count);
            Assert.AreEqual(4, p.Add(10)); Assert.AreEqual(6, p.Count); Assert.AreEqual(0, p.Add(1), "lleno: el pickup no se consume");
            for (int i = 0; i < 6; i++) Assert.IsTrue(p.TryUse());
            Assert.IsFalse(p.TryUse()); Assert.AreEqual(0, p.Count);
        }

        [Test]
        public void Drops_RareOnCommons_MoreOnTanks_AlwaysOnBosses()
        {
            Assert.Less(GrenadeRules.DropChance("common"), 0.05);
            Assert.Greater(GrenadeRules.DropChance("tank"), GrenadeRules.DropChance("armored"));
            Assert.Greater(GrenadeRules.DropChance("armored"), GrenadeRules.DropChance("common"));
            Assert.AreEqual(1.0, GrenadeRules.DropChance("boss")); Assert.AreEqual(2, GrenadeRules.DropAmount("boss"));
        }

        [Test]
        public void Arc_FallsUnderGravity_AndThrowsAReasonableDistance()
        {
            var buf = new V3[60];
            var v = GrenadeRules.ThrowVelocity(new V3(0f, 0f, 1f));
            int n = GrenadeRules.Arc(new V3(0f, 1.5f, 0f), v, 0.05f, buf, 0f);
            Assert.Greater(n, 10);
            Assert.Greater(buf[n / 3].Y, 1.5f, "sube primero");
            Assert.Less(buf[n - 1].Y, 0.01f, "acaba en el suelo");
            Assert.That(buf[n - 1].Z, Is.InRange(12f, 25f), "lanzamiento a 12-25 m en llano");
        }
    }
}
