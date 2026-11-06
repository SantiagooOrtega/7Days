using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class DirectorApiTests
    {
        [Test]
        public void Metrics_AccuracyHeadshotsAndTtk()
        {
            var m = new CombatMetrics();
            for (int i = 0; i < 10; i++) m.RecordShot();
            for (int i = 0; i < 6; i++) m.RecordHit(i < 2);
            m.RecordKill(true, 1.5f); m.RecordKill(false, 3.5f);
            var s = m.Snapshot();
            Assert.AreEqual(0.6f, s.accuracy, 1e-5f);
            Assert.AreEqual(2f / 6f, s.headshotRatio, 1e-5f);
            Assert.AreEqual(2.5f, s.averageTimeToKill, 1e-5f);
            Assert.AreEqual(2, s.kills); Assert.AreEqual(1, s.headshotKills);
        }

        [Test]
        public void Metrics_DamageBySourceAndCoverTime()
        {
            var m = new CombatMetrics();
            m.RecordDamageTaken("spitter", 14f); m.RecordDamageTaken("common", 12f); m.RecordDamageTaken("spitter", 14f); m.RecordDamageTaken("", 5f); m.RecordDamageTaken("x", -3f);
            for (int i = 0; i < 100; i++) m.Tick(0.1f, i < 30);
            var s = m.Snapshot();
            Assert.AreEqual(45f, s.damageTaken, 1e-4f);
            var map = new Dictionary<string, float>(); for (int i = 0; i < s.damageSources.Length; i++) map[s.damageSources[i]] = s.damageAmounts[i];
            Assert.AreEqual(28f, map["spitter"], 1e-4f); Assert.AreEqual(12f, map["common"], 1e-4f); Assert.AreEqual(5f, map["other"], 1e-4f);
            Assert.AreEqual(3f, s.timeInCover, 1e-3f); Assert.AreEqual(0.3f, s.coverRatio, 1e-3f); Assert.AreEqual(10f, s.timePlayed, 1e-3f);
        }

        [Test]
        public void Metrics_EmptyIsSafe_AndResetClears()
        {
            var m = new CombatMetrics();
            var s = m.Snapshot();
            Assert.AreEqual(0f, s.accuracy); Assert.AreEqual(0f, s.headshotRatio); Assert.AreEqual(0f, s.averageTimeToKill); Assert.AreEqual(0f, s.coverRatio);
            m.RecordShot(); m.RecordHit(true); m.RecordKill(true, 1f); m.RecordDamageTaken("a", 1f); m.Tick(1f, true);
            m.Reset();
            var r = m.Snapshot();
            Assert.AreEqual(0, r.shotsFired); Assert.AreEqual(0, r.kills); Assert.AreEqual(0f, r.damageTaken); Assert.AreEqual(0, r.damageSources.Length);
        }

        [Test]
        public void Tuning_DefaultsAndEveryDayAreValid()
        {
            Assert.AreEqual(0, new DirectorTuning().Validate().Count);
            for (int d = 1; d <= 7; d++) { var t = DirectorTuning.ForDay(d); Assert.AreEqual(0, t.Validate().Count, "día " + d + ": " + string.Join(";", t.Validate())); }
        }

        [Test]
        public void Tuning_DifficultyGrowsWithDays()
        {
            var d1 = DirectorTuning.ForDay(1); var d7 = DirectorTuning.ForDay(7);
            Assert.Greater(d7.healthMultiplier, d1.healthMultiplier); Assert.Greater(d7.speedMultiplier, d1.speedMultiplier);
            Assert.Greater(d7.bossAggressiveness, d1.bossAggressiveness); Assert.Greater(d7.specialFrequency, d1.specialFrequency);
            Assert.Greater(d1.Proportion(ZombieIds.Common), d7.Proportion(ZombieIds.Common));
            Assert.Greater(d7.Proportion(ZombieIds.Spitter), d1.Proportion(ZombieIds.Spitter));
        }

        [Test]
        public void Tuning_SpecialFrequencyScalesOnlySpecials()
        {
            var t = new DirectorTuning();
            float spitter = t.Proportion(ZombieIds.Spitter);
            t.specialFrequency = 3f;
            Assert.Greater(t.Proportion(ZombieIds.Spitter), spitter * 2f);
            t.specialFrequency = 0f;
            Assert.AreEqual(0f, t.Proportion(ZombieIds.Spitter)); Assert.AreEqual(0f, t.Proportion(ZombieIds.Explosive));
            Assert.AreEqual(1f, t.Proportion(ZombieIds.Common) + t.Proportion(ZombieIds.Runner) + t.Proportion(ZombieIds.Tank) + t.Proportion(ZombieIds.Armored), 1e-4f);
        }

        [Test]
        public void Tuning_PickMatchesProportions()
        {
            var t = DirectorTuning.ForDay(4);
            var rng = new System.Random(3);
            var count = new Dictionary<string, int>();
            const int n = 20000;
            for (int i = 0; i < n; i++) { string id = t.Pick(rng.NextDouble()); count[id] = (count.TryGetValue(id, out int c) ? c : 0) + 1; }
            foreach (var id in t.typeIds) Assert.AreEqual(t.Proportion(id), (count.TryGetValue(id, out int c) ? c : 0) / (float)n, 0.02f, id);
            Assert.AreEqual(ZombieIds.Common, new DirectorTuning { typeWeights = new float[] { 0, 0, 0, 0, 0, 0 } }.Pick(0.5), "sin pesos cae al común");
        }

        [Test]
        public void Tuning_ValidatorRejectsBadValues()
        {
            Assert.Greater(new DirectorTuning { healthMultiplier = 10f }.Validate().Count, 0);
            Assert.Greater(new DirectorTuning { typeIds = new[] { ZombieIds.BossFinal }, typeWeights = new[] { 1f } }.Validate().Count, 0, "un jefe no entra en las oleadas normales");
            Assert.Greater(new DirectorTuning { typeIds = new[] { "common", "runner" }, typeWeights = new[] { 1f } }.Validate().Count, 0);
            Assert.Greater(new DirectorTuning { typeWeights = new float[] { 0, 0, 0, 0, 0, 0 } }.Validate().Count, 0);
        }

        [Test]
        public void BossAggressiveness_MakesTheBossAttackMoreOften()
        {
            var data = ZombieCatalog.Get(ZombieIds.BossBrute).boss;
            int Count(float aggressive)
            {
                var b = new BossBrain(data, 4) { Aggressiveness = aggressive };
                int fired = 0;
                for (float t = 0; t < 120f; t += 0.05f) foreach (var e in b.Tick(0.05f, 1f, 3f)) if (e.Type == BossEventType.AbilityFired) fired++;
                return fired;
            }
            Assert.Greater(Count(2f), Count(1f));
            Assert.Greater(Count(1f), Count(0.5f));
        }
    }
}
