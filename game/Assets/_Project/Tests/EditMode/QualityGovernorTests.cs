using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class QualityGovernorTests
    {
        private static void Run(QualityGovernor g, float seconds, float fps, float dt = 0.1f)
        {
            for (float t = 0; t < seconds; t += dt) g.Update(dt, fps);
        }

        [Test]
        public void KeepsFullQuality_WhenFpsIsAtOrAboveTarget()
        {
            var g = new QualityGovernor();
            Run(g, 60f, 60f);
            Assert.AreEqual(0, g.Level);
        }

        [Test]
        public void IgnoresLowFps_DuringWarmup()
        {
            var g = new QualityGovernor { WarmupSeconds = 6f };
            Run(g, 5.5f, 20f);
            Assert.AreEqual(0, g.Level);
        }

        [Test]
        public void DropsOneLevel_AfterSustainedLowFps()
        {
            var g = new QualityGovernor { WarmupSeconds = 1f, SustainSeconds = 2.5f };
            Run(g, 1f + 2.0f, 40f);
            Assert.AreEqual(0, g.Level, "todavía no se cumplió el tiempo sostenido");
            Run(g, 1.0f, 40f);
            Assert.AreEqual(1, g.Level);
        }

        [Test]
        public void ShortDips_DoNotTriggerADrop()
        {
            var g = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 2.5f };
            for (int i = 0; i < 10; i++)
            {
                Run(g, 1.0f, 30f);
                Run(g, 0.5f, 60f); // se recupera antes de acumular 2.5 s seguidos
            }
            Assert.AreEqual(0, g.Level);
        }

        [Test]
        public void Cooldown_SpacesOutDrops_AndLevelIsCapped()
        {
            var g = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 1f, CooldownSeconds = 5f, MaxLevel = 3 };
            Run(g, 1.2f, 20f);
            Assert.AreEqual(1, g.Level);
            Run(g, 4.0f, 20f);
            Assert.AreEqual(1, g.Level, "durante el cooldown no baja otra vez");
            Run(g, 60f, 20f);
            Assert.AreEqual(3, g.Level, "tope en MaxLevel");
        }

        [Test]
        public void SetLevel_ClampsAndDensityFactorsDecrease()
        {
            var g = new QualityGovernor();
            g.SetLevel(99);
            Assert.AreEqual(3, g.Level);
            g.SetLevel(-4);
            Assert.AreEqual(0, g.Level);
            Assert.AreEqual(1f, QualityGovernor.DensityFactor(0));
            Assert.Greater(QualityGovernor.DensityFactor(0), QualityGovernor.DensityFactor(1));
            Assert.Greater(QualityGovernor.DensityFactor(1), QualityGovernor.DensityFactor(2));
            Assert.AreEqual(0f, QualityGovernor.DensityFactor(3));
        }
    }
}
