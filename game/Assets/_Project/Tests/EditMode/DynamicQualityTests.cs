using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class DynamicQualityTests
    {
        private static void Run(QualityGovernor g, float seconds, float fps, bool canDegrade = true, bool headroom = true)
        {
            for (float t = 0; t < seconds; t += 0.1f) g.Update(0.1f, fps, canDegrade, headroom);
        }

        [Test]
        public void Governor_RecoversWhenFpsComesBack_AfterHoldingSeconds()
        {
            var g = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 1f, CooldownSeconds = 2f, RecoverSeconds = 4f };
            Run(g, 1.2f, 30f);
            Assert.AreEqual(1, g.Level);
            Run(g, 2.1f, 60f);   // enfriamiento
            Run(g, 3f, 60f);
            Assert.AreEqual(1, g.Level, "aún no lleva 4 s holgado");
            Run(g, 1.5f, 60f);
            Assert.AreEqual(0, g.Level, "vuelve a subir la calidad");
        }

        [Test]
        public void Governor_DoesNotOscillate_BetweenThresholds()
        {
            var g = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 1f, CooldownSeconds = 2f, RecoverSeconds = 4f };
            Run(g, 1.2f, 30f);
            int level = g.Level;
            Run(g, 60f, 56.5f);   // entre 55 (bajar) y 58 (subir): no se mueve
            Assert.AreEqual(level, g.Level);
        }

        [Test]
        public void Governor_LowersResolutionFirst_ThenQuality()
        {
            var g = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 1f };
            Run(g, 10f, 30f, canDegrade: false);
            Assert.AreEqual(0, g.Level, "mientras la resolución dinámica puede bajar, la calidad no baja");
            Run(g, 1.2f, 30f, canDegrade: true);
            Assert.AreEqual(1, g.Level);
            var h = new QualityGovernor { WarmupSeconds = 0f, SustainSeconds = 1f, CooldownSeconds = 0.5f };
            Run(h, 1.2f, 30f);
            Run(h, 30f, 60f, headroom: false);
            Assert.AreEqual(1, h.Level, "solo recupera calidad con la resolución ya al máximo");
        }

        [Test]
        public void DynamicResolution_StaysWithinLimits()
        {
            var d = new DynamicResolution();
            for (int i = 0; i < 600; i++) d.Update(0.1f, 40f, 40f);
            Assert.AreEqual(d.Min, d.Scale, 1e-4f);
            Assert.GreaterOrEqual(d.Scale, 0.6f);
            for (int i = 0; i < 2000; i++) d.Update(0.1f, 5f, 16.7f);
            Assert.AreEqual(1f, d.Scale, 1e-4f, "con GPU de sobra vuelve a 1.0, nunca más");
        }

        [Test]
        public void DynamicResolution_DropsFast_RisesSlow_AndDoesNotFlicker()
        {
            var d = new DynamicResolution();
            d.Update(0.4f, 20f, 16.7f);
            Assert.Less(d.Scale, 1f, "baja en menos de medio segundo");
            float s = d.Scale;
            for (int i = 0; i < 20; i++) d.Update(0.1f, 9f, 16.7f);
            Assert.AreEqual(s, d.Scale, 1e-4f, "no sube antes de 3 s holgado");
            for (int i = 0; i < 100; i++) d.Update(0.1f, 13f, 16.7f);   // entre 11.5 y 15 ms: zona muerta
            Assert.AreEqual(s, d.Scale, 1e-4f, "sin parpadeo en la zona de histéresis");
        }

        [Test]
        public void DynamicResolution_WithoutGpuTiming_IgnoresTheFrameCap()
        {
            var d = new DynamicResolution();
            for (int i = 0; i < 100; i++) d.Update(0.1f, 0f, 16.7f);   // 60 FPS con el límite: no es falta de GPU
            Assert.AreEqual(1f, d.Scale, 1e-4f);
            for (int i = 0; i < 10; i++) d.Update(0.1f, 0f, 25f);      // 40 FPS: sí
            Assert.Less(d.Scale, 1f);
        }

        [Test]
        public void Profiles_GetCheaperLevelByLevel()
        {
            for (int l = 1; l <= 3; l++)
            {
                QualityProfile a = QualityProfile.For(l - 1), b = QualityProfile.For(l);
                Assert.LessOrEqual(b.ShadowDistance, a.ShadowDistance);
                Assert.LessOrEqual(b.Cascades, a.Cascades);
                Assert.LessOrEqual((int)b.Softness, (int)a.Softness);
                Assert.LessOrEqual(b.MinScale, a.MinScale);
                Assert.LessOrEqual(b.MaxScale, a.MaxScale);
                Assert.GreaterOrEqual(b.Atmosphere, a.Atmosphere);
                Assert.That(b.MinScale, Is.InRange(0.6f, 1f)); Assert.That(b.MaxScale, Is.InRange(b.MinScale, 1f));
            }
            var hi = QualityProfile.For(0);
            Assert.That(hi.ShadowDistance, Is.InRange(35f, 50f), "sombras 35-50 m");
            Assert.LessOrEqual(hi.Cascades, 2, "2 cascadas como máximo");
            Assert.AreEqual(ShadowSoftness.Hard, QualityProfile.For(2).Softness, "sin sombras suaves en calidad baja");
        }
    }
}
