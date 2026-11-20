using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class PerformanceTests
    {
        private static DayStats Day(float acc, float headRatio, float dmgPerKill, float secondsPerZombie, float coverRatio, bool died = false)
        {
            int kills = 20, fired = 100;
            return new DayStats
            {
                day = 1, zombiesTotal = kills, kills = kills, shotsFired = fired, shotsHit = (int)(fired * acc), headshotKills = (int)(kills * headRatio),
                damageTaken = dmgPerKill * kills, waveSeconds = secondsPerZombie * kills, timePlayed = 200f, coverSeconds = 200f * coverRatio, died = died,
            };
        }

        [Test]
        public void Raw_IsBetween0And100_AndOrdersPlayers()
        {
            float great = PerformanceScore.Raw(Day(0.7f, 0.5f, 0f, 3f, 0.3f));
            float average = PerformanceScore.Raw(Day(0.35f, 0.15f, 4f, 7f, 0.3f));
            float bad = PerformanceScore.Raw(Day(0.1f, 0f, 12f, 20f, 0.95f));
            Assert.AreEqual(100f, great, 1e-3f);
            Assert.That(average, Is.InRange(35f, 65f), "jugador normal cerca de 50");
            Assert.Less(bad, 25f);
            Assert.Greater(great, average); Assert.Greater(average, bad);
        }

        [Test]
        public void Raw_DeathCapsAt20_AndNoKillsIsLow()
        {
            Assert.LessOrEqual(PerformanceScore.Raw(Day(0.7f, 0.5f, 0f, 3f, 0.3f, true)), 20f);
            var empty = new DayStats { zombiesTotal = 10 };
            Assert.Less(PerformanceScore.Raw(empty), 30f);
        }

        [Test]
        public void Smooth_MovesPartWay()
        {
            Assert.AreEqual(70f, PerformanceScore.Smooth(50f, 100f), 1e-4f);
            Assert.AreEqual(50f, PerformanceScore.Smooth(50f, 50f), 1e-4f);
        }

        [Test]
        public void Adapt_DeadZone_NoChange()
        {
            var b = DirectorTuning.ForDay(3);
            var reasons = new List<string>();
            var t = DirectorAdapt.Apply(b, 53f, reasons);
            Assert.AreEqual(b.healthMultiplier, t.healthMultiplier);
            Assert.AreEqual(1, reasons.Count);
            StringAssert.Contains("sin cambios", reasons[0]);
        }

        [Test]
        public void Adapt_HighHarder_LowEasier_WithinLimits([Values(1, 4, 7)] int day)
        {
            var b = DirectorTuning.ForDay(day);
            var hard = DirectorAdapt.Apply(b, 100f);
            var easy = DirectorAdapt.Apply(b, 0f);
            Assert.Greater(hard.healthMultiplier, b.healthMultiplier);
            Assert.Less(easy.healthMultiplier, b.healthMultiplier);
            Assert.Less(hard.hordeDelayMax, b.hordeDelayMax, "más difícil: la horda se lanza antes");
            foreach (var t in new[] { hard, easy })
            {
                Assert.IsEmpty(t.Validate());
                Assert.LessOrEqual(System.Math.Abs(t.healthMultiplier / b.healthMultiplier - 1f), DirectorAdapt.MaxChange + 1e-4f);
                Assert.LessOrEqual(System.Math.Abs(t.speedMultiplier / b.speedMultiplier - 1f), DirectorAdapt.MaxChange + 1e-4f);
                Assert.LessOrEqual(System.Math.Abs(t.damageMultiplier / b.damageMultiplier - 1f), DirectorAdapt.MaxChange + 1e-4f);
            }
        }

        [Test]
        public void Adapt_ReasonExplainsInText()
        {
            var reasons = new List<string>();
            DirectorAdapt.Apply(DirectorTuning.ForDay(2), 85f, reasons);
            StringAssert.Contains("más difícil", reasons[0]);
            StringAssert.Contains("vida +", reasons[0]);
        }

        [Test]
        public void Match_TotalsAndRankingPoints()
        {
            var m = new MatchStats { mapId = "calle_en_ruinas" };
            m.days.Add(Day(0.5f, 0.2f, 3f, 6f, 0.3f));
            m.days.Add(Day(0.5f, 0.2f, 3f, 6f, 0.3f, true));
            Assert.AreEqual(1, m.DaysCompleted);
            Assert.AreEqual(40, m.Kills);
            Assert.AreEqual(0.5f, m.Accuracy, 1e-4f);
            int lost = m.RankingPoints();
            m.victory = true;
            Assert.Greater(m.RankingPoints(), lost);
            Assert.AreEqual(m.days.Count, UnityEngine.JsonUtility.FromJson<MatchStats>(UnityEngine.JsonUtility.ToJson(m)).days.Count, "se serializa con JsonUtility");
        }
    }
}
