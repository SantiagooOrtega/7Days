using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class WeaponHandlingTests
    {
        private static WeaponData W(string id) { var w = WeaponCatalog.Get(id); w.Validate(); return w; }

        [Test]
        public void EveryWeapon_HasHandlingValues_AndCurrentDataVersion()
        {
            foreach (var w in WeaponCatalog.Defaults())
            {
                Assert.AreEqual(WeaponData.CurrentDataVersion, w.dataVersion, w.id);
                Assert.Greater(w.fireModes.Length, 0, w.id);
                Assert.Greater(w.aimFov, 0f, w.id);
                Assert.GreaterOrEqual(w.spreadMax, w.spreadHip, w.id);
                Assert.LessOrEqual(w.spreadAim, w.spreadHip, w.id);
            }
            Assert.IsTrue(W("sniper").aimFirstPerson); Assert.IsTrue(W("sniper").HasScope); Assert.Greater(W("sniper").scopeFovs.Length, 1, "zoom variable");
            CollectionAssert.Contains(W("assault_rifle").fireModes, FireModes.Burst);
            Assert.AreEqual(FireModes.Pump, W("shotgun").DefaultFireMode);
            Assert.AreEqual(FireModes.Bolt, W("sniper").DefaultFireMode);
        }

        [Test]
        public void Recoil_ClimbsWithAShortBoundedLateralPattern()
        {
            var w = W("assault_rifle"); var r = new RecoilState(); var rng = new Random(3);
            float maxYaw = 0f;
            for (int i = 0; i < 30; i++) { r.OnShot(w, false, false, rng.NextDouble()); maxYaw = Math.Max(maxYaw, Math.Abs(r.Yaw)); }
            Assert.AreEqual(w.recoilMaxPitch, r.Pitch, 1e-4f, "el vertical se acumula hasta su tope");
            Assert.LessOrEqual(maxYaw, w.recoilMaxPitch * 0.5f + 1e-4f, "lateral acotado");
            Assert.Greater(maxYaw, 0f, "hay desviación lateral");
        }

        [Test]
        public void Recoil_IsSmallerAimingAndCrouched()
        {
            var w = W("revolver");
            var hip = new RecoilState(); var aim = new RecoilState(); var crouchAim = new RecoilState();
            hip.OnShot(w, false, false, 0.5); aim.OnShot(w, true, false, 0.5); crouchAim.OnShot(w, true, true, 0.5);
            Assert.Greater(hip.Pitch, aim.Pitch); Assert.Greater(aim.Pitch, crouchAim.Pitch);
        }

        [Test]
        public void Recoil_DoesNotRecoverWhileSprayingButRecoversAfterRelease()
        {
            var w = W("smg"); var r = new RecoilState();
            for (int i = 0; i < 5; i++) { r.Tick(w.SecondsBetweenShots, true, w); r.OnShot(w, false, false, 0.7); }
            float during = r.Pitch;
            Assert.Greater(during, 0f);
            r.Tick(w.SecondsBetweenShots, true, w);   // entre dos disparos de la ráfaga
            Assert.AreEqual(during, r.Pitch, 1e-4f, "manteniendo el gatillo no se recupera");
            for (int i = 0; i < 120; i++) r.Tick(1f / 60f, false, w);
            Assert.AreEqual(0f, r.Pitch, 1e-4f); Assert.AreEqual(0f, r.Yaw, 1e-4f);
        }

        [Test]
        public void Spread_GrowsInBurstsAndWithMovement_AndRecovers()
        {
            var w = W("assault_rifle"); var s = new SpreadState();
            float still = s.Current(w, false, false, 0f, false);
            Assert.AreEqual(w.spreadHip, still, 1e-5f);
            for (int i = 0; i < 10; i++) s.OnShot(w, false);
            Assert.Greater(s.Current(w, false, false, 0f, false), still + 1f, "la ráfaga abre la dispersión");
            Assert.LessOrEqual(s.Current(w, false, false, 1f, true), w.spreadMax + 1e-4f, "nunca pasa del máximo");
            Assert.Greater(new SpreadState().Current(w, false, false, 1f, false), still, "corriendo dispersa más");
            Assert.Less(new SpreadState().Current(w, true, false, 0f, false), still, "apuntando dispersa menos");
            Assert.Less(new SpreadState().Current(w, false, true, 0f, false), still, "agachado dispersa menos");
            for (int i = 0; i < 120; i++) s.Tick(1f / 60f, w);
            Assert.AreEqual(0f, s.Bloom, 1e-5f);
        }

        [Test]
        public void FireModes_SemiAutoBurstPumpBolt()
        {
            var ar = W("assault_rifle"); var fc = new FireControl(); fc.SetWeapon(ar);
            Assert.AreEqual(FireModes.Auto, fc.Mode);
            int shots = 0; float t = 0f;
            for (int i = 0; i < 60; i++, t += 1f / 60f) if (fc.Wants(t, i == 0, true)) { fc.Commit(t, ar); shots++; }
            Assert.That(shots, Is.InRange(8, 10), "automático a 520 rpm durante 1 s");

            fc.Cycle(ar); Assert.AreEqual(FireModes.Burst, fc.Mode);
            shots = 0; t = 10f;
            for (int i = 0; i < 60; i++, t += 1f / 60f) if (fc.Wants(t, i == 0, true)) { fc.Commit(t, ar); shots++; }
            Assert.AreEqual(ar.burstCount, shots, "un clic = una ráfaga aunque mantengas");

            fc.Cycle(ar); Assert.AreEqual(FireModes.Semi, fc.Mode);
            shots = 0; t = 20f;
            for (int i = 0; i < 60; i++, t += 1f / 60f) if (fc.Wants(t, i == 0, true)) { fc.Commit(t, ar); shots++; }
            Assert.AreEqual(1, shots, "semiautomático: un disparo por clic");
            fc.Cycle(ar); Assert.AreEqual(FireModes.Auto, fc.Mode, "vuelve al primero");

            var sg = W("shotgun"); var p = new FireControl(); p.SetWeapon(sg);
            Assert.IsFalse(p.Cycle(sg), "la escopeta solo tiene bombeo");
        }

        [Test]
        public void FireControl_BuffersAnEarlyClick_AndCancelStopsBursts()
        {
            var pistol = W("pistol"); var fc = new FireControl(); fc.SetWeapon(pistol);
            Assert.IsTrue(fc.Wants(0f, true, true)); fc.Commit(0f, pistol);
            float early = pistol.SecondsBetweenShots - 0.05f;
            Assert.IsFalse(fc.Wants(early, true, true), "aún no está lista");
            Assert.IsTrue(fc.Wants(pistol.SecondsBetweenShots + 0.01f, false, false), "el clic de antes se recuerda");
            var ar = W("assault_rifle"); var b = new FireControl(); b.SetWeapon(ar); b.Cycle(ar);
            b.Wants(0f, true, true); b.Commit(0f, ar);
            Assert.Greater(b.BurstLeft, 0);
            b.Cancel(0f, 0.5f);
            Assert.AreEqual(0, b.BurstLeft); Assert.IsFalse(b.Wants(0.3f, false, false));
        }

        [Test]
        public void Validate_ClampsHandlingValues()
        {
            var w = new WeaponData { id = "x", kind = WeaponKind.Pistol, recoilHorizontal = 99f, aimFov = 500f, adsTime = -1f, scopeFovs = new[] { 0.1f }, fireModes = new[] { "laser" }, spreadHip = 3f, spreadMax = 1f };
            w.Validate();
            Assert.LessOrEqual(w.recoilHorizontal, 3f); Assert.LessOrEqual(w.aimFov, 70f); Assert.GreaterOrEqual(w.adsTime, 0.05f);
            Assert.GreaterOrEqual(w.scopeFovs[0], 2f); Assert.AreEqual(0, w.fireModes.Length, "modo desconocido: se ignora la lista");
            Assert.GreaterOrEqual(w.spreadMax, w.spreadHip);
        }
    }
}
