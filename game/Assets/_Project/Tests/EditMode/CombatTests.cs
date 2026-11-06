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

namespace SevenDays.Tests
{
    public class FeedbackMathTests
    {
        [Test]
        public void Bar_VisibleWhenDamagedRecentlyOrClose_NeverWhenDead()
        {
            Assert.IsTrue(FeedbackMath.BarVisible(1f, 30f, false, false));
            Assert.IsFalse(FeedbackMath.BarVisible(10f, 30f, false, false));
            Assert.IsTrue(FeedbackMath.BarVisible(10f, 5f, false, false));
            Assert.IsFalse(FeedbackMath.BarVisible(1f, 1f, true, false));
            Assert.IsFalse(FeedbackMath.BarVisible(10f, 7f, false, true), "con la vida llena solo se ve muy cerca");
            Assert.IsTrue(FeedbackMath.BarVisible(10f, 3f, false, true));
        }

        [Test]
        public void Bar_ColorGoesGreenYellowRed()
        {
            FeedbackMath.BarColor(1f, out float r1, out float g1, out _);
            FeedbackMath.BarColor(0.5f, out float r2, out float g2, out _);
            FeedbackMath.BarColor(0f, out float r3, out float g3, out _);
            Assert.Greater(g1, r1); Assert.Greater(r2, 0.8f); Assert.Greater(g2, 0.8f); Assert.Greater(r3, g3);
        }

        [Test]
        public void Smooth_ApproachesTargetWithoutOvershoot()
        {
            float v = 1f;
            for (int i = 0; i < 10; i++) { float n = FeedbackMath.Smooth(v, 0.2f, 0.016f); Assert.LessOrEqual(n, v); Assert.GreaterOrEqual(n, 0.2f); v = n; }
            Assert.AreEqual(1f, FeedbackMath.Smooth(1f, 0.2f, 0f), 1e-6f);
        }

        [Test]
        public void NumberKind_FollowsHeadReducedBody()
        {
            Assert.AreEqual(FeedbackMath.NumberKind.Head, FeedbackMath.NumberFor(new HitReport { Headshot = true, FinalDamage = 50, BaseDamage = 20 }));
            Assert.AreEqual(FeedbackMath.NumberKind.Reduced, FeedbackMath.NumberFor(new HitReport { Reduced = true, FinalDamage = 6, BaseDamage = 36 }));
            Assert.AreEqual(FeedbackMath.NumberKind.Body, FeedbackMath.NumberFor(new HitReport { FinalDamage = 36, BaseDamage = 36 }));
        }
    }
}

namespace SevenDays.Tests
{
    public class SpitterTests
    {
        private static SpitterBrain Brain(string allowed = "mixed") => new SpitterBrain { PreferredRange = 12f, Cooldown = 3f, AimSeconds = 0.9f, FlankAfterSeconds = 4f, Allowed = allowed };

        private static SpitterAction Until(SpitterBrain b, SpitterAction wanted, float dist, bool los, float cover, float maxSeconds = 10f)
        {
            SpitterAction a = SpitterAction.Idle;
            for (float t = 0; t < maxSeconds; t += 0.1f) { a = b.Tick(0.1f, dist, los, cover); if (a == wanted) return a; }
            return a;
        }

        [Test]
        public void ClearLine_AimsThenThrowsDirect()
        {
            var b = Brain();
            Assert.AreEqual(SpitterAction.Aim, Until(b, SpitterAction.Aim, 12f, true, 0f));
            Assert.AreEqual(ProjectileMode.Direct, b.Mode);
            Assert.AreEqual(SpitterAction.Throw, Until(b, SpitterAction.Throw, 12f, true, 0f));
        }

        [Test]
        public void BlockedLineWithPlayerInCover_ThrowsArc()
        {
            var b = Brain();
            Assert.AreEqual(SpitterAction.Aim, Until(b, SpitterAction.Aim, 12f, false, 2f));
            Assert.AreEqual(ProjectileMode.Arc, b.Mode);
        }

        [Test]
        public void BlockedLineButNotInCover_RepositionsInsteadOfThrowingAtAWall()
        {
            var b = Brain();
            Assert.AreEqual(SpitterAction.Reposition, Until(b, SpitterAction.Aim, 12f, false, 0f, 3f));
        }

        [Test]
        public void LongCoverTime_TriggersFlank_UnlessAlreadyHasLine()
        {
            var b = Brain();
            b.Tick(0.1f, 12f, false, 5f);
            Assert.AreEqual(SpitterAction.Flank, b.Tick(0.1f, 12f, false, 5f));
            var c = Brain();
            Assert.AreNotEqual(SpitterAction.Flank, c.Tick(0.1f, 12f, true, 9f), "con línea libre no hace falta flanquear");
        }

        [Test]
        public void Cooldown_SpacesThrows()
        {
            var b = Brain();
            Until(b, SpitterAction.Throw, 12f, true, 0f);
            for (int i = 0; i < 25; i++) Assert.AreNotEqual(SpitterAction.Aim, b.Tick(0.1f, 12f, true, 0f), "en los 2.5 s siguientes no apunta");
            Assert.AreEqual(SpitterAction.Aim, Until(b, SpitterAction.Aim, 12f, true, 0f, 2f));
        }

        [Test]
        public void KeepsDistance_AndOnlyArcTypeIgnoresLine()
        {
            var b = Brain();
            b.Tick(1.5f, 4f, true, 0f);
            Assert.AreEqual(SpitterAction.Reposition, b.Tick(0.1f, 4f, true, 0f), "demasiado cerca: retrocede");
            var direct = Brain("direct");
            Assert.AreNotEqual(SpitterAction.Aim, Until(direct, SpitterAction.Aim, 12f, false, 5f, 3f), "el directo no apunta si algo lo tapa");
            var arc = Brain("arc");
            Assert.AreEqual(SpitterAction.Aim, Until(arc, SpitterAction.Aim, 12f, false, 0f));
            Assert.AreEqual(ProjectileMode.Arc, arc.Mode);
        }

        [Test]
        public void CoverBlocksDirectButNeverArc()
        {
            Assert.AreEqual(0f, CoverRules.DamageAfterCover(ProjectileMode.Direct, 14f, true));
            Assert.AreEqual(14f, CoverRules.DamageAfterCover(ProjectileMode.Direct, 14f, false));
            Assert.AreEqual(14f, CoverRules.DamageAfterCover(ProjectileMode.Arc, 14f, true));
        }

        [Test]
        public void ArcLandsOnTarget()
        {
            Ballistics.LaunchVelocity(0f, 1.6f, 0f, 10f, 0f, 5f, 1.2f, Ballistics.Gravity, out float vx, out float vy, out float vz);
            float t = 1.2f;
            Assert.AreEqual(10f, Ballistics.PositionAt(0f, vx, Ballistics.Gravity, t, false), 1e-3f);
            Assert.AreEqual(5f, Ballistics.PositionAt(0f, vz, Ballistics.Gravity, t, false), 1e-3f);
            Assert.AreEqual(0f, Ballistics.PositionAt(1.6f, vy, Ballistics.Gravity, t, true), 1e-3f);
            Assert.That(Ballistics.FlightTime(3f, 15f), Is.InRange(1.0f, 1.5f));
            Assert.That(Ballistics.FlightTime(40f, 15f), Is.InRange(1.0f, 1.5f));
        }

        [Test]
        public void AreaDamage_FallsOffAndStopsAtRadius()
        {
            Assert.AreEqual(40f, AreaDamage.At(0f, 3f, 40f));
            Assert.AreEqual(10f, AreaDamage.At(3f - 1e-4f, 3f, 40f), 0.1f);
            Assert.AreEqual(0f, AreaDamage.At(3f, 3f, 40f));
            Assert.Greater(AreaDamage.At(1f, 3f, 40f), AreaDamage.At(2f, 3f, 40f));
        }

        [Test]
        public void Types_SpitterMixedExplosiveHasRadius_DropsMapToLootTables()
        {
            var sp = ZombieCatalog.Get(ZombieIds.Spitter); var ex = ZombieCatalog.Get(ZombieIds.Explosive);
            Assert.AreEqual("mixed", sp.projectile); Assert.Greater(ex.explosionRadius, 0f); Assert.Greater(ex.explosionDamage, 0f);
            foreach (var z in ZombieCatalog.Defaults()) CollectionAssert.Contains(LootRules.Zombies, z.dropKind, z.id);
            CollectionAssert.AreEquivalent(LootRules.Zombies, ZombieIds.DropKinds);
            var fast = ZombieCatalog.Get(ZombieIds.Runner); var tank = ZombieCatalog.Get(ZombieIds.Tank); var c = ZombieCatalog.Get(ZombieIds.Common);
            Assert.Greater(fast.speed, c.speed * 1.8f); Assert.Less(fast.maxHealth, c.maxHealth);
            Assert.Less(tank.speed, c.speed); Assert.Greater(tank.resistance, 0.2f); Assert.Greater(tank.maxHealth, 3 * c.maxHealth);
        }
    }
}

namespace SevenDays.Tests
{
    public class StaleListTests
    {
        [Test]
        public void PruneDead_RemovesNullAndDestroyedKeepsLive()
        {
            var list = new System.Collections.Generic.List<string> { "a", null, "dead", "b" };
            int removed = StaleList.PruneDead(list, s => s != "dead");
            Assert.AreEqual(2, removed);
            CollectionAssert.AreEqual(new[] { "a", "b" }, list);
            Assert.AreEqual(0, StaleList.PruneDead(list, s => true));
        }
    }
}
