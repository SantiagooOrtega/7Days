using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Soltar, intercambiar y recoger armas sin perder munición.</summary>
    public class LoadoutTests
    {
        private static WeaponData W(string id) => WeaponCatalog.Get(id);

        private static List<WeaponState> Full()
        {
            var l = new List<WeaponState>();
            foreach (var id in new[] { "pistol", "revolver", "smg", "shotgun", "assault_rifle" }) l.Add(new WeaponState(W(id), 3));
            return l;
        }

        [Test]
        public void Drop_KeepsMagazine_AndCannotDropLast()
        {
            var ws = Full();
            var ammo = new AmmoReserve(new AmmoLimits());
            ammo.Add(AmmoType.Pistol, 10);
            int before = Loadout.TotalRounds(ws, ammo);
            var dropped = Loadout.Drop(ws, 2);
            Assert.AreEqual("smg", dropped.Data.id);
            Assert.AreEqual(before, Loadout.TotalRounds(ws, ammo) + dropped.Magazine, "las balas del cargador van con el arma");
            var one = new List<WeaponState> { new WeaponState(W("pistol"), 5) };
            Assert.IsNull(Loadout.Drop(one, 0));
            Assert.AreEqual(1, one.Count);
        }

        [Test]
        public void Drop_ThenPickUp_RestoresSameMagazine()
        {
            var ws = Full();
            var d = Loadout.Drop(ws, 1);
            Assert.AreEqual(WeaponPickupKind.Add, Loadout.Decide(ws, d.Data.id, 5));
            Assert.AreEqual(3, Loadout.MagazineFor(d.Data, d.Magazine));
            Assert.AreEqual(W("smg").magazineSize / 2, Loadout.MagazineFor(W("smg"), -1), "arma nueva del mapa: medio cargador");
        }

        [Test]
        public void FullInventory_OffersSwap_AndSwapConservesRounds()
        {
            var ws = Full();
            var ammo = new AmmoReserve(new AmmoLimits());
            Assert.AreEqual(WeaponPickupKind.SwapOffer, Loadout.Decide(ws, "sniper", 5));
            int before = Loadout.TotalRounds(ws, ammo) + 4;   // el francotirador del suelo trae 4 balas
            var old = Loadout.Swap(ws, 0, W("sniper"), 4);
            Assert.AreEqual("pistol", old.Data.id);
            Assert.AreEqual("sniper", ws[0].Data.id);
            Assert.AreEqual(5, ws.Count);
            Assert.AreEqual(before, Loadout.TotalRounds(ws, ammo) + old.Magazine);
        }

        [Test]
        public void Duplicate_GivesAmmo_FromItsMagazine()
        {
            var ws = Full();
            Assert.AreEqual(WeaponPickupKind.AmmoInstead, Loadout.Decide(ws, "pistol", 5));
            Assert.AreEqual(7, Loadout.DuplicateAmmo(W("pistol"), 7));
            Assert.AreEqual(System.Math.Max(1, W("pistol").magazineSize / 2), Loadout.DuplicateAmmo(W("pistol"), -1));
        }

        [Test]
        public void IndexAfterRemove()
        {
            Assert.AreEqual(1, Loadout.IndexAfterRemove(2, 2, 4));
            Assert.AreEqual(0, Loadout.IndexAfterRemove(0, 0, 4));
            Assert.AreEqual(2, Loadout.IndexAfterRemove(1, 3, 4));
        }

        [Test]
        public void HasAnyAmmo()
        {
            var ammo = new AmmoReserve(new AmmoLimits());
            var ws = new List<WeaponState> { new WeaponState(W("pistol"), 0) };
            Assert.IsFalse(Loadout.HasAnyAmmo(ws, ammo));
            ammo.Add(AmmoType.Pistol, 1);
            Assert.IsTrue(Loadout.HasAnyAmmo(ws, ammo));
        }

        [Test]
        public void Horde_ChasesFromAnyDistance_AndNeverLosesThePlayer()
        {
            var sm = new ZombieStateMachine { AggroRange = 18f, AttackRange = 1.5f };
            Assert.AreEqual(ZombieState.Patrol, sm.Tick(0.1f, 60f, false, false, false));
            sm.Horde = true;
            Assert.AreEqual(ZombieState.Chase, sm.Tick(0.1f, 60f, false, false, false));
            Assert.AreEqual(ZombieState.Chase, sm.Tick(0.1f, 200f, false, false, false));
            sm.Reset();
            Assert.IsFalse(sm.Horde, "Reset (pool) quita la horda");
        }

        [Test]
        public void DirectorTuning_HordeParamsValidated()
        {
            var t = new DirectorTuning();
            Assert.IsEmpty(t.Validate());
            t.hordeDelayMax = 9f;
            Assert.IsNotEmpty(t.Validate());
        }
    }
}
