using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class CombatStatsTests
    {
        private static CombatStatsRecord Day(int kills = 10, float cover = 0, float popped = 0, float blind = 0, float sprint = 0, float fatigue = 0, float ttk = 2f)
            => new CombatStatsRecord { kills = kills, shotsFired = 40, shotsHit = 20, headshots = 4, headshotKills = 3, averageTimeToKill = ttk, damageTaken = 30f, coverSeconds = cover, poppedOutSeconds = popped, blindFireSeconds = blind, sprintSeconds = sprint, maxFatigue = fatigue, daysPlayed = 1 };

        [Test]
        public void FromSnapshot_CarriesCoverAndFatigueData()
        {
            var m = new CombatMetrics();
            m.RecordCover(50f, 4f, 2f); m.RecordMovement(40f, 0.9f); m.RecordShot(); m.RecordHit(true); m.RecordKill(true, 1.5f); m.RecordDeath();
            var r = CombatStatsRecord.FromSnapshot(m.Snapshot(), 1);
            Assert.AreEqual(50f, r.coverSeconds); Assert.AreEqual(4f, r.poppedOutSeconds); Assert.AreEqual(2f, r.blindFireSeconds);
            Assert.AreEqual(40f, r.sprintSeconds); Assert.AreEqual(0.9f, r.maxFatigue, 1e-5f); Assert.AreEqual(1, r.headshotKills); Assert.AreEqual(1, r.deaths);
            Assert.IsEmpty(r.Validate());
        }

        [Test]
        public void Merge_AccumulatesTimesCountersAndKeepsMaxFatigue_WithoutTouchingInputs()
        {
            var stored = Day(kills: 10, cover: 30, popped: 10, blind: 2, sprint: 20, fatigue: 0.6f, ttk: 2f);
            var today = Day(kills: 30, cover: 10, popped: 20, blind: 5, sprint: 15, fatigue: 0.4f, ttk: 4f);
            var t = CombatStatsRecord.Merge(stored, today);
            Assert.AreEqual(40, t.kills); Assert.AreEqual(40f, t.coverSeconds); Assert.AreEqual(30f, t.poppedOutSeconds); Assert.AreEqual(7f, t.blindFireSeconds);
            Assert.AreEqual(35f, t.sprintSeconds); Assert.AreEqual(0.6f, t.maxFatigue, 1e-5f, "la fatiga guarda el máximo");
            Assert.AreEqual(3.5f, t.averageTimeToKill, 1e-4f, "media ponderada por muertes"); Assert.AreEqual(2, t.daysPlayed);
            Assert.AreEqual(10, stored.kills); Assert.AreEqual(30, today.kills);
            Assert.AreEqual(0, CombatStatsRecord.Merge(null, null).kills);
        }

        [Test]
        public void MetricsTotals_AddStoredStatsToToday()
        {
            var m = new CombatMetrics { Stored = Day(cover: 100f, popped: 5f) };
            m.RecordCover(20f, 2f, 0f);
            var t = m.Totals();
            Assert.AreEqual(120f, t.coverSeconds); Assert.AreEqual(7f, t.poppedOutSeconds);
        }

        [Test]
        public void CoverAbuse_UsesTheStoredStats()
        {
            // hoy solo hay 8 s en cobertura (sin datos suficientes), pero lo guardado dice que llevas 120 s escondido y 6 asomado
            var m = new CombatMetrics { Stored = Day(cover: 120f, popped: 6f, blind: 0f) };
            m.RecordCover(8f, 0f, 0f);
            Assert.IsTrue(m.Snapshot().coverAbuse, "con lo guardado hay abuso aunque hoy no haya datos");
            var clean = new CombatMetrics { Stored = Day(cover: 40f, popped: 40f, blind: 10f) };
            clean.RecordCover(10f, 10f, 0f);
            Assert.IsFalse(clean.Snapshot().coverAbuse);
            var none = new CombatMetrics(); none.RecordCover(8f, 0f, 0f);
            Assert.IsFalse(none.Snapshot().coverAbuse, "menos de 20 s en total: sin datos suficientes");
            Assert.IsTrue(CoverAbuse.IsAbusing(Day(cover: 60f, popped: 2f)));
            Assert.IsFalse(CoverAbuse.IsAbusing((CombatStatsRecord)null));
        }

        [Test]
        public void Director_RaisesSpecialsAndPressureOnAbuse_AndRunnersOnTiredRunning()
        {
            var baseT = DirectorTuning.ForDay(4); var reasons = new List<string>();
            var abusing = DirectorRules.Adjust(baseT, Day(cover: 90f, popped: 3f), reasons);
            Assert.AreEqual(1f, abusing.coverPressure); Assert.Greater(abusing.specialFrequency, baseT.specialFrequency);
            Assert.AreEqual(1, reasons.Count); StringAssert.Contains("abuso de cobertura", reasons[0]);
            Assert.AreEqual(0f, baseT.coverPressure, "no modifica el tuning de entrada");

            var tired = DirectorRules.Adjust(baseT, Day(sprint: 60f, fatigue: 0.95f));
            Assert.Greater(tired.Proportion(ZombieIds.Runner), baseT.Proportion(ZombieIds.Runner));
            Assert.AreEqual(baseT.specialFrequency, tired.specialFrequency);

            var calm = DirectorRules.Adjust(baseT, Day(cover: 30f, popped: 30f, sprint: 10f, fatigue: 0.3f));
            Assert.AreEqual(baseT.specialFrequency, calm.specialFrequency); Assert.AreEqual(0f, calm.coverPressure);
            Assert.IsEmpty(calm.Validate()); Assert.IsEmpty(abusing.Validate()); Assert.IsEmpty(tired.Validate());
            Assert.LessOrEqual(DirectorRules.Adjust(new DirectorTuning { specialFrequency = 3.9f }, Day(cover: 90f, popped: 1f)).specialFrequency, 4f);
        }

        [Test]
        public void TunedTuning_RejectsBadPressure_AndValidatesRecords()
        {
            Assert.Greater(new DirectorTuning { coverPressure = 2f }.Validate().Count, 0);
            Assert.Greater(new CombatStatsRecord { maxFatigue = 1.5f }.Validate().Count, 0);
            Assert.Greater(new CombatStatsRecord { shotsFired = 1, shotsHit = 5 }.Validate().Count, 0);
        }
    }
}
