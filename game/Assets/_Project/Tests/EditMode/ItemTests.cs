using System;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class ItemTests
    {
        // ------------------------------------------------------------ armas
        [Test]
        public void Catalog_HasTheSevenWeapons_WithUniqueIds()
        {
            var w = WeaponCatalog.Defaults();
            Assert.AreEqual(7, w.Count);
            Assert.AreEqual(7, w.Select(x => x.id).Distinct().Count());
            foreach (var kind in Enum.GetValues(typeof(WeaponKind))) Assert.IsTrue(w.Any(x => x.kind.Equals(kind)), "falta " + kind);
        }

        [Test]
        public void EveryWeapon_HasSaneStats_AndRightAmmoType()
        {
            foreach (var w in WeaponCatalog.Defaults())
            {
                Assert.Greater(w.damage, 0f, w.id); Assert.Greater(w.roundsPerMinute, 0f, w.id); Assert.Greater(w.range, 0f, w.id);
                Assert.GreaterOrEqual(w.spreadHip, w.spreadAim, w.id + ": apuntando dispersa menos");
                Assert.AreEqual(3, w.color.Length, w.id);
                if (w.IsMelee) { Assert.AreEqual(AmmoType.None, w.ammoType); Assert.AreEqual(0, w.magazineSize); }
                else { Assert.AreNotEqual(AmmoType.None, w.ammoType, w.id); Assert.Greater(w.magazineSize, 0, w.id); }
            }
        }

        [Test]
        public void AmmoTypes_AreSeparatedByWeapon_PistolShotgunRifle()
        {
            Assert.AreEqual(AmmoType.Pistol, WeaponCatalog.Get("pistol").ammoType);
            Assert.AreEqual(AmmoType.Pistol, WeaponCatalog.Get("revolver").ammoType);
            Assert.AreEqual(AmmoType.Pistol, WeaponCatalog.Get("smg").ammoType);
            Assert.AreEqual(AmmoType.Shotgun, WeaponCatalog.Get("shotgun").ammoType);
            Assert.AreEqual(AmmoType.Rifle, WeaponCatalog.Get("assault_rifle").ammoType);
            Assert.AreEqual(AmmoType.Rifle, WeaponCatalog.Get("sniper").ammoType);
            Assert.AreEqual(AmmoType.None, WeaponCatalog.Get("bar").ammoType);
        }

        [Test]
        public void Rarity_GrowsWithPower_SniperIsEpic_PistolAndBarAreCommon()
        {
            Assert.AreEqual(Rarity.Common, WeaponCatalog.Get("pistol").rarity);
            Assert.AreEqual(Rarity.Common, WeaponCatalog.Get("bar").rarity);
            Assert.AreEqual(Rarity.Uncommon, WeaponCatalog.Get("shotgun").rarity);
            Assert.AreEqual(Rarity.Rare, WeaponCatalog.Get("assault_rifle").rarity);
            Assert.AreEqual(Rarity.Epic, WeaponCatalog.Get("sniper").rarity);
            Assert.Greater(WeaponCatalog.Get("sniper").damage, WeaponCatalog.Get("assault_rifle").damage);
            Assert.Greater(WeaponCatalog.Get("sniper").range, WeaponCatalog.Get("pistol").range);
            Assert.Greater(WeaponCatalog.Get("shotgun").pellets, 1);
            Assert.Greater(WeaponCatalog.Get("smg").roundsPerMinute, WeaponCatalog.Get("pistol").roundsPerMinute);
            for (int r = 0; r < 4; r++) Assert.AreEqual(3, WeaponCatalog.RarityColor((Rarity)r).Length);
        }

        [Test]
        public void Validate_FixesBrokenWeaponData()
        {
            var w = new WeaponData { id = "x", kind = WeaponKind.Pistol, damage = -5, pellets = 0, roundsPerMinute = 0, magazineSize = 0, spreadHip = 1f, spreadAim = 5f, ammoType = AmmoType.None };
            w.Validate();
            Assert.Greater(w.damage, 0f); Assert.AreEqual(1, w.pellets); Assert.Greater(w.roundsPerMinute, 0f); Assert.Greater(w.magazineSize, 0);
            Assert.LessOrEqual(w.spreadAim, w.spreadHip); Assert.AreEqual(AmmoType.Pistol, w.ammoType);
            var melee = new WeaponData { kind = WeaponKind.Melee, ammoType = AmmoType.Rifle, magazineSize = 9 };
            melee.Validate();
            Assert.AreEqual(AmmoType.None, melee.ammoType); Assert.AreEqual(0, melee.magazineSize);
        }

        // ------------------------------------------------------------ reservas
        [Test]
        public void AmmoReserve_HasAMaximumPerType_AndAddReturnsWhatFit()
        {
            var a = new AmmoReserve(new AmmoLimits { pistol = 10, shotgun = 4, rifle = 20 });
            Assert.AreEqual(8, a.Add(AmmoType.Pistol, 8));
            Assert.AreEqual(2, a.Add(AmmoType.Pistol, 8), "solo caben 2 más");
            Assert.AreEqual(10, a.Count(AmmoType.Pistol));
            Assert.IsTrue(a.IsFull(AmmoType.Pistol));
            Assert.AreEqual(0, a.Add(AmmoType.Pistol, 5), "llena: no entra nada (el pickup no se consume)");
            Assert.IsFalse(a.IsFull(AmmoType.Rifle), "cada tipo tiene su propia reserva");
            Assert.AreEqual(0, a.Count(AmmoType.Rifle));
        }

        [Test]
        public void AmmoReserve_TakeNeverGoesNegative_AndIgnoresNone()
        {
            var a = new AmmoReserve(new AmmoLimits());
            a.Add(AmmoType.Shotgun, 6);
            Assert.AreEqual(4, a.Take(AmmoType.Shotgun, 4));
            Assert.AreEqual(2, a.Take(AmmoType.Shotgun, 10));
            Assert.AreEqual(0, a.Count(AmmoType.Shotgun));
            Assert.AreEqual(0, a.Add(AmmoType.None, 5));
            Assert.AreEqual(0, a.Take(AmmoType.None, 5));
            Assert.AreEqual(0, a.Add(AmmoType.Rifle, -3));
        }

        [Test]
        public void AmmoLimits_Defaults_AreDifferentPerType()
        {
            var l = new AmmoLimits();
            Assert.Greater(l.rifle, l.pistol); Assert.Less(l.shotgun, l.pistol);
            Assert.AreEqual(0, l.For(AmmoType.None));
        }

        // ------------------------------------------------------------ botiquines
        [Test]
        public void Medkits_HaveGrowingHealAndTime_AndACarryCap()
        {
            var b = MedkitData.For(MedkitKind.Bandage); var s = MedkitData.For(MedkitKind.Small); var m = MedkitData.For(MedkitKind.Medium); var l = MedkitData.For(MedkitKind.Large);
            Assert.Less(b.Heal, s.Heal); Assert.Less(s.Heal, m.Heal); Assert.Less(m.Heal, l.Heal);
            Assert.Less(b.UseSeconds, s.UseSeconds); Assert.Less(s.UseSeconds, m.UseSeconds); Assert.Less(m.UseSeconds, l.UseSeconds);
            Assert.Greater(b.MaxCarry, l.MaxCarry);
            var inv = new MedkitInventory();
            Assert.AreEqual(1, inv.Add(MedkitKind.Large, 3), "tope de 1 botiquín grande");
            Assert.IsTrue(inv.IsFull(MedkitKind.Large));
            Assert.AreEqual(0, inv.Add(MedkitKind.Large, 1), "lleno: no se consume el pickup");
        }

        [Test]
        public void MedkitInventory_TakeAndBest()
        {
            var inv = new MedkitInventory();
            Assert.IsNull(inv.Best(50f));
            inv.Add(MedkitKind.Bandage, 2); inv.Add(MedkitKind.Medium, 1);
            Assert.AreEqual(MedkitKind.Bandage, inv.Best(10f), "poca vida que curar: la venda");
            Assert.AreEqual(MedkitKind.Medium, inv.Best(70f), "mucha vida que curar: el mediano");
            Assert.IsTrue(inv.TryTake(MedkitKind.Bandage)); Assert.IsTrue(inv.TryTake(MedkitKind.Bandage)); Assert.IsFalse(inv.TryTake(MedkitKind.Bandage));
            Assert.AreEqual(MedkitKind.Medium, inv.Best(10f), "sin vendas, el único que queda");
        }

        [Test]
        public void Healing_TakesTime_ThenHealsOnce_WithoutExceedingMaxHealth()
        {
            var h = new HealingSession();
            var med = MedkitData.For(MedkitKind.Small); // 30 en 2.5 s
            Assert.IsTrue(h.Start(med, 40f, 100f));
            Assert.AreEqual(0f, h.Tick(1f, 40f, 100f)); Assert.AreEqual(0.4f, h.Progress, 1e-3f); Assert.IsTrue(h.Active);
            Assert.AreEqual(0f, h.Tick(1f, 40f, 100f));
            Assert.AreEqual(30f, h.Tick(1f, 40f, 100f), 1e-3f, "al terminar cura todo");
            Assert.IsFalse(h.Active);
            Assert.AreEqual(0f, h.Tick(1f, 70f, 100f), "no cura dos veces");

            var h2 = new HealingSession();
            h2.Start(MedkitData.For(MedkitKind.Large), 90f, 100f);
            Assert.AreEqual(10f, h2.Tick(10f, 90f, 100f), 1e-3f, "no pasa de la vida máxima");
        }

        [Test]
        public void Healing_CanBeCancelledByDamage_AndDoesNotStartWithFullHealth()
        {
            var h = new HealingSession();
            Assert.IsFalse(h.Start(MedkitData.For(MedkitKind.Bandage), 100f, 100f), "vida completa: no se gasta el botiquín");
            Assert.IsFalse(h.Start(MedkitData.For(MedkitKind.Bandage), 0f, 100f), "muerto");
            Assert.IsTrue(h.Start(MedkitData.For(MedkitKind.Medium), 20f, 100f));
            h.Tick(2f, 20f, 100f);
            h.Cancel();
            Assert.IsFalse(h.Active); Assert.AreEqual(0f, h.Progress);
            Assert.AreEqual(0f, h.Tick(10f, 20f, 100f), "cancelado: no cura");
            Assert.IsFalse(HealingSession.CanStart(100f, 100f)); Assert.IsTrue(HealingSession.CanStart(99f, 100f));
        }

        [Test]
        public void Healing_CannotStartTwiceAtTheSameTime()
        {
            var h = new HealingSession();
            Assert.IsTrue(h.Start(MedkitData.For(MedkitKind.Small), 10f, 100f));
            Assert.IsFalse(h.Start(MedkitData.For(MedkitKind.Large), 10f, 100f));
        }
    }
}
