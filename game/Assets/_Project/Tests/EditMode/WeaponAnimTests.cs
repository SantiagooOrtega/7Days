using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class WeaponAnimTests
    {
        private static WeaponData W(string id) { var w = WeaponCatalog.Get(id); w.Validate(); return w; }

        private static List<(float t, ReloadEvent e, int ammo)> Run(ReloadState r, float dt = 0.01f, float max = 10f)
        {
            var log = new List<(float, ReloadEvent, int)>(); float t = 0f;
            while (r.Active && t < max) { t += dt; foreach (var e in r.Tick(dt, out int a)) log.Add((t, e, a)); }
            return log;
        }

        [Test]
        public void Holds_RiflesTwoHandedPistolsOneHandedAtHipTwoAiming_MeleeOneHanded()
        {
            Assert.IsTrue(WeaponHolds.For(WeaponKind.AssaultRifle).TwoHandsHip);
            Assert.IsFalse(WeaponHolds.For(WeaponKind.Pistol).TwoHandsHip); Assert.IsTrue(WeaponHolds.For(WeaponKind.Pistol).TwoHandsAim);
            Assert.IsTrue(WeaponHolds.For(WeaponKind.Melee).OneHandedMelee); Assert.IsFalse(WeaponHolds.For(WeaponKind.Melee).TwoHandsAim);
            foreach (WeaponKind k in System.Enum.GetValues(typeof(WeaponKind)))
            {
                var h = WeaponHolds.For(k);
                Assert.Less(h.SprintPos.Y, h.HipPos.Y + 1e-4f, k + ": corriendo el arma va abajo");
                Assert.That(h.LeftGripFraction, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void MagazineReload_RefillsOnlyAtMagIn_InOrder_AndLastsReloadTime()
        {
            var w = W("assault_rifle"); var r = new ReloadState();
            Assert.IsTrue(r.Begin(w, 20, 90));
            var log = Run(r);
            var order = log.ConvertAll(x => x.e);
            CollectionAssert.AreEqual(new[] { ReloadEvent.MagOut, ReloadEvent.HandToBelt, ReloadEvent.MagIn, ReloadEvent.Slap, ReloadEvent.Done }, order);
            foreach (var x in log) Assert.AreEqual(x.e == ReloadEvent.MagIn ? 20 : 0, x.ammo, x.e.ToString());
            Assert.AreEqual(w.reloadTime, log[log.Count - 1].t, 0.02f);
            Assert.IsFalse(r.Active);
        }

        [Test]
        public void CancelBeforeMagIn_GivesNoAmmo()
        {
            var w = W("pistol"); var r = new ReloadState(); r.Begin(w, 12, 40);
            int total = 0;
            for (int i = 0; i < 40; i++) { r.Tick(0.01f, out int a); total += a; }   // 0.4 s de 1.3: antes del cargador nuevo
            r.Cancel();
            Assert.AreEqual(0, total); Assert.IsFalse(r.Active);
            r.Tick(1f, out int after); Assert.AreEqual(0, after, "cancelada no da nada después");
        }

        [Test]
        public void ShotgunLoadsShellByShell_AndKeepsShellsWhenInterrupted()
        {
            var w = W("shotgun"); var r = new ReloadState();
            Assert.IsTrue(r.Begin(w, 4, 20));
            Assert.AreEqual(ReloadStyle.Shells, r.Style);
            int ammo = 0, shells = 0; float t = 0f;
            while (r.Active && shells < 2) { t += 0.01f; foreach (var e in r.Tick(0.01f, out int a)) { if (e == ReloadEvent.ShellIn) shells++; } ammo += 0; }
            Assert.AreEqual(2, r.ShellsLoaded);
            Assert.IsTrue(r.CanInterruptToFire, "con cartuchos dentro se puede disparar");
            Assert.AreEqual(2, r.Cancel(), "los dos cartuchos metidos se conservan");
            var full = new ReloadState(); full.Begin(w, 6, 3);
            int got = 0; foreach (var x in Run(full)) got += x.ammo;
            Assert.AreEqual(3, got, "solo los que hay en la reserva");
        }

        [Test]
        public void RevolverAndSniper_HaveTheirOwnSequences()
        {
            var rv = new ReloadState(); rv.Begin(W("revolver"), 6, 30);
            CollectionAssert.Contains(Run(rv).ConvertAll(x => x.e), ReloadEvent.CylinderOpen);
            var sn = new ReloadState(); sn.Begin(W("sniper"), 5, 30);
            var ev = Run(sn).ConvertAll(x => x.e);
            CollectionAssert.Contains(ev, ReloadEvent.BoltOpen); CollectionAssert.Contains(ev, ReloadEvent.BoltClose);
            Assert.Less(ev.IndexOf(ReloadEvent.BoltOpen), ev.IndexOf(ReloadEvent.MagIn)); Assert.Less(ev.IndexOf(ReloadEvent.MagIn), ev.IndexOf(ReloadEvent.BoltClose));
        }

        [Test]
        public void NoReload_WhenFullNoReserveOrMelee()
        {
            var r = new ReloadState();
            Assert.IsFalse(r.Begin(W("pistol"), 0, 40)); Assert.IsFalse(r.Begin(W("pistol"), 6, 0)); Assert.IsFalse(r.Begin(W("bar"), 1, 1));
            Assert.IsFalse(r.Active);
        }

        [Test]
        public void Melee_HasAnticipationImpactAndFollowThrough_HeavyIsSlowerAndStronger()
        {
            Assert.IsFalse(MeleeSwing.IsHeavy(0.2f)); Assert.IsTrue(MeleeSwing.IsHeavy(0.5f));
            Assert.Greater(MeleeSwing.ImpactTime(true), MeleeSwing.ImpactTime(false));
            Assert.Greater(MeleeSwing.Total(true), MeleeSwing.Total(false));
            Assert.Less(MeleeSwing.Pose(MeleeSwing.Windup(false), false, false).X, -50f, "anticipación: el arma va atrás");
            Assert.Greater(MeleeSwing.Pose(MeleeSwing.Windup(false) + MeleeSwing.Strike(false), false, false).X, 50f, "seguimiento: pasa de largo");
            var end = MeleeSwing.Pose(MeleeSwing.Total(false) + 0.01f, false, false);
            Assert.AreEqual(0f, end.X); Assert.AreEqual(0f, end.Y);
            Assert.AreEqual(-MeleeSwing.Pose(0.05f, false, false).Y, MeleeSwing.Pose(0.05f, false, true).Y, 1e-4f, "el combo alterna el lado");
            float prev = 0f; bool smooth = true;
            for (float t = 0f; t < MeleeSwing.Total(true); t += 0.005f) { float x = MeleeSwing.Pose(t, true, false).X; if (System.Math.Abs(x - prev) > 15f) smooth = false; prev = x; }
            Assert.IsTrue(smooth, "sin saltos bruscos entre fases");
        }

        [Test]
        public void Draw_TakesAboutAThirdOfASecond()
        {
            var d = new DrawState(); d.Holster();
            Assert.IsFalse(d.Ready);
            float t = 0f; while (!d.Ready) { d.Tick(0.01f); t += 0.01f; }
            Assert.That(t, Is.InRange(0.2f, 0.3f));
            d.Tick(1f); Assert.AreEqual(1f, d.Value);
        }
    }
}
