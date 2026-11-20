using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class DayCycleTests
    {
        [Test]
        public void Bosses_OnDays3_5_7()
        {
            for (int d = 1; d <= 7; d++)
            {
                var p = DayPlans.For(d);
                bool boss = d == 3 || d == 5 || d == 7;
                Assert.AreEqual(boss, p.bossId != null, "día " + d);
            }
            Assert.AreEqual(ZombieIds.BossBrute, DayPlans.For(3).bossId);
            Assert.AreEqual(ZombieIds.BossLauncher, DayPlans.For(5).bossId);
            Assert.AreEqual(ZombieIds.BossFinal, DayPlans.For(7).bossId);
        }

        [Test]
        public void Plans_GrowEachDay_AndRespectCap()
        {
            for (int d = 2; d <= 7; d++)
            {
                Assert.Greater(DayPlans.For(d).totalZombies, DayPlans.For(d - 1).totalZombies);
                Assert.GreaterOrEqual(DayPlans.For(d).maxAlive, DayPlans.For(d - 1).maxAlive);
            }
            Assert.LessOrEqual(DayPlans.For(7, 12).maxAlive, 12);
        }

        [Test]
        public void Unlocks_Day1OnlyCommon_Day7All()
        {
            CollectionAssert.AreEquivalent(new[] { ZombieIds.Common }, DayPlans.Unlocked(1));
            Assert.AreEqual(6, DayPlans.Unlocked(7).Length);
            CollectionAssert.Contains(DayPlans.NewToday(2), ZombieIds.Runner);
        }

        [Test]
        public void ApplyUnlocks_LockedTypesNeverPicked()
        {
            var t = DayPlans.ApplyUnlocks(DirectorTuning.ForDay(1), 1);
            Assert.IsEmpty(t.Validate());
            for (int i = 0; i < 100; i++) Assert.AreEqual(ZombieIds.Common, t.Pick(i / 100.0));
            var t3 = DayPlans.ApplyUnlocks(DirectorTuning.ForDay(3), 3);
            Assert.AreEqual(0f, t3.Proportion(ZombieIds.Tank));
            Assert.Greater(t3.Proportion(ZombieIds.Spitter), 0f);
        }

        [Test]
        public void Cycle_FullRun_ToVictory()
        {
            var c = new DayCycle();
            Assert.AreEqual(DayPhase.Preparation, c.Phase);
            for (int d = 1; d <= 7; d++)
            {
                Assert.AreEqual(d, c.Day);
                c.Tick(c.Plan.prepSeconds + 0.1f);       // la preparación termina sola
                Assert.AreEqual(DayPhase.Wave, c.Phase);
                c.WaveCleared();
                if (d < 7) { Assert.AreEqual(DayPhase.Summary, c.Phase); c.NextDay(); }
            }
            Assert.AreEqual(DayPhase.Victory, c.Phase);
            Assert.IsTrue(c.IsOver);
        }

        [Test]
        public void Cycle_DeathIsDefeat_AndFinal()
        {
            var c = new DayCycle();
            c.StartWave();
            c.PlayerDied();
            Assert.AreEqual(DayPhase.Defeat, c.Phase);
            c.WaveCleared(); c.NextDay();
            Assert.AreEqual(DayPhase.Defeat, c.Phase);
        }

        [Test]
        public void Wave_SpawnsUpToTotal_BossAtHalf_ClearsWhenAllDead()
        {
            var w = new WaveState(DayPlans.For(3));
            int alive = 0;
            int guard = 0;
            while (!w.IsCleared(alive) && guard++ < 10000)
            {
                if (w.WantsSpawn(1f, alive)) { w.OnSpawned(); alive++; }
                if (w.WantsBoss) w.OnBossSpawned();
                if (alive > 0) { alive--; w.OnKilled(); }       // el jugador mata uno por tick
                if (w.BossSpawned && w.AllSpawned && alive == 0) w.OnBossDead();
                Assert.LessOrEqual(alive, w.Plan.maxAlive);
            }
            Assert.IsTrue(w.IsCleared(0));
            Assert.AreEqual(w.Plan.totalZombies, w.Spawned);
            Assert.IsTrue(w.BossSpawned);
        }

        [Test]
        public void Wave_RemainingCountsPendingAndBoss()
        {
            var w = new WaveState(DayPlans.For(3));
            Assert.AreEqual(w.Plan.totalZombies + 1, w.Remaining(0));
            w.OnSpawned();
            Assert.AreEqual(w.Plan.totalZombies + 1, w.Remaining(1));
            Assert.IsFalse(w.IsCleared(0));
        }
    }
}
