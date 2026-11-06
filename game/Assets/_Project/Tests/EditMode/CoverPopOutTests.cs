using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class CoverPopOutTests
    {
        private static PopDirection C(float top, int shoulder = 1, bool over = true, bool eL = false, bool eR = false, bool lC = true, bool rC = true)
            => PopOutRules.Choose(top, 1.45f, shoulder, over, eL, eR, lC, rC);

        [Test]
        public void LowCover_PopsOver_EvenWithEdgesAvailable()
        {
            Assert.AreEqual(PopDirection.Over, C(1.0f));
            Assert.AreEqual(PopDirection.Over, C(1.0f, eL: true, eR: true));
            Assert.AreEqual(PopDirection.Over, C(1.45f), "justo en el límite sigue siendo baja");
        }

        [Test]
        public void LowCover_WithNoRoomAbove_UsesTheShoulderSideThenTheOther()
        {
            Assert.AreEqual(PopDirection.Right, C(1.0f, 1, over: false, eL: true, eR: true));
            Assert.AreEqual(PopDirection.Left, C(1.0f, -1, over: false, eL: true, eR: true));
            Assert.AreEqual(PopDirection.Left, C(1.0f, 1, over: false, eL: true, eR: false), "el hombro derecho no tiene borde: sale por el izquierdo");
        }

        [Test]
        public void TallCover_PopsToTheSideOfTheActiveShoulder_OnlyAtAnEdge()
        {
            Assert.AreEqual(PopDirection.Right, C(2.2f, 1, eL: true, eR: true));
            Assert.AreEqual(PopDirection.Left, C(2.2f, -1, eL: true, eR: true));
            Assert.AreEqual(PopDirection.Left, C(2.2f, 1, eL: true, eR: false), "sin espacio en su lado prueba el otro");
            Assert.AreEqual(PopDirection.Right, C(2.2f, 1, eL: true, eR: true, lC: true, rC: true));
            Assert.AreEqual(PopDirection.Left, C(2.2f, 1, eL: true, eR: true, rC: false), "el lado derecho está tapado por otro muro");
        }

        [Test]
        public void TallCoverMidWall_HasNoPopOut_OnlyBlindFire()
        {
            Assert.AreEqual(PopDirection.None, C(2.2f));
            Assert.AreEqual(PopDirection.None, C(2.2f, eL: true, eR: true, lC: false, rC: false));
            Assert.AreEqual(PopDirection.Over, C(1.7f), "una de 1.7 m aún se ve por encima de pie");
        }

        [Test]
        public void Exposure_RisesAndFallsWithinPopSeconds_AndTracksTime()
        {
            var e = new ExposureState(); const float pop = 0.2f;
            for (int i = 0; i < 6; i++) e.Tick(0.016f, true, false, true, 0.35f, pop);
            Assert.AreEqual(0f, e.Value); Assert.AreEqual(CoverState.Covered, e.State);
            e.Tick(0.05f, true, true, true, 0.35f, pop);
            Assert.That(e.Value, Is.InRange(0.01f, 0.5f), "sube con ease, no de golpe");
            for (int i = 0; i < 20; i++) e.Tick(0.016f, true, true, true, 0.35f, pop);
            Assert.AreEqual(1f, e.Value, 1e-5f); Assert.AreEqual(CoverState.PoppedOut, e.State); Assert.IsTrue(e.CanShootAimed);
            for (int i = 0; i < 15; i++) e.Tick(0.016f, true, false, true, 0.35f, pop);
            Assert.AreEqual(0f, e.Value, 1e-5f); Assert.AreEqual(CoverState.Covered, e.State);
            Assert.Greater(e.CoveredSeconds, 0f); Assert.Greater(e.PoppedSeconds, 0f);
        }

        [Test]
        public void Exposure_CannotPopWithoutRoom_AndResetsOutOfCover()
        {
            var e = new ExposureState();
            for (int i = 0; i < 30; i++) e.Tick(0.016f, true, true, false, 0.35f, 0.2f);
            Assert.AreEqual(0f, e.Value, "sin espacio no se asoma");
            for (int i = 0; i < 30; i++) e.Tick(0.016f, true, true, true, 0.35f, 0.2f);
            Assert.AreEqual(1f, e.Value, 1e-5f);
            e.Tick(0.016f, false, true, true, 0.35f, 0.2f);
            Assert.AreEqual(0f, e.Value); Assert.AreEqual(CoverState.Out, e.State);
        }

        [Test]
        public void BlindFire_GivesPartialExposureThatFadesAndCountsAsBlind()
        {
            var e = new ExposureState();
            e.Tick(0.016f, true, false, true, 0.35f, 0.2f);
            e.OnBlindShot(0.45f);
            for (int i = 0; i < 20; i++) e.Tick(0.016f, true, false, true, 0.35f, 0.2f);
            Assert.AreEqual(CoverState.BlindFiring, e.State);
            Assert.That(e.Raw, Is.InRange(0.3f, 0.36f), "exposición parcial");
            Assert.IsFalse(e.CanShootAimed);
            for (int i = 0; i < 80; i++) e.Tick(0.016f, true, false, true, 0.35f, 0.2f);
            Assert.AreEqual(0f, e.Raw, 1e-5f); Assert.AreEqual(CoverState.Covered, e.State);
            Assert.Greater(e.BlindSeconds, 0.3f);
        }

        [Test]
        public void Protection_ScalesWithExposure()
        {
            Assert.AreEqual(0.35f, CoverProtection.DamageMultiplier(0f, 0.35f), 1e-5f);
            Assert.AreEqual(1f, CoverProtection.DamageMultiplier(1f, 0.35f), 1e-5f);
            Assert.Less(CoverProtection.DamageMultiplier(0.35f, 0.35f), CoverProtection.DamageMultiplier(1f, 0.35f));
            Assert.IsTrue(CoverProtection.AbsorbsProjectile(0f)); Assert.IsTrue(CoverProtection.AbsorbsProjectile(0.35f)); Assert.IsFalse(CoverProtection.AbsorbsProjectile(1f));
            Assert.AreEqual(6f, CoverProtection.VisibleRange(0f, 18f)); Assert.AreEqual(18f, CoverProtection.VisibleRange(1f, 18f));
            Assert.AreEqual(4f, CoverProtection.VisibleRange(0f, 4f), "nunca más que su rango de detección");
        }

        [Test]
        public void BlindFire_HasHugeSpreadAndReducedDamage_Configurable()
        {
            var c = new CameraSettingsData();
            foreach (var w in WeaponCatalog.Defaults())
            {
                if (w.IsMelee) continue;
                float s = BlindFire.Spread(w, c);
                Assert.GreaterOrEqual(s, c.blindMinSpread, w.id); Assert.LessOrEqual(s, c.blindMaxSpread, w.id);
                Assert.Greater(s, w.spreadHip * 1.5f - 0.001f, w.id + ": mucho más que de cadera (salvo el tope)");
            }
            Assert.AreEqual(18f, BlindFire.Damage(30f, c), 1e-4f, "x0.6 por defecto");
            c.blindDamageMultiplier = 0.5f; Assert.AreEqual(15f, BlindFire.Damage(30f, c), 1e-4f);
            var bad = new CameraSettingsData { popSeconds = 5f, blindDamageMultiplier = 9f }; bad.Validate();
            Assert.LessOrEqual(bad.popSeconds, 0.25f); Assert.LessOrEqual(bad.blindDamageMultiplier, 1f);
        }

        [Test]
        public void ShotOrigin_IsPushedOutOfTheCoverBox()
        {
            // caja de 2 m de largo (x), 1 m de alto, 0.5 m de grosor (z)
            float ox, oy, oz;
            bool moved = ShotOrigin.PushOut(0.2f, 0.9f, 0.1f, -1f, 0f, -0.25f, 1f, 1f, 0.25f, 0.05f, PopDirection.Over, out ox, out oy, out oz);
            Assert.IsTrue(moved); Assert.AreEqual(1.05f, oy, 1e-5f, "por encima si se asoma por encima"); Assert.AreEqual(0.2f, ox);
            moved = ShotOrigin.PushOut(0.2f, 0.5f, 0.1f, -1f, 0f, -0.25f, 1f, 1f, 0.25f, 0.05f, PopDirection.None, out ox, out oy, out oz);
            Assert.IsTrue(moved); Assert.AreEqual(0.5f, oy, 1e-5f, "la salida más corta sin preferencia: por el grosor (z)"); Assert.AreEqual(0.3f, oz, 1e-5f);
            moved = ShotOrigin.PushOut(0.9f, 0.9f, 0f, -1f, 0f, -0.25f, 1f, 1f, 0.25f, 0.05f, PopDirection.Right, out ox, out oy, out oz);
            Assert.IsTrue(moved); Assert.AreEqual(1.05f, ox, 1e-5f, "por el lado derecho");
            moved = ShotOrigin.PushOut(3f, 0.9f, 0f, -1f, 0f, -0.25f, 1f, 1f, 0.25f, 0.05f, PopDirection.Over, out ox, out oy, out oz);
            Assert.IsFalse(moved); Assert.AreEqual(3f, ox);
        }

        [Test]
        public void ShotOrigin_AfterPushOut_IsOutsideTheBox()
        {
            var rng = new System.Random(4);
            foreach (var dir in new[] { PopDirection.Over, PopDirection.Left, PopDirection.Right, PopDirection.None })
                for (int i = 0; i < 200; i++)
                {
                    float px = (float)rng.NextDouble() * 2f - 1f, py = (float)rng.NextDouble(), pz = (float)rng.NextDouble() * 0.5f - 0.25f;
                    ShotOrigin.PushOut(px, py, pz, -1f, 0f, -0.25f, 1f, 1f, 0.25f, 0.05f, dir, out float ox, out float oy, out float oz);
                    bool inside = ox > -1f && ox < 1f && oy > 0f && oy < 1f && oz > -0.25f && oz < 0.25f;
                    Assert.IsFalse(inside, dir + " " + px + "," + py + "," + pz);
                }
        }

        [Test]
        public void Telemetry_AndCoverAbuseRule()
        {
            var m = new CombatMetrics();
            m.RecordCover(30f, 1f, 0f);
            var s = m.Snapshot();
            Assert.AreEqual(30f, s.coverSeconds); Assert.AreEqual(1f, s.poppedOutSeconds); Assert.IsTrue(s.coverAbuse, "30 s cubierto y 1 s asomado = abuso");
            m.RecordCover(30f, 20f, 8f);
            Assert.IsFalse(m.Snapshot().coverAbuse);
            m.RecordCover(10f, 0f, 0f);
            Assert.IsFalse(m.Snapshot().coverAbuse, "menos de 20 s: no hay datos suficientes");
            Assert.Greater(CoverAbuse.ActiveRatio(10f, 10f, 0f), CoverAbuse.ActiveRatio(10f, 1f, 0f));
            m.Reset(); Assert.AreEqual(0f, m.Snapshot().coverSeconds);
        }
    }
}
