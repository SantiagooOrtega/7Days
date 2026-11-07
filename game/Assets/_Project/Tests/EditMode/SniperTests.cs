using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class SniperTests
    {
        [Test]
        public void Sniper_HasLongRangeNoFalloffAndFourZoomLevels()
        {
            var w = WeaponCatalog.Get("sniper");
            Assert.GreaterOrEqual(w.range, 400f);
            Assert.AreEqual(1f, DamageModel.Falloff(w, 399f), 1e-5f, "sin caída de daño en todo su alcance");
            Assert.AreEqual(4, w.scopeFovs.Length);
            var nominal = new[] { 3, 5, 8, 12 };
            for (int i = 0; i < 4; i++) Assert.AreEqual(nominal[i], 60f / w.scopeFovs[i], 0.5f, "zoom x" + nominal[i]);
            for (int i = 1; i < 4; i++) Assert.Less(w.scopeFovs[i], w.scopeFovs[i - 1], "cada nivel acerca más");
        }

        [Test]
        public void Migration_UpdatesOnlyWhatChanged_AndKeepsHandEdits()
        {
            var old = WeaponCatalog.Get("sniper");
            old.dataVersion = 2; old.range = 260f; old.scopeFovs = new[] { 20f, 12f, 7f }; old.damage = 77f;   // daño editado a mano
            Assert.IsTrue(WeaponCatalog.Migrate(old));
            Assert.AreEqual(WeaponData.CurrentDataVersion, old.dataVersion);
            Assert.AreEqual(400f, old.range); Assert.AreEqual(4, old.scopeFovs.Length);
            Assert.AreEqual(77f, old.damage, "lo editado a mano se conserva");
            var pistol = WeaponCatalog.Get("pistol"); pistol.dataVersion = 2; pistol.damage = 21f;
            Assert.IsTrue(WeaponCatalog.Migrate(pistol)); Assert.AreEqual(21f, pistol.damage);
            var ancient = WeaponCatalog.Get("pistol"); ancient.dataVersion = 0;
            Assert.IsFalse(WeaponCatalog.Migrate(ancient), "demasiado viejo: se usan los valores por defecto");
        }

        [Test]
        public void BreathHold_ReducesSwayThenGaspsAndRecovers()
        {
            var c = new CameraSettingsData(); var b = new BreathHold();
            Assert.AreEqual(1f, b.SwayMultiplier(c));
            b.Tick(0.1f, true, false, c);
            Assert.AreEqual(BreathState.Ready, b.State, "solo con la mira");
            b.Tick(0.1f, true, true, c);
            Assert.AreEqual(BreathState.Holding, b.State); Assert.Less(b.SwayMultiplier(c), 0.3f);
            bool gasp = false; float t = 0f;
            while (b.State == BreathState.Holding && t < 10f) { b.Tick(0.05f, true, true, c); t += 0.05f; gasp |= b.JustExhausted; }
            Assert.AreEqual(c.breathHoldSeconds, t, 0.15f, "dura unos 3 s");
            Assert.IsTrue(gasp); Assert.AreEqual(BreathState.Exhausted, b.State); Assert.Greater(b.SwayMultiplier(c), 1f, "jadeo: más vaivén");
            b.Tick(0.1f, true, true, c);
            Assert.AreEqual(BreathState.Exhausted, b.State, "agotado no puede volver a contener");
            for (int i = 0; i < 60; i++) b.Tick(0.05f, false, true, c);
            Assert.AreEqual(BreathState.Ready, b.State);
        }

        [Test]
        public void BreathHold_ReleasingEarlyKeepsTheRestOfTheAir()
        {
            var c = new CameraSettingsData(); var b = new BreathHold();
            b.Tick(0.1f, true, true, c);
            for (int i = 0; i < 20; i++) b.Tick(0.05f, true, true, c);   // 1 s
            b.Tick(0.05f, false, true, c);
            Assert.AreEqual(BreathState.Ready, b.State);
            Assert.That(b.Air, Is.InRange(0.55f, 0.75f));
        }

        [Test]
        public void ScopeSway_IsBoundedAndMoves()
        {
            float maxAbs = 0f, first = 0f;
            for (int i = 0; i < 600; i++)
            {
                ScopeSway.Offset(i * 0.05f, 0.5f, out float p, out float y);
                maxAbs = System.Math.Max(maxAbs, System.Math.Max(System.Math.Abs(p), System.Math.Abs(y)));
                if (i == 0) first = y;
            }
            Assert.LessOrEqual(maxAbs, 0.5f + 1e-4f); Assert.Greater(maxAbs, 0.2f);
            ScopeSway.Offset(0f, 0f, out float p0, out float y0);
            Assert.AreEqual(0f, p0); Assert.AreEqual(0f, y0);
        }

        [Test]
        public void AimToggleSetting_DefaultsToHold()
        {
            Assert.IsFalse(new CameraSettingsData().aimToggle);
        }
    }
}
