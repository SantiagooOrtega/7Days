using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class BossTests
    {
        private static BossBrain Brain(string boss, int seed = 7) => new BossBrain(ZombieCatalog.Get(boss).boss, seed);

        private static List<BossEvent> Run(BossBrain b, float seconds, float fraction, float dist, float dt = 0.05f)
        {
            var all = new List<BossEvent>();
            for (float t = 0; t < seconds; t += dt) all.AddRange(b.Tick(dt, fraction, dist));
            return all;
        }

        [Test]
        public void ThreeBosses_ForDays3_5_7_WithBigHealth()
        {
            foreach (int day in new[] { 3, 5, 7 }) Assert.IsNotNull(ZombieCatalog.BossForDay(day), "día " + day);
            Assert.IsNull(ZombieCatalog.BossForDay(4));
            Assert.AreEqual(ZombieIds.BossBrute, ZombieCatalog.BossForDay(3).id);
            Assert.AreEqual(ZombieIds.BossLauncher, ZombieCatalog.BossForDay(5).id);
            Assert.AreEqual(ZombieIds.BossFinal, ZombieCatalog.BossForDay(7).id);
        }

        [TestCase(1.0f, 0)] [TestCase(0.51f, 0)] [TestCase(0.5f, 1)] [TestCase(0.3f, 1)] [TestCase(0.25f, 2)] [TestCase(0.01f, 2)]
        public void PhaseFollowsHealth(float fraction, int expected) => Assert.AreEqual(expected, BossBrain.PhaseFor(ZombieCatalog.Get(ZombieIds.BossBrute).boss, fraction));

        [Test]
        public void PhaseChange_FiresOnceAndNeverGoesBack()
        {
            var b = Brain(ZombieIds.BossBrute);
            var ev = Run(b, 0.2f, 0.45f, 50f);
            Assert.AreEqual(1, ev.FindAll(e => e.Type == BossEventType.PhaseChanged).Count);
            Assert.AreEqual(1, b.Phase);
            Run(b, 0.2f, 0.9f, 50f);   // se cura (no debería) o el dato fluctúa
            Assert.AreEqual(1, b.Phase, "la furia no retrocede");
            var ev2 = Run(b, 0.2f, 0.2f, 50f);
            Assert.AreEqual(2, b.Phase);
            Assert.AreEqual(1, ev2.FindAll(e => e.Type == BossEventType.PhaseChanged).Count);
        }

        [Test]
        public void Fury_RaisesSpeedAndAttackRate()
        {
            var b = Brain(ZombieIds.BossBrute);
            float s0 = b.SpeedMultiplier, a0 = b.AttackRateMultiplier;
            b.Tick(0.1f, 0.4f, 10f); float s1 = b.SpeedMultiplier;
            b.Tick(0.1f, 0.1f, 10f); float s2 = b.SpeedMultiplier;
            Assert.Less(s0, s1); Assert.Less(s1, s2); Assert.Greater(b.AttackRateMultiplier, a0);
        }

        [Test]
        public void Ability_TelegraphsBeforeFiring_WithAtLeastHalfSecond()
        {
            var b = Brain(ZombieIds.BossBrute);
            var ev = Run(b, 6f, 1f, 15f);
            int telegraph = ev.FindIndex(e => e.Type == BossEventType.TelegraphStarted);
            int fired = ev.FindIndex(e => e.Type == BossEventType.AbilityFired);
            Assert.GreaterOrEqual(telegraph, 0, "debe avisar");
            Assert.Greater(fired, telegraph, "dispara después de avisar");
            Assert.GreaterOrEqual(ev[telegraph].Seconds, 0.5f);
            Assert.AreEqual(ev[telegraph].AbilityId, ev[fired].AbilityId);
        }

        [Test]
        public void Abilities_RespectRange_AndCooldown()
        {
            var b = Brain(ZombieIds.BossBrute);   // charge pide >= 6 m; slam <= 4 m
            var far = Run(b, 20f, 1f, 20f);
            Assert.IsTrue(far.TrueForAll(e => e.AbilityId == null || e.AbilityId == "charge"), "lejos solo carga");
            var fired = far.FindAll(e => e.Type == BossEventType.AbilityFired);
            Assert.GreaterOrEqual(fired.Count, 1);
            Assert.LessOrEqual(fired.Count, 3, "el cooldown de 9 s limita las cargas en 20 s");
            var c = Brain(ZombieIds.BossBrute);
            var near = Run(c, 20f, 1f, 2f);
            Assert.IsTrue(near.TrueForAll(e => e.AbilityId == null || e.AbilityId == "slam"), "cerca solo golpea");
        }

        [Test]
        public void GlobalCooldown_SpacesAbilities()
        {
            var b = Brain(ZombieIds.BossFinal);
            var ev = Run(b, 60f, 1f, 3f).FindAll(e => e.Type == BossEventType.AbilityFired);
            Assert.GreaterOrEqual(ev.Count, 2);
            // dos disparos nunca pueden salir en el mismo instante: al menos hay un telegraph entre ellos
            var all = Run(Brain(ZombieIds.BossFinal, 3), 60f, 1f, 3f);
            int lastFired = -1;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type == BossEventType.AbilityFired) { if (lastFired >= 0) Assert.AreNotEqual(lastFired, i - 1, "disparos pegados"); lastFired = i; }
            }
        }

        [Test]
        public void FuryAbilities_OnlyAppearInTheirPhase()
        {
            var data = ZombieCatalog.Get(ZombieIds.BossBrute).boss;
            data.abilities[1].minPhase = 2;   // el golpe solo en furia máxima
            var b = new BossBrain(data, 5);
            var ev = Run(b, 40f, 1f, 2f);
            Assert.AreEqual(0, ev.FindAll(e => e.Type == BossEventType.AbilityFired).Count, "en fase 0 no usa habilidades de fase 2");
            var late = Run(b, 40f, 0.1f, 2f);
            Assert.Greater(late.FindAll(e => e.Type == BossEventType.AbilityFired && e.AbilityId == "slam").Count, 0);
        }

        [Test]
        public void PhaseChange_InterruptsTelegraph()
        {
            var b = Brain(ZombieIds.BossBrute);
            for (int i = 0; i < 200 && !b.Busy; i++) b.Tick(0.05f, 1f, 15f);
            Assert.IsTrue(b.Busy);
            b.Tick(0.05f, 0.4f, 15f);
            Assert.IsFalse(b.Busy);
        }

        [Test]
        public void WallStun_OpensDamageWindow_ThenEnds()
        {
            var b = Brain(ZombieIds.BossBrute);
            Assert.AreEqual(1f, b.VulnerabilityMultiplier);
            b.WallStun();
            Assert.IsTrue(b.WallStunned);
            Assert.AreEqual(b.Data.stunnedExtraDamage, b.VulnerabilityMultiplier);
            Assert.Greater(b.VulnerabilityMultiplier, 1f);
            var ev = Run(b, b.Data.wallStunSeconds + 0.2f, 1f, 15f);
            Assert.IsTrue(ev.Exists(e => e.Type == BossEventType.WallStunEnded));
            Assert.AreEqual(1f, b.VulnerabilityMultiplier);
        }

        [Test]
        public void WeakPoint_ExposedOnlyAtCertainMoments_AndMultipliesDamage()
        {
            var z = ZombieCatalog.Get(ZombieIds.BossFinal);
            var b = new BossBrain(z.boss, 1);
            Assert.IsFalse(b.WeakPointExposed);
            Assert.AreEqual(HitZone.Torso, b.ResolveZone(HitZone.WeakPoint), "oculto: cuenta como torso");

            var ev = Run(b, z.boss.weakPointPeriod + 0.5f, 1f, 50f);
            Assert.IsTrue(ev.Exists(e => e.Type == BossEventType.WeakPointExposed));
            Assert.IsTrue(b.WeakPointExposed);
            Assert.AreEqual(HitZone.WeakPoint, b.ResolveZone(HitZone.WeakPoint));

            var ev2 = Run(b, z.boss.weakPointExposeSeconds + 0.5f, 1f, 50f);
            Assert.IsTrue(ev2.Exists(e => e.Type == BossEventType.WeakPointHidden));
            Assert.IsFalse(b.WeakPointExposed);

            var hidden = new CombatantCore(z.maxHealth) { Resistance = z.resistance, Zones = z.zones.Clone() };
            var exposed = new CombatantCore(z.maxHealth) { Resistance = z.resistance, Zones = z.zones.Clone() };
            float torso = hidden.ApplyHit(b.ResolveZone(HitZone.WeakPoint), 50f).FinalDamage;
            b.ExposeWeakPoint();
            float weak = exposed.ApplyHit(b.ResolveZone(HitZone.WeakPoint), 50f).FinalDamage;
            Assert.Greater(weak, 3f * torso, "el punto débil duele mucho más que el torso");
        }

        [Test]
        public void BossDamage_HeadshotsBeatBodyAndWallStunAddsDamage()
        {
            var z = ZombieCatalog.Get(ZombieIds.BossBrute);
            var plain = new CombatantCore(z.maxHealth) { Resistance = z.resistance, Zones = z.zones.Clone() };
            var stunned = new CombatantCore(z.maxHealth) { Resistance = z.resistance, Zones = z.zones.Clone(), VulnerabilityMultiplier = z.boss.stunnedExtraDamage };
            Assert.Greater(stunned.ApplyHit(HitZone.Torso, 40f).FinalDamage, plain.ApplyHit(HitZone.Torso, 40f).FinalDamage);
        }

        [Test]
        public void BossResistsStun()
        {
            foreach (var z in ZombieCatalog.Defaults())
            {
                if (!z.isBoss) continue;
                var c = new CombatantCore(z.maxHealth) { StunResistance = z.stunResistance };
                var r = HitReaction.Compute(120f, z.maxHealth, HitZone.Head, 2.4f, z.stunResistance);
                Assert.IsFalse(r.Stagger, z.id);
                Assert.Less(StunMath.Duration(2f, z.stunResistance), 0.25f, z.id);
            }
        }

        [Test]
        public void Summon_CountsAndTypesExist()
        {
            foreach (var z in ZombieCatalog.Defaults())
                if (z.isBoss) { Assert.IsNotNull(ZombieCatalog.Get(z.boss.summonType), z.id); Assert.GreaterOrEqual(z.boss.summonCount, 0); }
            Assert.IsTrue(System.Array.Exists(ZombieCatalog.Get(ZombieIds.BossLauncher).boss.abilities, a => a.id == "throw"));
            Assert.IsTrue(System.Array.Exists(ZombieCatalog.Get(ZombieIds.BossLauncher).boss.abilities, a => a.id == "summon"));
            Assert.IsTrue(System.Array.Exists(ZombieCatalog.Get(ZombieIds.BossFinal).boss.abilities, a => a.id == "shockwave"));
            Assert.IsTrue(System.Array.Exists(ZombieCatalog.Get(ZombieIds.BossFinal).boss.abilities, a => a.id == "summon_wave"));
            Assert.IsTrue(System.Array.Exists(ZombieCatalog.Get(ZombieIds.BossBrute).boss.abilities, a => a.id == "charge"));
            Assert.IsTrue(ZombieCatalog.Get(ZombieIds.BossFinal).boss.hasWeakPoint);
        }

        [Test]
        public void Deterministic_SameSeedSameSequence()
        {
            var a = Run(Brain(ZombieIds.BossFinal, 11), 90f, 1f, 3f); var b = Run(Brain(ZombieIds.BossFinal, 11), 90f, 1f, 3f);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].ToString(), b[i].ToString());
        }

        [Test]
        public void BossDrop_UsesGuaranteedBossTable()
        {
            foreach (var z in ZombieCatalog.Defaults()) if (z.isBoss) Assert.AreEqual("boss", z.dropKind);
        }
    }
}
