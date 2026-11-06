using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class CombatTests
    {
        [Test]
        public void ZoneTable_HasTheAgreedDefaults()
        {
            var z = new ZoneMultipliers();
            Assert.AreEqual(2.5f, z.For(HitZone.Head));
            Assert.AreEqual(1.0f, z.For(HitZone.Torso));
            Assert.AreEqual(0.6f, z.For(HitZone.Arms));
            Assert.AreEqual(0.7f, z.For(HitZone.Legs));
            Assert.Greater(z.For(HitZone.WeakPoint), z.For(HitZone.Head));
        }

        [Test]
        public void ZoneTable_IsConfigurablePerType()
        {
            var armored = ZombieCatalog.Get(ZombieIds.Armored);
            var common = ZombieCatalog.Get(ZombieIds.Common);
            Assert.Less(armored.zones.torso, common.zones.torso, "el blindado resiste el torso");
            Assert.AreEqual(common.zones.head, armored.zones.head, "pero la cabeza sigue vulnerable");
        }

        [Test]
        public void Final_FollowsTheFormula()
        {
            // 20 base x 0.5 falloff x 2.5 cabeza x (1 - 0.2) = 20
            Assert.AreEqual(20, DamageModel.Final(20f, 0.5f, 2.5f, 0.2f));
            Assert.AreEqual(30, DamageModel.Final(30f, 1f, 1f, 0f));
        }

        [Test]
        public void Final_RoundsHalvesUpAndNeverGoesNegative()
        {
            Assert.AreEqual(3, DamageModel.Final(1f, 1f, 2.5f, 0f));   // 2.5 -> 3 (estable)
            Assert.AreEqual(0, DamageModel.Final(-10f, 1f, 1f, 0f));
            Assert.AreEqual(0, DamageModel.Final(10f, 1f, -1f, 0f));
            Assert.AreEqual(1, DamageModel.Final(1f, 1f, 0.1f, 0.9f), "daño positivo nunca se redondea a 0");
        }

        [Test]
        public void ArmorPenetration_ReducesResistanceButNotBelowZero()
        {
            Assert.AreEqual(0f, DamageModel.EffectiveResistance(0.3f, 0.5f));
            Assert.AreEqual(0.2f, DamageModel.EffectiveResistance(0.5f, 0.3f), 1e-5f);
            Assert.Greater(DamageModel.Final(50f, 1f, 1f, 0.5f, 0.4f), DamageModel.Final(50f, 1f, 1f, 0.5f, 0f));
        }

        [Test]
        public void Falloff_IsFlatThenLinearToMinimum()
        {
            Assert.AreEqual(1f, DamageModel.Falloff(5f, 100f, 0.4f, 0.5f));
            Assert.AreEqual(1f, DamageModel.Falloff(40f, 100f, 0.4f, 0.5f), 1e-5f);
            Assert.AreEqual(0.75f, DamageModel.Falloff(70f, 100f, 0.4f, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, DamageModel.Falloff(100f, 100f, 0.4f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, DamageModel.Falloff(500f, 100f, 0.4f, 0.5f), 1e-5f);
        }

        [Test]
        public void EachZoneForwardsItsOwnMultiplier()
        {
            foreach (HitZone zone in new[] { HitZone.Head, HitZone.Torso, HitZone.Arms, HitZone.Legs })
            {
                var c = new CombatantCore(1000f);
                var r = c.ApplyHit(zone, 20f);
                Assert.AreEqual(zone, r.Zone);
                Assert.AreEqual(System.Math.Round(20f * c.Zones.For(zone)), r.FinalDamage, 0.01f, zone.ToString());
                Assert.AreEqual(1000f - r.FinalDamage, c.Current, 0.01f);
                Assert.AreEqual(zone == HitZone.Head, r.Headshot);
            }
        }

        [Test]
        public void Headshot_UsesWeaponMultiplierToo()
        {
            var a = new CombatantCore(1000f); var b = new CombatantCore(1000f);
            float plain = a.ApplyHit(HitZone.Head, 20f, 0f, 1f).FinalDamage;
            float boosted = b.ApplyHit(HitZone.Head, 20f, 0f, 1.5f).FinalDamage;
            Assert.Greater(boosted, plain);
            var t = new CombatantCore(1000f);
            Assert.AreEqual(20f, t.ApplyHit(HitZone.Torso, 20f, 0f, 3f).FinalDamage, "el multiplicador del arma solo afecta a la cabeza");
        }

        [Test]
        public void Combatant_DiesOnceAndReportsHeadshotKill()
        {
            var c = new CombatantCore(50f);
            var r1 = c.ApplyHit(HitZone.Torso, 20f);
            Assert.IsFalse(r1.Killed); Assert.AreEqual(HealthState.Alive, c.State);
            var r2 = c.ApplyHit(HitZone.Head, 20f);
            Assert.IsTrue(r2.Killed); Assert.IsTrue(c.IsDead); Assert.IsTrue(c.KilledByHeadshot);
            var r3 = c.ApplyHit(HitZone.Head, 20f);
            Assert.IsFalse(r3.Valid, "un muerto ya no recibe impactos");
            Assert.AreEqual(0f, c.Current);
        }

        [Test]
        public void Resistance_ReducesAndMarksReduced()
        {
            var c = new CombatantCore(1000f) { Resistance = 0.5f };
            var r = c.ApplyHit(HitZone.Torso, 40f);
            Assert.AreEqual(20f, r.FinalDamage);
            Assert.IsTrue(r.Reduced);
        }

        [Test]
        public void ApplyResolved_SkipsZoneAndResistance()
        {
            var c = new CombatantCore(100f) { Resistance = 0.9f };
            c.ApplyResolved(40f);
            Assert.AreEqual(60f, c.Current);
        }

        [Test]
        public void Stun_AliveToStunnedToAlive_AndResistanceCutsIt()
        {
            var c = new CombatantCore(100f);
            Assert.IsTrue(c.Stun(0.5f)); Assert.AreEqual(HealthState.Stunned, c.State);
            c.Tick(0.3f); Assert.AreEqual(HealthState.Stunned, c.State);
            c.Tick(0.3f); Assert.AreEqual(HealthState.Alive, c.State);
            var boss = new CombatantCore(100f) { StunResistance = 1f };
            Assert.IsFalse(boss.Stun(5f)); Assert.AreEqual(HealthState.Alive, boss.State);
        }

        [Test]
        public void Dead_CannotBeStunnedOrHealed()
        {
            var c = new CombatantCore(10f);
            c.ApplyHit(HitZone.Torso, 50f);
            Assert.IsFalse(c.Stun(1f)); c.Heal(5f);
            Assert.AreEqual(0f, c.Current); Assert.AreEqual(HealthState.Dead, c.State);
        }

        [Test]
        public void HitReaction_StrongerWithShotgunAndHead_AndZeroForBosses()
        {
            var pistol = HitReaction.Compute(20f, 100f, HitZone.Torso, 0.8f, 0f);
            var shotgun = HitReaction.Compute(60f, 100f, HitZone.Torso, 2.4f, 0f);
            var head = HitReaction.Compute(60f, 100f, HitZone.Head, 2.4f, 0f);
            Assert.Greater(shotgun.KnockbackMeters, pistol.KnockbackMeters);
            Assert.Greater(head.StunSeconds, shotgun.StunSeconds);
            var boss = HitReaction.Compute(60f, 1500f, HitZone.Head, 2.4f, 0.95f);
            Assert.IsFalse(boss.Stagger);
        }

        [Test]
        public void Vulnerability_ScalesDamage()
        {
            var c = new CombatantCore(1000f) { VulnerabilityMultiplier = 1.5f };
            Assert.AreEqual(30f, c.ApplyHit(HitZone.Torso, 20f).FinalDamage);
        }
    }
}

namespace SevenDays.Tests
{
    public class ZombieStateMachineTests
    {
        private static ZombieStateMachine Make() => new ZombieStateMachine { AggroRange = 18f, AttackRange = 1.5f };

        [Test]
        public void PatrolsUntilItSeesThePlayer_ThenChasesThenAttacks()
        {
            var m = Make();
            Assert.AreEqual(ZombieState.Patrol, m.Tick(0.1f, 30f, true, false, false));
            Assert.AreEqual(ZombieState.Patrol, m.Tick(0.1f, 10f, false, false, false), "sin visión no persigue");
            Assert.AreEqual(ZombieState.Chase, m.Tick(0.1f, 10f, true, false, false));
            Assert.AreEqual(ZombieState.Attack, m.Tick(0.1f, 1.2f, true, false, false));
        }

        [Test]
        public void HasHysteresis()
        {
            var m = Make();
            m.Tick(0.1f, 10f, true, false, false);
            Assert.AreEqual(ZombieState.Chase, m.Tick(0.1f, 20f, false, false, false), "a 20 m aún no lo pierde (18 x 1.4 = 25.2)");
            Assert.AreEqual(ZombieState.Patrol, m.Tick(0.1f, 26f, false, false, false));
            m.Tick(0.1f, 5f, true, false, false); m.Tick(0.1f, 1f, true, false, false);
            Assert.AreEqual(ZombieState.Attack, m.Tick(0.1f, 1.7f, true, false, false), "1.7 < 1.5 x 1.25: sigue atacando");
            Assert.AreEqual(ZombieState.Chase, m.Tick(0.1f, 2.0f, true, false, false));
        }

        [Test]
        public void StunAndDeathOverrideAndRecover()
        {
            var m = Make();
            m.Tick(0.1f, 5f, true, false, false);
            Assert.AreEqual(ZombieState.Stunned, m.Tick(0.1f, 5f, true, true, false));
            Assert.AreEqual(ZombieState.Chase, m.Tick(0.1f, 5f, true, false, false));
            Assert.AreEqual(ZombieState.Dead, m.Tick(0.1f, 5f, true, false, true));
            Assert.AreEqual(ZombieState.Dead, m.Tick(0.1f, 0.5f, true, true, true));
        }

        [Test]
        public void ReturnsToPatrolWhenPlayerDies()
        {
            var m = Make();
            m.Tick(0.1f, 5f, true, false, false);
            Assert.AreEqual(ZombieState.Patrol, m.Tick(0.1f, 5f, true, false, false, playerDead: true));
        }
    }
}
