using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Pantalla de muerte/victoria: medallas según las estadísticas, barras 0-100 del director, formato de los datos, tabla por día y contadores.</summary>
    public class StatsReportTests
    {
        private static DayStats D(int day, int kills = 10, int head = 2, int fired = 40, int hit = 20, float dmg = 30f, float cover = 20f, float played = 100f, int streak = 3, int grenade = 0, bool died = false)
            => new DayStats { day = day, kills = kills, headshotKills = head, shotsFired = fired, shotsHit = hit, damageTaken = dmg, coverSeconds = cover, timePlayed = played, bestStreak = streak, grenadeKills = grenade, died = died, waveSeconds = 90f, zombiesTotal = 14, score = 50f };

        [Test]
        public void Sniper_NeedsAccuracyAndHeadshots()
        {
            CollectionAssert.Contains(StatsReport.Medals(new[] { D(1, kills: 12, head: 6, fired: 30, hit: 20) }), Medal.Francotirador);
            CollectionAssert.DoesNotContain(StatsReport.Medals(new[] { D(1, kills: 12, head: 6, fired: 60, hit: 20) }), Medal.Francotirador, "poca precisión");
            CollectionAssert.DoesNotContain(StatsReport.Medals(new[] { D(1, kills: 12, head: 1, fired: 30, hit: 20) }), Medal.Francotirador, "pocos a la cabeza");
            CollectionAssert.DoesNotContain(StatsReport.Medals(new[] { D(1, kills: 4, head: 4, fired: 6, hit: 5) }), Medal.Francotirador, "muy pocas bajas");
        }

        [Test]
        public void NoCover_Streak_Grenadier_Untouchable_Survivor()
        {
            CollectionAssert.Contains(StatsReport.Medals(new[] { D(1, kills: 15, cover: 2f, played: 100f) }), Medal.SinCobertura);
            CollectionAssert.DoesNotContain(StatsReport.Medals(new[] { D(1, kills: 15, cover: 30f, played: 100f) }), Medal.SinCobertura);
            CollectionAssert.Contains(StatsReport.Medals(new[] { D(1, streak: 8) }), Medal.Racha);
            CollectionAssert.DoesNotContain(StatsReport.Medals(new[] { D(1, streak: 7) }), Medal.Racha);
            CollectionAssert.Contains(StatsReport.Medals(new[] { D(1, grenade: 3), D(2, grenade: 2) }), Medal.Granadero, "se suman los días");
            CollectionAssert.Contains(StatsReport.Medals(new[] { D(1, kills: 20, dmg: 10f) }), Medal.Intocable);
            var week = new List<DayStats>(); for (int d = 1; d <= 6; d++) week.Add(D(d, died: d == 6));
            CollectionAssert.Contains(StatsReport.Medals(week), Medal.Superviviente);
            Assert.AreEqual(9, StatsReport.BestStreak(new[] { D(1, streak: 4), D(2, streak: 9) }));
            foreach (Medal m in System.Enum.GetValues(typeof(Medal))) { Assert.IsNotEmpty(StatsReport.MedalName(m)); Assert.IsNotEmpty(StatsReport.MedalHow(m)); }
        }

        [Test]
        public void Bars_Are0To100_WithExplanations_MatchingTheDirectorScore()
        {
            var d = D(1);
            var bars = StatsReport.Bars(d);
            Assert.AreEqual(5, bars.Length);
            float sum = 0f;
            for (int i = 0; i < bars.Length; i++) { Assert.That(bars[i].Value, Is.InRange(0f, 100f)); Assert.IsNotEmpty(bars[i].Explain); sum += bars[i].Value / 100f * PerformanceScore.PartMax[i]; }
            Assert.AreEqual(PerformanceScore.Raw(d), sum, 0.01f, "las barras suman la puntuación del director");
        }

        [Test]
        public void Format_Time_Percent_Damage_Weapons()
        {
            Assert.AreEqual("0:00", StatsReport.Time(0f)); Assert.AreEqual("1:05", StatsReport.Time(65f)); Assert.AreEqual("12:00", StatsReport.Time(720f));
            Assert.AreEqual("50 %", StatsReport.Percent(0.5f)); Assert.AreEqual("100 %", StatsReport.Percent(1.4f)); Assert.AreEqual("0 %", StatsReport.Percent(-1f));
            Assert.AreEqual("31", StatsReport.Damage(30.6f));
            var a = D(1); a.weapons = new[] { "Pistola", "Escopeta" }; var b = D(2); b.weapons = new[] { "Pistola" };
            Assert.AreEqual("Pistola, Escopeta", StatsReport.Weapons(new[] { a, b }));
            Assert.AreEqual("—", StatsReport.Weapons(new[] { D(1) }));
        }

        [Test]
        public void DayTable_SortedByDay_WithHeader_AndDeathMark()
        {
            var rows = StatsReport.DayTable(new[] { D(3, died: true), D(1), D(2) });
            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual("Día", rows[0][0]);
            Assert.AreEqual("1", rows[1][0]); Assert.AreEqual("2", rows[2][0]); Assert.AreEqual("3 †", rows[3][0]);
            foreach (var r in rows) Assert.AreEqual(7, r.Length);
            Assert.IsNotEmpty(StatsReport.TableText(rows));
        }

        [Test]
        public void CountUp_RisesFrom0ToTarget()
        {
            Assert.AreEqual(0, StatsReport.CountUp(120, 0f));
            Assert.AreEqual(120, StatsReport.CountUp(120, 1f));
            Assert.Greater(StatsReport.CountUp(120, 0.5f), 60, "ease-out: rápido al principio");
            int prev = -1; for (float t = 0; t <= 1f; t += 0.05f) { int v = StatsReport.CountUp(120, t); Assert.GreaterOrEqual(v, prev); prev = v; }
        }

        [Test]
        public void StatList_Values_AndChartNeverEmpty()
        {
            var d = D(1, kills: 0, fired: 0, hit: 0, streak: 0); d.weapons = new[] { "Pistola" };
            var vals = StatsReport.StatValues(new[] { d }, out int k, out int h, out int acc, out int dmg);
            Assert.AreEqual(StatsReport.StatNames.Length, vals.Length);
            Assert.AreEqual("0", vals[0]); Assert.AreEqual("0 %", vals[2]); Assert.AreEqual("Pistola", vals[5]); Assert.AreEqual("1:30", vals[4]);
            foreach (var days in new[] { new[] { d }, new[] { D(1), D(2, kills: 30) } })
            {
                var px = StatsReport.Chart(days, 100, 40); int solid = 0;
                for (int y = 1; y < 40; y++) for (int x = 0; x < 100; x++) if (px[(y * 100 + x) * 4 + 3] > 0) solid++;
                Assert.Greater(solid, 20, "nunca vacía (con un día y sin bajas: barra mínima y punto)");
            }
        }
    }
}
