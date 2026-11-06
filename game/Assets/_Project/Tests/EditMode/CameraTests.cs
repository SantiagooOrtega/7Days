using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class CameraTests
    {
        [Test]
        public void Pitch_IsClampedToLimits()
        {
            var c = new CameraSettingsData();
            Assert.AreEqual(c.maxPitch, CameraMath.ClampPitch(120f, c.minPitch, c.maxPitch));
            Assert.AreEqual(c.minPitch, CameraMath.ClampPitch(-120f, c.minPitch, c.maxPitch));
            Assert.AreEqual(10f, CameraMath.ClampPitch(10f, c.minPitch, c.maxPitch));
            var bad = new CameraSettingsData { minPitch = -200f, maxPitch = 200f }; bad.Validate();
            Assert.GreaterOrEqual(bad.minPitch, -89f); Assert.LessOrEqual(bad.maxPitch, 89f);
        }

        [Test]
        public void Shoulder_SwapsSmoothlyAndArrives()
        {
            var s = new ShoulderState();
            Assert.AreEqual(1, s.Side); Assert.AreEqual(1f, s.CinemachineSide);
            s.Toggle();
            Assert.AreEqual(-1, s.Side);
            s.Tick(0.05f, 0.25f);
            Assert.Less(s.Value, 1f); Assert.Greater(s.Value, -1f, "no salta de golpe");
            float prev = s.Value;
            for (int i = 0; i < 10; i++) { s.Tick(0.025f, 0.25f); Assert.LessOrEqual(s.Value, prev + 1e-6f, "avanza en una sola dirección"); prev = s.Value; }
            s.Tick(0.1f, 0.25f);
            Assert.AreEqual(-1f, s.Value, 1e-5f); Assert.AreEqual(0f, s.CinemachineSide, 1e-5f);
            s.Toggle(); s.Tick(0.02f, 0.25f); s.Toggle();   // cambio a mitad de camino: vuelve desde donde esté, sin saltos
            Assert.AreEqual(-1, s.Side); Assert.Greater(s.Value, -1.0001f);
        }

        [Test]
        public void AimTransition_EasesFovAndSensitivity()
        {
            var a = new AimTransition();
            Assert.AreEqual(60f, a.Fov(60f, 40f));
            a.Tick(0.05f, true, 0.2f, 0.2f);
            float early = a.Fov(60f, 40f);
            Assert.Less(early, 60f); Assert.Greater(early, 55f, "ease-in: empieza despacio");
            a.Tick(0.05f, true, 0.2f, 0.2f);
            Assert.AreEqual(50f, a.Fov(60f, 40f), 0.01f, "a mitad de camino");
            a.Tick(0.2f, true, 0.2f, 0.2f);
            Assert.AreEqual(40f, a.Fov(60f, 40f), 1e-4f);
            Assert.AreEqual(0.06f, a.Sensitivity(0.1f, 0.6f), 1e-5f, "menos sensibilidad apuntando");
            a.Tick(0.1f, false, 0.2f, 0.4f);
            Assert.Greater(a.Raw, 0.5f, "la salida tiene su propio tiempo");
        }

        [Test]
        public void SpreadToPixels_GrowsWithSpreadAndShrinksWithFov()
        {
            float a = CameraMath.SpreadToPixels(2f, 60f, 1080f), b = CameraMath.SpreadToPixels(4f, 60f, 1080f), c = CameraMath.SpreadToPixels(2f, 40f, 1080f);
            Assert.Greater(b, a); Assert.Greater(c, a, "al cerrar el FOV el mismo ángulo ocupa más pantalla");
        }

        [Test]
        public void StepCycle_AlternatesFeetAtTheRightCadence()
        {
            var s = new StepCycle(); int steps = 0, lastFoot = s.Foot;
            for (int i = 0; i < 100; i++) if (s.Tick(0.01f, 6f, 1.2f)) { steps++; Assert.AreNotEqual(lastFoot, s.Foot); lastFoot = s.Foot; }
            Assert.AreEqual(5, steps, 1, "6 m/s con zancada de 1.2 m = 5 pasos por segundo");
            var still = new StepCycle(); for (int i = 0; i < 100; i++) Assert.IsFalse(still.Tick(0.01f, 0f, 1.2f));
        }
    }

    public class RunCamTests
    {
        private static RunCamInput Run(bool aim = false) => new RunCamInput { Sprinting = true, Aiming = aim, MoveY = 1f, Speed = 6.8f, MotionScale = 1f };
        private static RunCamInput Idle => new RunCamInput { MotionScale = 1f };

        private static RunCamOutput For(RunCamModel m, RunCamInput i, float seconds, float dt = 1f / 60f)
        {
            RunCamOutput o = default;
            for (float t = 0; t < seconds; t += dt) o = m.Tick(dt, i);
            return o;
        }

        [Test]
        public void Fatigue_ZeroBeforeDelay_ThenRisesGraduallyToOne()
        {
            var m = new RunCamModel(new CameraRunSettingsData());
            For(m, Run(), 3.9f);
            Assert.AreEqual(0f, m.Fatigue, 1e-4f, "antes de 4 s corriendo no hay cansancio");
            float prev = 0f;
            for (int i = 0; i < 6; i++) { For(m, Run(), 0.5f); Assert.GreaterOrEqual(m.Fatigue, prev); prev = m.Fatigue; }
            Assert.That(m.Fatigue, Is.InRange(0.2f, 0.95f), "sube de forma gradual");
            For(m, Run(), 5f);
            Assert.AreEqual(1f, m.Fatigue, 1e-4f);
            Assert.Greater(m.RunTime, 9f); Assert.Greater(m.TotalSprintSeconds, 9f);
        }

        [Test]
        public void Fatigue_RaisesAmplitudeUpTo40Percent()
        {
            var fresh = new RunCamModel(new CameraRunSettingsData()); var tired = new RunCamModel(new CameraRunSettingsData());
            float peakFresh = 0f, peakTired = 0f;
            For(fresh, Run(), 1f); For(tired, Run(), 12f);
            for (int i = 0; i < 120; i++) { peakFresh = System.Math.Max(peakFresh, System.Math.Abs(fresh.Tick(1f / 60f, Run()).OffsetX)); }
            for (int i = 0; i < 120; i++) { peakTired = System.Math.Max(peakTired, System.Math.Abs(tired.Tick(1f / 60f, Run()).OffsetX)); }
            Assert.Greater(peakTired, peakFresh * 1.15f);
            Assert.LessOrEqual(peakTired, peakFresh * 1.4f * 1.25f + 0.002f, "como mucho +40 % (más la asimetría lenta)");
        }

        [Test]
        public void Recovery_ReturnsFatigueToZeroWithinRecoverSeconds()
        {
            var s = new CameraRunSettingsData();
            var m = new RunCamModel(s);
            For(m, Run(), 12f);
            Assert.AreEqual(1f, m.Fatigue, 1e-4f);
            For(m, Idle, s.recoverSeconds * 0.5f);
            Assert.That(m.Fatigue, Is.InRange(0.05f, 0.95f), "baja con una curva, no de golpe");
            For(m, Idle, s.recoverSeconds * 0.55f);
            Assert.AreEqual(0f, m.Fatigue, 1e-5f);
            Assert.AreEqual(0f, m.RunTime);
        }

        [Test]
        public void HardLimits_NeverExceeded_EvenWithAbsurdSettings()
        {
            var s = new CameraRunSettingsData { swayMeters = 1f, bobMeters = 1f, rollDegrees = 20f, maxRoll = 9f, maxOffset = 1f, fatigueAmplitudeBoost = 5f, fovBoost = 30f };
            var m = new RunCamModel(s);
            for (int i = 0; i < 60 * 20; i++)
            {
                var o = m.Tick(1f / 60f, Run());
                Assert.LessOrEqual(System.Math.Abs(o.Roll), 1.5f + 1e-5f);
                Assert.LessOrEqual(System.Math.Sqrt(o.OffsetX * o.OffsetX + o.OffsetY * o.OffsetY), 0.08f + 1e-5f);
                Assert.LessOrEqual(o.FovDelta, 8f + 1e-4f);
            }
        }

        [Test]
        public void DefaultAmplitudes_AreWithinTheRequestedRanges()
        {
            var m = new RunCamModel(new CameraRunSettingsData());
            float maxX = 0f, maxRoll = 0f, maxY = 0f, fov = 0f;
            for (int i = 0; i < 60 * 3; i++) { var o = m.Tick(1f / 60f, Run()); maxX = System.Math.Max(maxX, System.Math.Abs(o.OffsetX)); maxRoll = System.Math.Max(maxRoll, System.Math.Abs(o.Roll)); maxY = System.Math.Max(maxY, System.Math.Abs(o.OffsetY)); fov = o.FovDelta; }
            Assert.That(maxX, Is.InRange(0.03f, 0.06f + 0.012f), "balanceo lateral ±3..6 cm");
            Assert.That(maxRoll, Is.InRange(0.4f, 1.0f + 0.2f), "roll ±0.4..1.0°");
            Assert.That(maxY, Is.InRange(0.02f, 0.04f + 0.005f), "rebote 2..4 cm");
            Assert.That(fov, Is.InRange(4f, 8f), "FOV +4..+8");
        }

        [Test]
        public void Aiming_CancelsTheEffectWithin015Seconds()
        {
            var s = new CameraRunSettingsData();
            var m = new RunCamModel(s);
            For(m, Run(), 10f);
            For(m, Run(aim: true), s.aimCancelSeconds + 0.005f);
            for (int i = 0; i < 30; i++)
            {
                var o = m.Tick(1f / 60f, Run(aim: true));
                Assert.AreEqual(0f, o.OffsetX, 1e-5f); Assert.AreEqual(0f, o.Roll, 1e-5f); Assert.AreEqual(0f, o.Pitch, 1e-5f); Assert.AreEqual(0f, o.FovDelta, 1e-5f);
            }
        }

        [Test]
        public void Context_DisablesOrHalvesTheEffect()
        {
            float Peak(RunCamInput i) { var m = new RunCamModel(new CameraRunSettingsData()); float p = 0f; for (int k = 0; k < 180; k++) p = System.Math.Max(p, System.Math.Abs(m.Tick(1f / 60f, i).Roll)); return p; }
            float normal = Peak(Run());
            var crouch = Run(); crouch.Crouched = true;
            var cover = Run(); cover.InCover = true;
            var air = Run(); air.Airborne = true;
            var vault = Run(); vault.Vaulting = true;
            var side = Run(); side.MoveY = 0.3f; side.MoveX = 1f;
            Assert.Less(Peak(crouch), normal * 0.15f); Assert.Less(Peak(cover), normal * 0.15f); Assert.Less(Peak(air), normal * 0.15f); Assert.Less(Peak(vault), normal * 0.15f);
            Assert.AreEqual(normal * 0.5f, Peak(side), normal * 0.2f, "de lado: la mitad");
        }

        [Test]
        public void AccessibilityScale_ZeroRemovesAllMotion()
        {
            var m = new RunCamModel(new CameraRunSettingsData());
            var i = Run(); i.MotionScale = 0f;
            for (int k = 0; k < 600; k++) { var o = m.Tick(1f / 60f, i); Assert.AreEqual(0f, o.OffsetX); Assert.AreEqual(0f, o.Roll); Assert.AreEqual(0f, o.Pitch); Assert.AreEqual(0f, o.FovDelta); }
            Assert.Greater(m.Fatigue, 0f, "la fatiga se sigue midiendo (datos para el director)");
        }

        [Test]
        public void SwayAlternatesSidesWithSteps()
        {
            var m = new RunCamModel(new CameraRunSettingsData());
            For(m, Run(), 1f);
            int changes = 0; float prevSign = 0f;
            for (int k = 0; k < 120; k++)
            {
                var o = m.Tick(1f / 60f, Run());
                float sign = System.Math.Sign(o.OffsetX);
                if (sign != 0 && prevSign != 0 && sign != prevSign) changes++;
                if (sign != 0) prevSign = sign;
            }
            Assert.That(changes, Is.InRange(5, 14), "cambia de lado aproximadamente en cada paso");
        }
    }
}

namespace SevenDays.Tests
{
    public class LeanTests
    {
        [Test]
        public void LeansTowardTheActiveShoulderOnlyWhenAimingInCover()
        {
            var l = new LeanState();
            for (int i = 0; i < 30; i++) l.Tick(1f / 60f, false, true, 1, true);
            Assert.AreEqual(0f, l.Value, "fuera de cobertura no hay lean");
            for (int i = 0; i < 30; i++) l.Tick(1f / 60f, true, true, -1, true);
            Assert.AreEqual(-1f, l.Value, 1e-4f, "hombro izquierdo: se asoma a la izquierda");
            Assert.Greater(l.CameraOffset(), 0.3f); Assert.Greater(l.BodyRoll(), 0f);
            l.Tick(0.05f, true, false, -1, true);
            Assert.Greater(l.Value, -1f); Assert.Less(l.Value, 0f, "vuelve con suavidad al soltar");
            var low = new LeanState(); for (int i = 0; i < 30; i++) low.Tick(1f / 60f, true, true, 1, false);
            Assert.Less(low.Value, 0.5f, "tras una cobertura baja apenas se inclina (se asoma por encima)");
        }
    }
}
