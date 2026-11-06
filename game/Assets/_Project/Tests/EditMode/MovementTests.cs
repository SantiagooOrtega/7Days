using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class MovementTests
    {
        private static MovementSettings S() => new MovementSettings();

        // ------------------------------------------------------------ salto
        [Test]
        public void JumpVelocity_ReachesTheConfiguredHeight()
        {
            var s = S();
            float v = s.JumpVelocity;
            Assert.AreEqual(s.JumpHeight, v * v / (2f * s.Gravity), 1e-3f, "v^2 / 2g debe ser la altura de salto (1.0 m por defecto)");
            Assert.AreEqual(1.0f, s.JumpHeight, 1e-6f);
        }

        [Test]
        public void FallingGravity_IsStrongerThanRisingGravity_SoJumpFeelsAgile()
        {
            var s = S();
            float up = s.Gravity, down = s.Gravity * s.FallGravityMultiplier;
            float timeUp = s.JumpVelocity / up;
            float timeDown = (float)Math.Sqrt(2f * s.JumpHeight / down);
            Assert.Greater(down, up);
            Assert.Less(timeDown, timeUp, "baja más rápido de lo que sube");
            Assert.Less(timeUp + timeDown, 0.65f, "el salto completo dura poco");
        }

        [Test]
        public void Jump_HappensOnTheGround_AndOnlyOnce()
        {
            var j = new JumpController(S());
            Assert.IsTrue(j.Tick(0.016f, true, true));
            Assert.IsFalse(j.Tick(0.016f, false, false), "ya no hay salto pendiente");
            Assert.IsFalse(j.Tick(0.016f, false, true), "en el aire sin coyote no salta de nuevo");
        }

        [Test]
        public void CoyoteTime_AllowsAJumpJustAfterLeavingTheGround()
        {
            var s = S();
            var j = new JumpController(s);
            j.Tick(0.016f, true, false);                          // en el suelo
            Assert.IsFalse(j.Tick(0.05f, false, false));          // cae de un borde
            Assert.IsTrue(j.Tick(0.016f, false, true), "50-66 ms después aún se puede saltar");
        }

        [Test]
        public void CoyoteTime_Expires()
        {
            var s = S();
            var j = new JumpController(s);
            j.Tick(0.016f, true, false);
            j.Tick(s.CoyoteTime + 0.02f, false, false);
            Assert.IsFalse(j.Tick(0.016f, false, true), "pasado el coyote time ya no salta");
        }

        [Test]
        public void JumpBuffer_RemembersAPressJustBeforeLanding()
        {
            var s = S();
            var j = new JumpController(s);
            Assert.IsFalse(j.Tick(0.016f, false, true), "pulsa en el aire");
            Assert.IsTrue(j.Tick(0.05f, true, false), "aterriza 50 ms después: salta solo");
        }

        [Test]
        public void JumpBuffer_Expires()
        {
            var s = S();
            var j = new JumpController(s);
            j.Tick(0.016f, false, true);
            Assert.IsFalse(j.Tick(s.JumpBufferTime + 0.05f, true, false), "pulsado demasiado pronto: se olvida");
        }

        [Test]
        public void ZeroWindows_DisableCoyoteAndBuffer()
        {
            var s = S();
            s.CoyoteTime = 0f; s.JumpBufferTime = 0f;
            var j = new JumpController(s);
            j.Tick(0.016f, true, false);
            Assert.IsFalse(j.Tick(0.016f, false, true), "sin coyote");
            Assert.IsFalse(new JumpController(s).Tick(0.016f, false, true) || new JumpController(s).Tick(0.016f, true, false), "sin buffer");
        }

        [Test]
        public void Consume_CancelsAPendingJump()
        {
            var j = new JumpController(S());
            j.Tick(0.016f, false, true);
            j.Consume();
            Assert.IsFalse(j.Tick(0.016f, true, false));
        }

        // ------------------------------------------------------------ altura vaultable
        [TestCase(0.30f, VaultVerdict.TooLow)]
        [TestCase(0.49f, VaultVerdict.TooLow)]
        [TestCase(0.50f, VaultVerdict.Vault)]
        [TestCase(1.00f, VaultVerdict.Vault)]
        [TestCase(1.30f, VaultVerdict.Vault)]
        [TestCase(1.31f, VaultVerdict.Mantle)]
        [TestCase(1.60f, VaultVerdict.Mantle)]
        [TestCase(1.61f, VaultVerdict.TooHigh)]
        [TestCase(2.60f, VaultVerdict.TooHigh)]
        public void Classify_RespectsTheHeightLimits(float height, VaultVerdict expected)
        {
            Assert.AreEqual(expected, VaultMath.Classify(height, true, S()));
        }

        [Test]
        public void Classify_RequiresClearSpaceOnTop_ButOnlyInsideTheValidRange()
        {
            var s = S();
            Assert.AreEqual(VaultVerdict.Blocked, VaultMath.Classify(1.0f, false, s));
            Assert.AreEqual(VaultVerdict.Blocked, VaultMath.Classify(1.5f, false, s));
            Assert.AreEqual(VaultVerdict.TooLow, VaultMath.Classify(0.3f, false, s), "demasiado bajo: se camina/salta normal");
            Assert.AreEqual(VaultVerdict.TooHigh, VaultMath.Classify(2.0f, false, s));
        }

        [Test]
        public void Mantle_CanBeDisabled_AndLimitsAreConfigurable()
        {
            var s = S();
            s.AllowMantle = false;
            Assert.AreEqual(VaultVerdict.TooHigh, VaultMath.Classify(1.5f, true, s));
            Assert.AreEqual(VaultVerdict.Vault, VaultMath.Classify(1.2f, true, s));

            s = S();
            s.VaultMaxHeight = 1.1f; s.MantleMaxHeight = 1.8f;
            Assert.AreEqual(VaultVerdict.Mantle, VaultMath.Classify(1.2f, true, s));
            Assert.AreEqual(VaultVerdict.Mantle, VaultMath.Classify(1.75f, true, s));
            Assert.AreEqual(VaultVerdict.TooHigh, VaultMath.Classify(1.85f, true, s));
        }

        [Test]
        public void DefaultObstacles_CratesAndBarriersAreVaultable_CarsAreMantle_ContainersAreNot()
        {
            var s = S();
            Assert.AreEqual(VaultVerdict.Vault, VaultMath.Classify(1.2f, true, s), "caja o barrera de 1.2 m");
            Assert.AreEqual(VaultVerdict.Mantle, VaultMath.Classify(1.5f, true, s), "techo de coche de 1.5 m");
            Assert.AreEqual(VaultVerdict.TooHigh, VaultMath.Classify(2.6f, true, s), "contenedor");
        }

        [Test]
        public void MantleIsSlowerThanVault()
        {
            var s = S();
            Assert.Greater(VaultMath.Duration(VaultVerdict.Mantle, s), VaultMath.Duration(VaultVerdict.Vault, s));
        }

        // ------------------------------------------------------------ recorrido
        [Test]
        public void VaultPath_StartsAndEndsExactly_AndClearsTheObstacle()
        {
            var start = new V3(0, 0, 0); var top = new V3(0, 1.2f, 0.8f); var end = new V3(0, 0, 1.9f);
            var p0 = VaultMath.Position(VaultVerdict.Vault, start, top, end, 0f);
            var p1 = VaultMath.Position(VaultVerdict.Vault, start, top, end, 1f);
            Assert.AreEqual(0f, (p0 - start).Length, 1e-4f);
            Assert.AreEqual(0f, (p1 - end).Length, 1e-4f);
            var mid = VaultMath.Position(VaultVerdict.Vault, start, top, end, 0.5f, 0.12f);
            Assert.GreaterOrEqual(mid.Y, 1.2f + 0.12f - 1e-3f, "al cruzar, los pies pasan por encima de la superficie");
            Assert.AreEqual(0.95f, mid.Z, 0.05f, "el punto medio está sobre el obstáculo");
        }

        [Test]
        public void VaultPath_NeverGoesBelowTheGround_AndMovesForwardMonotonically()
        {
            var start = new V3(0, 0, 0); var top = new V3(0, 1.0f, 0.7f); var end = new V3(0, 0, 1.7f);
            float lastZ = -1f;
            for (int i = 0; i <= 50; i++)
            {
                var p = VaultMath.Position(VaultVerdict.Vault, start, top, end, i / 50f);
                Assert.GreaterOrEqual(p.Y, -1e-3f, "no se hunde en el suelo");
                Assert.GreaterOrEqual(p.Z, lastZ - 1e-4f, "avanza siempre");
                lastZ = p.Z;
            }
        }

        [Test]
        public void MantlePath_RisesFirst_ThenMovesOntoTheTop()
        {
            var start = new V3(0, 0, 0); var top = new V3(0, 1.5f, 0.5f); var end = new V3(0, 1.5f, 0.9f);
            var p0 = VaultMath.Position(VaultVerdict.Mantle, start, top, end, 0f);
            var p1 = VaultMath.Position(VaultVerdict.Mantle, start, top, end, 1f);
            Assert.AreEqual(0f, (p0 - start).Length, 1e-4f);
            Assert.AreEqual(0f, (p1 - end).Length, 1e-4f);

            var atSplit = VaultMath.Position(VaultVerdict.Mantle, start, top, end, 0.6f);
            Assert.AreEqual(1.5f, atSplit.Y, 1e-3f, "a los 60% ya está a la altura de la superficie");
            Assert.Less(atSplit.Z, 0.25f, "casi sin avanzar: primero sube");
            float lastY = -1f;
            for (int i = 0; i <= 40; i++)
            {
                var p = VaultMath.Position(VaultVerdict.Mantle, start, top, end, i / 40f);
                Assert.GreaterOrEqual(p.Y, lastY - 1e-4f, "la trepada solo sube");
                lastY = p.Y;
            }
        }

        [Test]
        public void PositionClampsT()
        {
            var a = new V3(0, 0, 0); var t = new V3(0, 1, 1); var b = new V3(0, 0, 2);
            Assert.AreEqual(0f, (VaultMath.Position(VaultVerdict.Vault, a, t, b, -3f) - a).Length, 1e-4f);
            Assert.AreEqual(0f, (VaultMath.Position(VaultVerdict.Vault, a, t, b, 9f) - b).Length, 1e-4f);
        }

        // ------------------------------------------------------------ caída
        [Test]
        public void FallDamage_OnlyAboveThreshold_AndGrowsWithHeight()
        {
            var s = S();
            Assert.AreEqual(0f, FallDamage.Compute(0f, s));
            Assert.AreEqual(0f, FallDamage.Compute(3.9f, s));
            Assert.AreEqual(0f, FallDamage.Compute(4f, s), "exactamente 4 m no hace daño");
            Assert.AreEqual(2f * s.FallDamagePerMeter, FallDamage.Compute(6f, s), 1e-3f);
            Assert.Greater(FallDamage.Compute(9f, s), FallDamage.Compute(6f, s));
        }

        [Test]
        public void FallDamage_ThresholdIsConfigurable()
        {
            var s = S();
            s.FallDamageThreshold = 8f;
            Assert.AreEqual(0f, FallDamage.Compute(7f, s));
            Assert.Greater(FallDamage.Compute(9f, s), 0f);
        }

        [Test]
        public void LandImpulse_IsZeroForSmallDrops_AndStrongerForHighOnes()
        {
            var s = S();
            Assert.AreEqual(0f, FallDamage.LandImpulse(0.4f, s));
            float small = FallDamage.LandImpulse(1.5f, s), big = FallDamage.LandImpulse(6f, s);
            Assert.Greater(small, 0f);
            Assert.Greater(big, small);
            Assert.AreEqual(s.LandImpulseMax, big, 1e-3f, "satura en el máximo");
        }

        // ------------------------------------------------------------ validación
        [Test]
        public void Validate_FixesImpossibleValues()
        {
            var s = new MovementSettings { VaultMinHeight = 1.5f, VaultMaxHeight = 0.5f, MantleMaxHeight = 0.4f, CoyoteTime = -1f, AirControl = 9f, Gravity = 0f, FallDamageThreshold = 0f };
            s.Validate();
            Assert.LessOrEqual(s.VaultMinHeight, s.VaultMaxHeight);
            Assert.LessOrEqual(s.VaultMaxHeight, s.MantleMaxHeight);
            Assert.AreEqual(0f, s.CoyoteTime);
            Assert.AreEqual(1f, s.AirControl);
            Assert.Greater(s.Gravity, 0f);
            Assert.GreaterOrEqual(s.FallDamageThreshold, 1f);
        }

        [Test]
        public void Defaults_MatchTheDesign()
        {
            var s = S();
            Assert.AreEqual(1.0f, s.JumpHeight);
            Assert.AreEqual(0.1f, s.CoyoteTime);
            Assert.AreEqual(0.1f, s.JumpBufferTime);
            Assert.AreEqual(0.5f, s.VaultMinHeight);
            Assert.AreEqual(1.3f, s.VaultMaxHeight);
            Assert.AreEqual(1.6f, s.MantleMaxHeight);
            Assert.AreEqual(4f, s.FallDamageThreshold);
        }
    }
}
